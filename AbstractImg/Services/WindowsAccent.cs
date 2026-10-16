using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace AbstractImg.Services
{
    /// <summary>
    /// Windows accent color for borders (window frame, hover, focus and selection frames).
    /// <see cref="Brush"/> is one shared instance whose color follows the system setting, so elements using it update live.
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
            Settings.ColorValuesChanged += (_, _) => _dispatcher.TryEnqueue(() =>
            {
                var accent = ReadAccent();
                if (accent == Brush.Color)
                    return;
                Brush.Color = accent;
                Changed?.Invoke(null, EventArgs.Empty);
            });
        }

        private static Color ReadAccent() => Settings.GetColorValue(UIColorType.Accent);
    }
}
