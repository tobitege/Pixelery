using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixelery.Localization
{
    /// <summary>
    /// Provides JSON-based localization services for Pixelery.
    /// Mirrors the Gallery localization pattern: bind with {Binding [Key]} and DataContext = Localization.
    /// </summary>
    [Microsoft.UI.Xaml.Data.Bindable]
    [WinRT.GeneratedBindableCustomProperty]
    public partial class AppLocalization : INotifyPropertyChanged
    {
        private static string _currentCultureName = "en";
        private static readonly Dictionary<string, Dictionary<string, string>> _translations = [];
        private static readonly Lazy<AppLocalization> _instance = new(() => new AppLocalization());

        public static AppLocalization Instance => _instance.Value;
        public static event EventHandler<string>? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        static AppLocalization()
        {
            foreach (var lang in FloweryLocalization.SupportedLanguages)
                LoadTranslation(lang);

            FloweryLocalization.CultureChanged += OnFloweryCultureChanged;

            // Register the app's localization as the custom resolver for Flowery controls.
            FloweryLocalization.CustomResolver = GetString;

            SetCulture(FloweryLocalization.CurrentCultureName);
        }

        private static void OnFloweryCultureChanged(object? sender, string cultureName)
        {
            SetCulture(cultureName);
        }

        public static string CurrentCultureName => _currentCultureName;

        /// <summary>
        /// Indexer for XAML binding: {Binding [KeyName]}
        /// </summary>
        public string this[string key] => GetString(key);

        public static CultureInfo CurrentCulture
        {
            get
            {
                try
                {
                    return CultureInfo.GetCultureInfo(_currentCultureName);
                }
                catch (CultureNotFoundException)
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }

        public static void SetCulture(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
                return;

            if (string.Equals(_currentCultureName, cultureName, StringComparison.OrdinalIgnoreCase))
                return;

            _currentCultureName = cultureName;

            CultureChanged?.Invoke(null, cultureName);
            NotifyPropertyChangedOnUIThread();
        }

        private static void NotifyPropertyChangedOnUIThread()
        {
            var instance = Instance;
            var handler = instance.PropertyChanged;
            if (handler == null)
                return;

            void RaiseNotifications()
            {
                handler.Invoke(instance, new PropertyChangedEventArgs("Item"));
                handler.Invoke(instance, new PropertyChangedEventArgs("Item[]"));
            }

            try
            {
                var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                    ?? FlowerySizeManager.MainWindow?.DispatcherQueue;
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(RaiseNotifications);
                }
                else
                {
                    RaiseNotifications();
                }
            }
            catch
            {
                RaiseNotifications();
            }
        }

        public static string GetString(string key)
        {
            try
            {
                if (_translations.TryGetValue(_currentCultureName, out var exactDict) &&
                    exactDict.TryGetValue(key, out var exactValue))
                {
                    return exactValue;
                }

                var dashIndex = _currentCultureName.IndexOf('-');
                if (dashIndex > 0)
                {
                    var twoLetter = _currentCultureName[..dashIndex];
                    if (_translations.TryGetValue(twoLetter, out var langDict) &&
                        langDict.TryGetValue(key, out var langValue))
                    {
                        return langValue;
                    }
                }

                if (_translations.TryGetValue("en", out var enDict) &&
                    enDict.TryGetValue(key, out var enValue))
                {
                    return enValue;
                }

                return FloweryLocalization.GetStringInternal(key);
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// Resolves a key and formats it with the current culture.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(CurrentCulture, GetString(key), args);
            }
            catch (FormatException)
            {
                return GetString(key);
            }
        }

        private static void LoadTranslation(string languageCode)
        {
            try
            {
                var assembly = typeof(AppLocalization).Assembly;
                var resourceName = $"{assembly.GetName().Name}.Localization.{languageCode}.json";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return;

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var dict = JsonSerializer.Deserialize(json, AppLocalizationJsonContext.Default.DictionaryStringString);
                if (dict != null)
                    _translations[languageCode] = dict;
            }
            catch
            {
                // Fallback to English will be used.
            }
        }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class AppLocalizationJsonContext : JsonSerializerContext
    {
    }
}
