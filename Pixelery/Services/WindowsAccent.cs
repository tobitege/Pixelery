using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace Pixelery.Services
{
    /// <summary>
    /// Accent color for borders: standard Fluent blue, or the Windows accent for other themes.
    /// <see cref="Brush"/> is shared so existing frames follow theme and system changes.
    /// </summary>
    public static class WindowsAccent
    {
        private static readonly UISettings Settings = new();
        private static DispatcherQueue? _dispatcher;

        public static SolidColorBrush Brush { get; } = new(ReadAccent());

        public static Color Color => Brush.Color;

        /// <summary>Raised on the UI thread after the accent color changed.</summary>
        public static event EventHandler? Changed;

        public static void Initialize(DispatcherQueue dispatcher)
        {
            if (_dispatcher != null)
                return;
            _dispatcher = dispatcher;
            // Raised on a background thread.
            Settings.ColorValuesChanged += (_, _) => _dispatcher.TryEnqueue(Refresh);
        }

        public static void Refresh()
        {
            var accent = FluentThemes.IsFluent(DaisyThemeManager.CurrentThemeName) && !AppSettings.UseWindowsAccent
                ? ((SolidColorBrush)DaisyResourceLookup.GetBrush("DaisyPrimaryBrush")).Color
                : ReadAccent();
            if (accent == Brush.Color)
                return;
            Brush.Color = accent;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static Color ReadAccent() => Settings.GetColorValue(UIColorType.Accent);
    }
}
