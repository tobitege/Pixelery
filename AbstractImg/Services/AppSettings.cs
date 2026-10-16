using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AbstractImg.Core.Generation;

namespace AbstractImg.Services
{
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

        public static readonly WindowBounds DefaultWindowBounds = new(80, 60, 1400, 900);

        public static string StateDirectory { get; private set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AbstractImg");

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
                $"{nameof(PreviewsFollowColors)}={PreviewsFollowColors}"
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
                    var settings = JsonSerializer.Deserialize(File.ReadAllText(path), AppJsonContext.Default.GeneratorSettings);
                    if (settings != null)
                        return settings;
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
                Directory.CreateDirectory(StateDirectory);
                File.WriteAllText(System.IO.Path.Combine(StateDirectory, GeneratorFile),
                    JsonSerializer.Serialize(Generator, AppJsonContext.Default.GeneratorSettings));
            }
            catch
            {
                // Saving settings must never break the app.
            }
        }

        #endregion
    }

    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(GeneratorSettings))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
