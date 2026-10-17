using System.IO;
using Pixelery.Core.Generation;

namespace Pixelery.Services
{
    public enum StyleViewMode
    {
        Cards,
        PreviewList,
        TextList,
        TextOnly
    }

    /// <summary>
    /// Represents saved window position and size.
    /// </summary>
    public record WindowBounds(int X, int Y, int Width, int Height);

    /// <summary>
    /// Persisted app settings: theme, language, size and window via the Flowery state storage (like Flowery Photos),
    /// app options, and the generator configuration as JSON.
    /// </summary>
    public static class AppSettings
    {
        private const string ThemeKey = "theme";
        private const string LanguageKey = "language";
        private const string SizeKey = "size";
        private const string WindowKey = "window";
        private const string OptionsKey = "options";
        private const string GeneratorFile = "generator.json";
        private const string PaletteDirectoryKey = "palette-directory";

        public static readonly WindowBounds DefaultWindowBounds = new(80, 60, 1400, 900);

        public static string StateDirectory { get; private set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pixelery");

        /// <summary>The generator configuration edited on the main page.</summary>
        public static GeneratorSettings Generator { get; private set; } = new();

        public static void Initialize(string stateDirectory)
        {
            StateDirectory = stateDirectory;
            StateStorageProvider.Configure(new FileStateStorage(stateDirectory));
            LoadOptions();
            Generator = LoadGenerator();
        }

        private static string? LoadSingle(string key)
        {
            var lines = StateStorageProvider.Instance.LoadLines(key);
            var line = lines.Count > 0 ? lines[0] : null;
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }

        private static void SaveSingle(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            StateStorageProvider.Instance.SaveLines(key, (string[])[value]);
        }

        public static string? LoadTheme() => LoadSingle(ThemeKey);
        public static void SaveTheme(string themeName) => SaveSingle(ThemeKey, themeName);

        public static string? LoadLanguage() => LoadSingle(LanguageKey);
        public static void SaveLanguage(string cultureName) => SaveSingle(LanguageKey, cultureName);

        /// <summary>Last successful palette export folder, or the app's own palette directory.</summary>
        public static string PaletteDirectory
        {
            get
            {
                var saved = SanitizeDirectoryPath(LoadSingle(PaletteDirectoryKey));
                if (saved != null && Directory.Exists(saved))
                    return saved;

                // Only create the app-owned fallback, never a folder read from persisted settings.
                var root = SanitizeDirectoryPath(StateDirectory)
                    ?? throw new InvalidOperationException("The app data directory is invalid.");
                var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "Palettes"));
                if (System.IO.Path.GetRelativePath(root, folder) != "Palettes")
                    throw new InvalidOperationException("The palette folder must stay inside the app data directory.");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>Rejects malformed or relative paths and returns a canonical absolute directory path.</summary>
        private static string? SanitizeDirectoryPath(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return null;
            if (candidate.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
                return null;
            if (!System.IO.Path.IsPathFullyQualified(candidate))
                return null;
            try
            {
                return System.IO.Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        public static void RememberPaletteDirectory(string savedFile)
        {
            var directory = SanitizeDirectoryPath(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(savedFile)));
            if (directory != null && Directory.Exists(directory))
                SaveSingle(PaletteDirectoryKey, directory);
        }

        public static DaisySize? LoadGlobalSize()
        {
            var line = LoadSingle(SizeKey);
            return line != null && Enum.TryParse(line, ignoreCase: true, out DaisySize size) ? size : null;
        }

        public static void SaveGlobalSize(DaisySize size) => SaveSingle(SizeKey, size.ToString());

        public static WindowBounds? LoadWindowBounds()
        {
            var lines = StateStorageProvider.Instance.LoadLines(WindowKey);
            if (lines.Count < 4)
                return null;

            try
            {
                var x = int.Parse(lines[0].Trim(), CultureInfo.InvariantCulture);
                var y = int.Parse(lines[1].Trim(), CultureInfo.InvariantCulture);
                var width = int.Parse(lines[2].Trim(), CultureInfo.InvariantCulture);
                var height = int.Parse(lines[3].Trim(), CultureInfo.InvariantCulture);
                if (width < 100 || height < 100 || width > 10000 || height > 10000)
                    return null;
                return new WindowBounds(x, y, width, height);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveWindowBounds(WindowBounds bounds)
        {
            StateStorageProvider.Instance.SaveLines(WindowKey,
            (string[])[
                bounds.X.ToString(CultureInfo.InvariantCulture),
                bounds.Y.ToString(CultureInfo.InvariantCulture),
                bounds.Width.ToString(CultureInfo.InvariantCulture),
                bounds.Height.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        #region App options

        /// <summary>Shows a thumbnail of the latest image while generating.</summary>
        public static bool LivePreview { get; set; } = true;

        /// <summary>Minimum time between preview updates in milliseconds.</summary>
        public static int PreviewIntervalMs { get; set; } = 250;

        /// <summary>Opens the output folder in Explorer after a run finished.</summary>
        public static bool OpenFolderWhenDone { get; set; }

        /// <summary>Renders the style previews with the enabled palettes and tones.</summary>
        public static bool PreviewsFollowColors { get; set; } = true;

        /// <summary>Layout of the style selection, independent of generation settings.</summary>
        public static StyleViewMode StyleView { get; set; } = StyleViewMode.PreviewList;

        /// <summary>Seconds between two slideshow images.</summary>
        public static int SlideshowSeconds { get; set; } = 8;

        /// <summary>Slideshow in full screen (true) or in a window.</summary>
        public static bool SlideshowFullScreen { get; set; } = true;

        /// <summary>Cross-fade between slideshow images.</summary>
        public static bool SlideshowCrossfade { get; set; } = true;

        /// <summary>Show style, palette and seed of the current slideshow image.</summary>
        public static bool SlideshowShowInfo { get; set; }

        /// <summary>Slideshow seed; empty means a new random sequence every time.</summary>
        public static string SlideshowSeed { get; set; } = string.Empty;

        private static void LoadOptions()
        {
            foreach (var line in StateStorageProvider.Instance.LoadLines(OptionsKey))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;
                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();
                switch (key)
                {
                    case nameof(LivePreview) when bool.TryParse(value, out var b):
                        LivePreview = b;
                        break;
                    case nameof(PreviewIntervalMs) when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i):
                        PreviewIntervalMs = Math.Clamp(i, 50, 5000);
                        break;
                    case nameof(OpenFolderWhenDone) when bool.TryParse(value, out var b):
                        OpenFolderWhenDone = b;
                        break;
                    case nameof(PreviewsFollowColors) when bool.TryParse(value, out var b):
                        PreviewsFollowColors = b;
                        break;
                    case nameof(StyleView) when Enum.TryParse<StyleViewMode>(value, out var mode) && Enum.IsDefined(mode):
                        StyleView = mode;
                        break;
                    case nameof(SlideshowSeconds) when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i):
                        SlideshowSeconds = Math.Clamp(i, 1, 3600);
                        break;
                    case nameof(SlideshowFullScreen) when bool.TryParse(value, out var b):
                        SlideshowFullScreen = b;
                        break;
                    case nameof(SlideshowCrossfade) when bool.TryParse(value, out var b):
                        SlideshowCrossfade = b;
                        break;
                    case nameof(SlideshowShowInfo) when bool.TryParse(value, out var b):
                        SlideshowShowInfo = b;
                        break;
                    case nameof(SlideshowSeed):
                        SlideshowSeed = value;
                        break;
                }
            }
        }

        public static void SaveOptions()
        {
            StateStorageProvider.Instance.SaveLines(OptionsKey,
            (string[])[
                $"{nameof(LivePreview)}={LivePreview}",
                $"{nameof(PreviewIntervalMs)}={PreviewIntervalMs.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(OpenFolderWhenDone)}={OpenFolderWhenDone}",
                $"{nameof(PreviewsFollowColors)}={PreviewsFollowColors}",
                $"{nameof(StyleView)}={StyleView}",
                $"{nameof(SlideshowSeconds)}={SlideshowSeconds.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(SlideshowFullScreen)}={SlideshowFullScreen}",
                $"{nameof(SlideshowCrossfade)}={SlideshowCrossfade}",
                $"{nameof(SlideshowShowInfo)}={SlideshowShowInfo}",
                $"{nameof(SlideshowSeed)}={SlideshowSeed}"
            ]);
        }

        #endregion

        #region Generator settings

        private static GeneratorSettings LoadGenerator()
        {
            try
            {
                var path = System.IO.Path.Combine(StateDirectory, GeneratorFile);
                if (File.Exists(path))
                {
                    return SettingsJson.Load(path);
                }
            }
            catch
            {
                // Corrupt or outdated file: start with defaults.
            }
            return new GeneratorSettings();
        }

        public static void SaveGenerator()
        {
            try
            {
                SettingsJson.Save(System.IO.Path.Combine(StateDirectory, GeneratorFile), Generator);
            }
            catch
            {
                // Saving settings must never break the app.
            }
        }

        #endregion
    }
}
