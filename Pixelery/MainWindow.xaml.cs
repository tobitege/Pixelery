using Pixelery.Services;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Pixelery
{
    public sealed partial class MainWindow : Window
    {
        private const int MinimumWindowWidth = 900;
        private const int MinimumWindowHeight = 600;
        private const int WmGetMinMaxInfo = 0x0024;
        private static readonly IntPtr MinimumSizeSubclassId = new(1);
        private IntPtr _windowHandle;
        private SUBCLASSPROC? _minSizeSubclassProc;
        private bool _isSystemMinSizeActive;
        private bool _isActive;
        private const int DwmwaBorderColor = 34;
        private const uint DwmColorDefault = 0xFFFFFFFF;

        public MainWindow()
        {
            InitializeComponent();

            Title = "Pixelery";
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(Shell.TitleBarElement);
            if (GetSafeAppWindow() is { } appWindow)
            {
                appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }

            InitializeMinimumWindowSize();
            ApplyWindowBounds();

            WindowsAccent.Initialize(DispatcherQueue);
            WindowsAccent.Changed += OnAccentChanged;
            Activated += OnWindowActivated;

            if (GetSafeAppWindow() is { } window)
            {
                window.Changed += OnAppWindowChanged;
            }

            Closed += OnWindowClosed;
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            RootGrid.Loaded += (_, _) => UpdateCaptionButtonColors();

            Shell.Initialize(this);
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            DispatcherQueue.TryEnqueue(UpdateCaptionButtonColors);
        }

        /// <summary>
        /// Caption buttons are drawn by the system; tint them with the active Daisy palette.
        /// </summary>
        private void UpdateCaptionButtonColors()
        {
            var appWindow = GetSafeAppWindow();
            if (appWindow == null)
                return;

            var foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") is SolidColorBrush fg
                ? fg.Color
                : Colors.White;
            var titleBar = appWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(140, foreground.R, foreground.G, foreground.B);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(28, foreground.R, foreground.G, foreground.B);
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(48, foreground.R, foreground.G, foreground.B);
            titleBar.ButtonPressedForegroundColor = foreground;
        }

        private void ApplyWindowBounds()
        {
            if (!TryApplyBounds(AppSettings.LoadWindowBounds()))
            {
                TryApplyBounds(AppSettings.DefaultWindowBounds);
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange)
                return;
            if ((args.DidPositionChange || args.DidSizeChange) && sender.Presenter.Kind == AppWindowPresenterKind.Overlapped
                && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            {
                SaveCurrentWindowBounds();
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            _isActive = args.WindowActivationState != WindowActivationState.Deactivated;
            ApplyBorderColor();
        }

        private void OnAccentChanged(object? sender, EventArgs e) => ApplyBorderColor();

        /// <summary>
        /// The active window frame uses the Windows accent color (also when the system setting draws it gray);
        /// the inactive frame keeps the system default.
        /// </summary>
        private void ApplyBorderColor()
        {
            if (_windowHandle == IntPtr.Zero)
                return;
            var accent = WindowsAccent.Color;
            var color = _isActive ? (uint)(accent.R | (accent.G << 8) | (accent.B << 16)) : DwmColorDefault;
            DwmSetWindowAttribute(_windowHandle, DwmwaBorderColor, ref color, sizeof(uint));
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            WindowsAccent.Changed -= OnAccentChanged;
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            Shell.Shutdown();

            if (_isSystemMinSizeActive && _windowHandle != IntPtr.Zero && _minSizeSubclassProc != null)
            {
                RemoveWindowSubclass(_windowHandle, _minSizeSubclassProc, MinimumSizeSubclassId);
                _isSystemMinSizeActive = false;
            }
        }

        private void SaveCurrentWindowBounds()
        {
            var appWindow = GetSafeAppWindow();
            if (appWindow == null)
                return;
            var position = appWindow.Position;
            var size = appWindow.Size;
            if (size.Width > 0 && size.Height > 0)
            {
                AppSettings.SaveWindowBounds(new WindowBounds(position.X, position.Y, size.Width, size.Height));
            }
        }

        private bool TryApplyBounds(WindowBounds? bounds)
        {
            if (bounds is not { } value || !HasVisibleWindowArea(value))
                return false;

            var appWindow = GetSafeAppWindow();
            if (appWindow == null)
                return false;

            try
            {
                var rect = new RectInt32
                {
                    X = value.X,
                    Y = value.Y,
                    Width = Math.Max(value.Width, MinimumWindowWidth),
                    Height = Math.Max(value.Height, MinimumWindowHeight)
                };

                // Keep the window inside the work area (never behind the taskbar).
                if (DisplayArea.GetFromRect(rect, DisplayAreaFallback.Nearest) is { } display)
                {
                    var work = display.WorkArea;
                    rect.Width = Math.Min(rect.Width, work.Width);
                    rect.Height = Math.Min(rect.Height, work.Height);
                    rect.X = Math.Clamp(rect.X, work.X, work.X + work.Width - rect.Width);
                    rect.Y = Math.Clamp(rect.Y, work.Y, work.Y + work.Height - rect.Height);
                }

                appWindow.MoveAndResize(rect);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasVisibleWindowArea(WindowBounds bounds)
        {
            var rect = new RectInt32
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = Math.Max(bounds.Width, MinimumWindowWidth),
                Height = Math.Max(bounds.Height, MinimumWindowHeight)
            };

            try
            {
                var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.Nearest);
                if (displayArea == null)
                    return true;

                var workArea = displayArea.WorkArea;
                var visibleWidth = Math.Min(rect.X + rect.Width, workArea.X + workArea.Width) - Math.Max(rect.X, workArea.X);
                var visibleHeight = Math.Min(rect.Y + rect.Height, workArea.Y + workArea.Height) - Math.Max(rect.Y, workArea.Y);
                return visibleWidth >= 200 && visibleHeight >= 200;
            }
            catch
            {
                return true;
            }
        }

        private void InitializeMinimumWindowSize()
        {
            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (_windowHandle == IntPtr.Zero)
                return;

            _minSizeSubclassProc ??= MinimumSizeSubclassProc;
            _isSystemMinSizeActive = SetWindowSubclass(_windowHandle, _minSizeSubclassProc, MinimumSizeSubclassId, IntPtr.Zero);
        }

        private IntPtr MinimumSizeSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WmGetMinMaxInfo)
            {
                var scale = GetDpiForWindow(hWnd) / 96.0;
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = (int)(MinimumWindowWidth * scale);
                minMaxInfo.ptMinTrackSize.y = (int)(MinimumWindowHeight * scale);
                Marshal.StructureToPtr(minMaxInfo, lParam, false);
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private AppWindow? GetSafeAppWindow()
        {
            try
            {
                return AppWindow;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref uint value, int size);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
    }
}
