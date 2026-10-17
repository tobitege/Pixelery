using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Styles;
using Pixelery.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>Exact parameters for one slide, e.g. to open the image of the style preview in full screen.</summary>
    public sealed record SlideOverride(ArtStyle Style, int Seed, Core.Palettes.PaletteFamily Family, Core.Palettes.BackgroundTone Tone, DetailLevel Detail,
        Core.Palettes.PaletteSample? SamplePalette = null);

    /// <summary>
    /// Endless slideshow of freshly generated images at window resolution. Nothing is written to disk.
    /// The next image is rendered in the background while the current one is shown.
    /// Keys: Esc exit, Space pause, ←/→ browse, I info, F full screen.
    /// </summary>
    public sealed partial class SlideshowWindow : Window
    {
        private const int CacheSize = 8;

        private readonly GeneratorSettings _settings;
        private readonly List<ArtStyle> _styles;
        private readonly int _masterSeed;
        private readonly TimeSpan _interval;
        private readonly bool _crossfade;
        private readonly Grid _root;
        private readonly Image[] _layers;
        private readonly Border _infoPanel;
        private readonly TextBlock _infoText;
        private readonly Border _hintPanel;
        private readonly Border _pausedBadge;
        private readonly DaisyLoading _loading;
        private readonly DispatcherQueueTimer _timer;
        private readonly DispatcherQueueTimer _hintTimer;
        private readonly DispatcherQueueTimer _cursorTimer;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<int, (BitmapImage Bitmap, string Info)> _cache = [];
        private readonly Dictionary<int, Task<(byte[] Jpeg, string Info)>> _pending = [];
        private int _current = -1;
        private int _activeLayer;
        private bool _busy;
        private bool _paused;
        private bool _showInfo;
        private bool _cursorHidden;
        private bool _started;
        private readonly SlideOverride? _first;

        public SlideshowWindow(Microsoft.UI.WindowId sourceWindowId, GeneratorSettings settings, string seed, TimeSpan interval, bool crossfade, bool showInfo,
            bool startPaused = false, SlideOverride? first = null)
        {
            _paused = startPaused;
            _first = first;
            _settings = settings.Clone();
            _settings.SpreadDates = false;
            _styles = [.. _settings.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>()];
            _masterSeed = ImageGenerator.ParseSeed(seed);
            _interval = interval;
            _crossfade = crossfade;
            _showInfo = showInfo;

            Title = L("Slide_Title");

            _layers = [NewLayer(), NewLayer()];
            _loading = new DaisyLoading
            {
                Variant = DaisyLoadingVariant.Dots,
                Size = DaisySize.Large,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _infoText = new TextBlock { Foreground = new SolidColorBrush(Colors.White), FontSize = 14, TextWrapping = TextWrapping.Wrap };
            _infoPanel = Overlay(_infoText, HorizontalAlignment.Left, VerticalAlignment.Bottom);
            _hintPanel = Overlay(new TextBlock { Text = L("Slide_Hint"), Foreground = new SolidColorBrush(Colors.White), FontSize = 14 },
                HorizontalAlignment.Center, VerticalAlignment.Top);
            _pausedBadge = Overlay(new TextBlock { Text = L("Slide_Paused"), Foreground = new SolidColorBrush(Colors.White), FontSize = 14, FontWeight = FontWeights.SemiBold },
                HorizontalAlignment.Right, VerticalAlignment.Top);
            _pausedBadge.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
            _infoPanel.Visibility = Visibility.Collapsed;

            _root = new Grid { Background = new SolidColorBrush(Colors.Black), IsTabStop = true, UseSystemFocusVisuals = false };
            _root.Children.Add(_layers[0]);
            _root.Children.Add(_layers[1]);
            _root.Children.Add(_loading);
            _root.Children.Add(_infoPanel);
            _root.Children.Add(_hintPanel);
            _root.Children.Add(_pausedBadge);
            // The image layers swap z-index 0/1 for cross-fading; overlays stay above them.
            foreach (var overlay in new UIElement[] { _loading, _infoPanel, _hintPanel, _pausedBadge })
                Canvas.SetZIndex(overlay, 10);
            _root.KeyDown += OnKeyDown;
            _root.PointerMoved += (_, _) => ShowCursorTemporarily();
            _root.DoubleTapped += (_, _) => ToggleFullScreen();
            _root.Tapped += (_, _) => _root.Focus(FocusState.Programmatic);
            _root.SizeChanged += (_, _) => StartIfReady();
            Content = _root;

            var queue = DispatcherQueue;
            _timer = queue.CreateTimer();
            _timer.Interval = _interval;
            _timer.IsRepeating = false;
            _timer.Tick += (_, _) => _ = AdvanceAsync(1);
            _hintTimer = queue.CreateTimer();
            _hintTimer.Interval = TimeSpan.FromSeconds(4);
            _hintTimer.IsRepeating = false;
            _hintTimer.Tick += (_, _) => _hintPanel.Visibility = Visibility.Collapsed;
            _cursorTimer = queue.CreateTimer();
            _cursorTimer.Interval = TimeSpan.FromSeconds(2);
            _cursorTimer.IsRepeating = false;
            _cursorTimer.Tick += (_, _) => SetCursorHidden(IsFullScreen);

            Activated += (_, _) => _root.Focus(FocusState.Programmatic);
            Closed += OnClosed;

            // Place the new window on its caller's monitor before entering full screen.
            if (DisplayArea.GetFromWindowId(sourceWindowId, DisplayAreaFallback.Nearest) is { } display)
            {
                var work = display.WorkArea;
                var width = (int)(work.Width * 0.7);
                var height = (int)(work.Height * 0.7);
                AppWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
            }
            _hintTimer.Start();
        }

        public void Show(bool fullScreen)
        {
            // Initial activation restores the overlapped presenter, so enter full screen afterward.
            Activate();
            if (fullScreen)
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        private bool IsFullScreen => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

        public bool IsPaused => _paused;

        private static Image NewLayer() => new() { Stretch = Stretch.UniformToFill, Opacity = 0 };

        private static Border Overlay(UIElement child, HorizontalAlignment horizontal, VerticalAlignment vertical) => new()
        {
            Child = child,
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(24),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            IsHitTestVisible = false
        };

        private void StartIfReady()
        {
            if (_started || _root.ActualWidth < 16 || _root.ActualHeight < 16)
                return;
            _started = true;
            _ = AdvanceAsync(1);
        }

        private (int Width, int Height) RenderSize()
        {
            var scale = _root.XamlRoot?.RasterizationScale ?? 1;
            return (Math.Max(64, (int)(_root.ActualWidth * scale)), Math.Max(64, (int)(_root.ActualHeight * scale)));
        }

        private Task<(byte[] Jpeg, string Info)> RenderAsync(int index)
        {
            if (_pending.TryGetValue(index, out var task))
                return task;
            var (width, height) = RenderSize();
            var settings = _settings;
            var styles = _styles;
            var seed = ImageGenerator.ImageSeed(_masterSeed, index);
            var first = index == 0 ? _first : null;
            var token = _cts.Token;
            task = Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var size = new SizeChoice(width, height, SizeCatalog.RatioLabel(width, height));
                var recipe = ImageGenerator.CreateRecipe(settings, styles, [size], seed, index + 1, DateTime.Now);
                if (first != null)
                    recipe = recipe with { Style = first.Style, Seed = first.Seed, Family = first.Family, Tone = first.Tone, Detail = first.Detail, SamplePalette = first.SamplePalette };
                var (rendered, palette) = ImageGenerator.Render(recipe);
                var image = rendered;
                if (settings.Watermark.ShowInSlideshow)
                {
                    var values = ImageGenerator.CreateNameValues(recipe, 0, ImageGenerator.PaletteSlug(palette), DateTime.Now);
                    image = Core.Watermark.WatermarkRenderer.Apply(rendered, settings.Watermark, values, recipe.Seed);
                    if (!ReferenceEquals(image, rendered))
                        rendered.Dispose();
                }
                using (image)
                {
                    var info = $"{recipe.Style.Name} · {palette.Name} · {recipe.Family.ToString().ToLowerInvariant()}/{recipe.Tone.ToString().ToLowerInvariant()} · #{index + 1} · seed {(uint)recipe.Seed:x8}";
                    return (ImageEncoder.Encode(image, OutputFormat.Jpeg, 92, null), info);
                }
            }, token);
            _pending[index] = task;
            return task;
        }

        private async Task<(BitmapImage Bitmap, string Info)?> GetSlideAsync(int index)
        {
            if (_cache.TryGetValue(index, out var cached))
                return cached;
            try
            {
                var (jpeg, info) = await RenderAsync(index);
                _pending.Remove(index);
                if (_cts.IsCancellationRequested)
                    return null;
                var slide = (await PreviewRenderer.ToBitmapAsync(jpeg), info);
                _cache[index] = slide;
                foreach (var old in _cache.Keys.Where(k => Math.Abs(k - index) > CacheSize / 2).ToList())
                    _cache.Remove(old);
                return slide;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>Shows the image <paramref name="delta"/> steps away and prepares the following one.</summary>
        private async Task AdvanceAsync(int delta)
        {
            if (_busy || _cts.IsCancellationRequested)
                return;
            var target = _current + delta;
            if (target < 0)
                return;
            _busy = true;
            _timer.Stop();
            try
            {
                if (!_cache.ContainsKey(target))
                    _loading.Visibility = Visibility.Visible;
                var slide = await GetSlideAsync(target);
                if (slide is not { } value || _cts.IsCancellationRequested)
                    return;
                Show(value.Bitmap, value.Info);
                _current = target;
                // Render the next image while this one is on screen.
                _ = RenderAsync(target + 1);
            }
            catch (Exception ex)
            {
                _infoText.Text = ex.Message;
                _infoPanel.Visibility = Visibility.Visible;
            }
            finally
            {
                _loading.Visibility = Visibility.Collapsed;
                _busy = false;
                if (!_paused && !_cts.IsCancellationRequested)
                    _timer.Start();
            }
        }

        private void Show(BitmapImage bitmap, string info)
        {
            var incoming = _layers[1 - _activeLayer];
            var outgoing = _layers[_activeLayer];
            incoming.Source = bitmap;
            Canvas.SetZIndex(incoming, 1);
            Canvas.SetZIndex(outgoing, 0);
            _activeLayer = 1 - _activeLayer;
            _infoText.Text = info;
            _infoPanel.Visibility = _showInfo ? Visibility.Visible : Visibility.Collapsed;

            if (!_crossfade || outgoing.Source == null)
            {
                incoming.Opacity = 1;
                outgoing.Opacity = 0;
                return;
            }
            incoming.Opacity = 0;
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(900, _interval.TotalMilliseconds * 0.4))),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(animation, incoming);
            Storyboard.SetTargetProperty(animation, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Completed += (_, _) =>
            {
                if (_layers[_activeLayer] == incoming)
                    outgoing.Opacity = 0;
            };
            storyboard.Begin();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    Close();
                    break;
                case VirtualKey.Space:
                    TogglePause();
                    break;
                case VirtualKey.Right:
                case VirtualKey.PageDown:
                    _ = AdvanceAsync(1);
                    break;
                case VirtualKey.Left:
                case VirtualKey.PageUp:
                    _ = AdvanceAsync(-1);
                    break;
                case VirtualKey.I:
                    _showInfo = !_showInfo;
                    _infoPanel.Visibility = _showInfo && _current >= 0 ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case VirtualKey.F:
                case VirtualKey.F11:
                    ToggleFullScreen();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _pausedBadge.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
            if (_paused)
                _timer.Stop();
            else if (!_busy)
                _timer.Start();
        }

        private void ToggleFullScreen()
        {
            AppWindow.SetPresenter(IsFullScreen ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen);
            ShowCursorTemporarily();
        }

        private void ShowCursorTemporarily()
        {
            SetCursorHidden(false);
            _cursorTimer.Stop();
            _cursorTimer.Start();
        }

        /// <summary>Hides the mouse cursor in full screen; ShowCursor keeps a counter, so calls are balanced.</summary>
        private void SetCursorHidden(bool hidden)
        {
            if (hidden == _cursorHidden)
                return;
            _cursorHidden = hidden;
            ShowCursor(!hidden);
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _cts.Cancel();
            _timer.Stop();
            _hintTimer.Stop();
            _cursorTimer.Stop();
            SetCursorHidden(false);
            _cache.Clear();
        }

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool show);
    }
}
