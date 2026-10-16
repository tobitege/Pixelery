using System.IO;
using AbstractImg.Localization;
using AbstractImg.Services;
using Windows.System;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>
    /// App settings: appearance (theme, size, language), generation options, shortcuts and about.
    /// Changes preview live; the shell restores the snapshot on Cancel.
    /// </summary>
    public sealed partial class SettingsView : UserControl
    {
        private AppShell? _shell;
        private bool _updatingLanguage;
        private SettingsSnapshot? _snapshot;

        public SettingsView()
        {
            InitializeComponent();
            LanguageSelect.SelectionChanged += LanguageSelect_SelectionChanged;
            AppLocalization.Instance.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
            FlowerySizeManager.SizeChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                ApplyScale();
                Refresh();
            });
            Loaded += (_, _) => ApplyScale();
            var version = typeof(SettingsView).Assembly.GetName().Version;
            VersionText.Text = version == null ? string.Empty : $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        public AppLocalization Localization => AppLocalization.Instance;

        /// <summary>Hosted inside the settings dialog: the dialog shows the title, the page scrolls without outer padding.</summary>
        public void SetDialogMode() => RootScroller.Padding = new Thickness(0, 0, 14, 0);

        public void Initialize(AppShell shell)
        {
            _shell = shell;
            Refresh();
        }

        /// <summary>Starts an editing session; changes preview live until <see cref="CommitEdit"/> or <see cref="CancelEdit"/>.</summary>
        public void BeginEdit()
        {
            _snapshot = SettingsSnapshot.Capture();
            Refresh();
            ApplyScale();
            RootScroller.ChangeView(null, 0, null, true);
        }

        public void CommitEdit() => _snapshot = null;

        public void CancelEdit()
        {
            var snapshot = _snapshot;
            _snapshot = null;
            if (snapshot == null)
                return;
            var previewsChanged = snapshot.PreviewsFollowColors != AppSettings.PreviewsFollowColors;
            snapshot.Restore();
            if (previewsChanged)
                _shell?.OnPreviewOptionChanged();
        }

        private void ApplyScale()
        {
            var f = SizeFactor;
            var size = FlowerySizeManager.CurrentSize;
            ThemeDropdown.Size = size;
            SizeDropdown.Size = size;
            LanguageSelect.Size = size;
            foreach (var button in new[] { FloweryButton, DataFolderButton })
                button.Size = size;
            ThemeDropdown.Width = 240 * f;
            SizeDropdown.Width = 240 * f;
            LanguageSelect.Width = 240 * f;
            AboutIcon.Width = AboutIcon.Height = 56 * f;
            PageStack.Spacing = 16 * f;
            PageStack.MaxWidth = 860 * f;
        }

        public void Refresh()
        {
            BuildLanguageSelect();
            BuildGenerationOptions();
            BuildShortcuts();
        }

        private void BuildLanguageSelect()
        {
            _updatingLanguage = true;
            try
            {
                List<SelectOption> options = [];
                foreach (var code in FloweryLocalization.SupportedLanguages)
                {
                    var name = FloweryLocalization.LanguageDisplayNames.TryGetValue(code, out var display) ? display : code;
                    options.Add(new SelectOption(code, name));
                }
                LanguageSelect.ItemsSource = options;
                var current = FloweryLocalization.CurrentCultureName;
                LanguageSelect.SelectedItem = options.FirstOrDefault(o => string.Equals((string)o.Key, current, StringComparison.OrdinalIgnoreCase))
                    ?? options.FirstOrDefault(o => current.StartsWith((string)o.Key, StringComparison.OrdinalIgnoreCase))
                    ?? options.FirstOrDefault();
            }
            finally
            {
                _updatingLanguage = false;
            }
        }

        private void LanguageSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingLanguage || LanguageSelect.SelectedItem is not SelectOption { Key: string code })
                return;
            if (string.Equals(code, FloweryLocalization.CurrentCultureName, StringComparison.OrdinalIgnoreCase))
                return;
            DispatcherQueue.TryEnqueue(() => FloweryLocalization.SetCulture(code));
        }

        private void BuildGenerationOptions()
        {
            GenerationOptions.Children.Clear();
            GenerationOptions.Children.Add(ToggleRow("Settings_LivePreview", AppSettings.LivePreview, v =>
            {
                AppSettings.LivePreview = v;
                AppSettings.SaveOptions();
            }));
            GenerationOptions.Children.Add(LabeledRange("Settings_PreviewInterval", 50, 2000, AppSettings.PreviewIntervalMs, 50,
                v => $"{v:0} ms",
                v =>
                {
                    AppSettings.PreviewIntervalMs = (int)Math.Round(v);
                    AppSettings.SaveOptions();
                }));
            GenerationOptions.Children.Add(ToggleRow("Settings_PreviewsFollowColors", AppSettings.PreviewsFollowColors, v =>
            {
                AppSettings.PreviewsFollowColors = v;
                AppSettings.SaveOptions();
                _shell?.OnPreviewOptionChanged();
            }));
            GenerationOptions.Children.Add(ToggleRow("Settings_OpenFolderWhenDone", AppSettings.OpenFolderWhenDone, v =>
            {
                AppSettings.OpenFolderWhenDone = v;
                AppSettings.SaveOptions();
            }));
        }

        private static Grid LabeledRange(string key, double min, double max, double value, double step, Func<double, string> format, Action<double> onChanged)
        {
            var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = Responsive(new TextBlock { Text = L(key), Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") }, ResponsiveFontTier.Primary);
            var valueText = Responsive(new TextBlock { Text = format(value), Opacity = 0.7, Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") }, ResponsiveFontTier.Primary);
            var range = Range(min, max, value, step, v =>
            {
                valueText.Text = format(v);
                onChanged(v);
            });
            range.Size = FlowerySizeManager.CurrentSize;
            Grid.SetColumn(valueText, 1);
            Grid.SetRow(range, 1);
            Grid.SetColumnSpan(range, 2);
            grid.Children.Add(label);
            grid.Children.Add(valueText);
            grid.Children.Add(range);
            return grid;
        }

        private void BuildShortcuts()
        {
            (string Keys, string TextKey)[] shortcuts =
            [
                ("Ctrl+Enter", "Shortcut_Start"),
                ("Esc", "Shortcut_Cancel"),
                ("Ctrl+,", "Shortcut_Settings"),
                ("Space", "Shortcut_ToggleStyle")
            ];

            ShortcutsGrid.Children.Clear();
            ShortcutsGrid.RowDefinitions.Clear();
            ShortcutsGrid.ColumnDefinitions.Clear();
            ShortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ShortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < shortcuts.Length; i++)
            {
                ShortcutsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var kbd = new DaisyKbd { Text = shortcuts[i].Keys, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                var text = Text(L(shortcuts[i].TextKey));
                Grid.SetRow(kbd, i);
                Grid.SetRow(text, i);
                Grid.SetColumn(text, 1);
                ShortcutsGrid.Children.Add(kbd);
                ShortcutsGrid.Children.Add(text);
            }
        }

        private void FloweryButton_Click(object sender, RoutedEventArgs e) =>
            _ = Launcher.LaunchUriAsync(new Uri("https://github.com/tobitege/Flowery.WinUI"));

        private void DataFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppSettings.StateDirectory);
            _ = Launcher.LaunchFolderPathAsync(AppSettings.StateDirectory);
        }
    }
}
