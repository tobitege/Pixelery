using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AbstractImg.Localization;
using AbstractImg.Services;
using IOPath = System.IO.Path;

namespace AbstractImg
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            InitializeComponent();
            ConfigureCrashLogging();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                Launch();
            }
            catch (Exception ex)
            {
                LogFatal("OnLaunched", ex);
                throw;
            }
        }

        private void Launch()
        {
            // AppResources includes XamlControlsResources and the native Flowery dictionary.
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("ms-appx:///AppResources.xaml")
            });
            DaisyResourceLookup.EnsureTokens();

            AppSettings.Initialize(GetStateDirectory());

            // Ensure the localization registers its resolver early.
            _ = AppLocalization.Instance;

            // Apply language BEFORE creating the window so all bindings start in the right culture.
            var savedLanguage = AppSettings.LoadLanguage();
            if (!string.IsNullOrWhiteSpace(savedLanguage))
            {
                FloweryLocalization.SetCulture(savedLanguage);
            }

            _window = new MainWindow();
            FlowerySizeManager.MainWindow = _window;
            FloweryScaleManager.MainWindow = _window;

            ApplyPersistedSettings();

            _window.Activate();
        }

        private static void ApplyPersistedSettings()
        {
            FloweryLocalization.CultureChanged += (_, cultureName) =>
                AppSettings.SaveLanguage(cultureName);

            var savedTheme = AppSettings.LoadTheme() ?? "Dark";
            if (!DaisyThemeManager.ApplyTheme(savedTheme))
            {
                DaisyThemeManager.ApplyTheme("Dark");
            }
            DaisyThemeManager.ThemeChanged += (_, name) =>
                AppSettings.SaveTheme(name);

            var savedSize = AppSettings.LoadGlobalSize();
            if (savedSize.HasValue)
            {
                FlowerySizeManager.ApplySize(savedSize.Value);
            }

            FlowerySizeManager.SizeChanged += (_, size) =>
                AppSettings.SaveGlobalSize(size);
        }

        private static string GetStateDirectory()
        {
            var configuredRoot = Environment.GetEnvironmentVariable("ABSTRACTIMG_STATE_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot) && IOPath.IsPathFullyQualified(configuredRoot))
                return IOPath.GetFullPath(configuredRoot);

            return IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AbstractImg");
        }

        private void ConfigureCrashLogging()
        {
            UnhandledException += (_, e) =>
            {
                try
                {
                    LogFatal("Application.UnhandledException", e.Exception);
                }
                finally
                {
                    e.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogFatal("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogFatal("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        private static void LogFatal(string source, Exception? ex)
        {
            var message = $"[{DateTimeOffset.Now:O}] {source}: {ex}\n";
            Debug.WriteLine(message);

            try
            {
                var dir = GetStateDirectory();
                Directory.CreateDirectory(dir);
                File.AppendAllText(IOPath.Combine(dir, "crash.log"), message);
            }
            catch
            {
                // Ignore file IO failures; Debug output is still useful.
            }
        }
    }
}
