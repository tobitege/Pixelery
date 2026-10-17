using Pixelery.Core.Styles;
using Pixelery.Localization;
using Pixelery.Services;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>
    /// Side panel tabs ("Generate" and "Slideshow") and the shared start button below them.
    /// The slideshow uses the styles and colors of the configuration but saves nothing.
    /// </summary>
    public sealed partial class GeneratorView
    {
        private SlideshowWindow? _slideshow;
        private TextBlock? _slideshowSummary;

        private bool IsSlideshowTab => SideTabs.SelectedIndex == 1;

        private void InitializeSideTabs()
        {
            BuildSlideshowTab();
            // The pages are declared in a hidden container; detach them before handing them to the tabs.
            TabPages.Children.Clear();
            SideTabs.AddTab(L("Tab_Generate"), GeneratePage);
            SideTabs.AddTab(L("Tab_Slideshow"), SlideshowPage);
            // Tabs added after DaisyTabs loaded are not selected automatically.
            SideTabs.SelectedIndex = 1;
            SideTabs.SelectedIndex = 0;
            SideTabs.RegisterPropertyChangedCallback(DaisyTabs.SelectedIndexProperty, (_, _) => UpdateStartButton());
            _localizers.Add(UpdateTabHeaders);
        }

        /// <summary>DaisyTabs keeps tab headers as plain text; rebuild the tabs with the same pages to translate them.</summary>
        private void UpdateTabHeaders()
        {
            var items = SideTabs.Items;
            if (items.Length == 2 && items[0].Header == L("Tab_Generate") && items[1].Header == L("Tab_Slideshow"))
                return;
            var selected = SideTabs.SelectedIndex;
            SideTabs.ClearTabs();
            SideTabs.AddTab(L("Tab_Generate"), GeneratePage);
            SideTabs.AddTab(L("Tab_Slideshow"), SlideshowPage);
            // Select another tab and back so the content presenter shows the page again.
            SideTabs.SelectedIndex = 1 - selected;
            SideTabs.SelectedIndex = selected;
        }

        private void BuildSlideshowTab()
        {
            var intro = Text(string.Empty, ResponsiveFontTier.Secondary, 0.7);
            Localize(() => intro.Text = L("Slide_Intro"));
            SlideshowStack.Children.Add(intro);

            // Interval
            var seconds = Number(1, 3600, AppSettings.SlideshowSeconds, v =>
            {
                AppSettings.SlideshowSeconds = v;
                AppSettings.SaveOptions();
            }, 140);
            var secondsLabel = Text(string.Empty, ResponsiveFontTier.Primary, 0.8);
            Localize(() => secondsLabel.Text = L("Slide_Seconds"));
            var intervalRow = new WrapPanel { HorizontalSpacing = 10, VerticalSpacing = 8 };
            intervalRow.Children.Add(seconds);
            intervalRow.Children.Add(secondsLabel);
            SlideshowStack.Children.Add(Block("Slide_Interval", intervalRow));

            // Display mode (single choice)
            var modes = new WrapPanel();
            modes.Children.Add(Chip(() => L("Slide_FullScreen"), () => AppSettings.SlideshowFullScreen, () => SetMode(true)));
            modes.Children.Add(Chip(() => L("Slide_Window"), () => !AppSettings.SlideshowFullScreen, () => SetMode(false)));
            SlideshowStack.Children.Add(Block("Slide_Display", modes));

            void SetMode(bool full)
            {
                AppSettings.SlideshowFullScreen = full;
                AppSettings.SaveOptions();
                RefreshChipVisuals();
            }

            // Options
            var options = new StackPanel { Spacing = 10 };
            options.Children.Add(LToggleRow("Slide_Crossfade", AppSettings.SlideshowCrossfade, v =>
            {
                AppSettings.SlideshowCrossfade = v;
                AppSettings.SaveOptions();
            }));
            options.Children.Add(LToggleRow("Slide_ShowInfo", AppSettings.SlideshowShowInfo, v =>
            {
                AppSettings.SlideshowShowInfo = v;
                AppSettings.SaveOptions();
            }));
            SlideshowStack.Children.Add(Block("Slide_Options", options));

            // Seed
            var seed = new DaisyInput { Text = AppSettings.SlideshowSeed, Variant = DaisyInputVariant.Bordered };
            Localize(() => seed.PlaceholderText = L("Run_SeedPlaceholder"));
            seed.TextChanged += (_, _) =>
            {
                AppSettings.SlideshowSeed = seed.Text?.Trim() ?? string.Empty;
                AppSettings.SaveOptions();
            };
            var dice = new DaisyButton { Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square, IconData = Icon("AppIconDice") };
            LocalizeTip(dice, "Run_SeedDice");
            dice.Click += (_, _) => seed.Text = Random.Shared.Next(1, int.MaxValue).ToString(CultureInfo.InvariantCulture);
            var seedRow = new Grid { ColumnSpacing = 6 };
            seedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            seedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(dice, 1);
            seedRow.Children.Add(seed);
            seedRow.Children.Add(dice);
            SlideshowStack.Children.Add(Block("Run_Seed", seedRow, "Slide_SeedHint"));

            // What will be shown
            _slideshowSummary = Text(string.Empty, ResponsiveFontTier.Secondary);
            SlideshowStack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush"),
                Child = _slideshowSummary
            });

            // Keyboard
            var keys = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            keys.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keys.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            (string Keys, string TextKey)[] shortcuts =
            [
                ("Esc", "Slide_KeyExit"),
                ("Space", "Slide_KeyPause"),
                ("← →", "Slide_KeyNav"),
                ("I", "Slide_KeyInfo"),
                ("F", "Slide_KeyFull")
            ];
            for (var i = 0; i < shortcuts.Length; i++)
            {
                keys.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var kbd = new DaisyKbd { Text = shortcuts[i].Keys, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                var key = shortcuts[i].TextKey;
                var label = Text(string.Empty, ResponsiveFontTier.Secondary, 0.8);
                Localize(() => label.Text = L(key));
                Grid.SetRow(kbd, i);
                Grid.SetRow(label, i);
                Grid.SetColumn(label, 1);
                keys.Children.Add(kbd);
                keys.Children.Add(label);
            }
            SlideshowStack.Children.Add(Block("Slide_Keys", keys));
        }

        /// <summary>Problems that keep the slideshow from starting (the output options do not matter here).</summary>
        private static List<string> SlideshowProblems() =>
            [.. Settings.Validate().Where(p => p is "Validation_Styles" or "Validation_Palettes" or "Validation_Tones" or "Validation_Details")];

        private void UpdateSlideshowSummary()
        {
            if (_slideshowSummary == null)
                return;
            var problems = SlideshowProblems();
            _slideshowSummary.Text = problems.Count > 0
                ? string.Join(Environment.NewLine, problems.Select(L))
                : Settings.UsePaletteSet
                    ? AppLocalization.Format("Colors_SetSummary", Settings.PaletteSet.Entries.Count)
                    : AppLocalization.Format("Slide_Uses", Settings.Styles.Count(id => StyleCatalog.Find(id) != null), Settings.Palettes.Count, Settings.Tones.Count);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e) => StartFromButton();

        /// <summary>What the big start button does: start the slideshow, start a run or cancel the running one.</summary>
        public void StartFromButton()
        {
            if (!_initialized)
                return;
            if (IsSlideshowTab)
                StartSlideshow();
            else if (IsRunning)
                CancelRun();
            else
                StartRun();
        }

        /// <summary>Start (or cancel) depending on the selected tab and the run state.</summary>
        private void UpdateStartButton()
        {
            if (!_initialized)
                return;
            if (!IsSlideshowTab && IsRunning)
            {
                StartButton.Variant = DaisyButtonVariant.Error;
                StartButton.IconData = Icon("AppIconStop");
                StartButton.Content = L("Run_Cancel");
                StartButton.IsEnabled = _runCts is { IsCancellationRequested: false };
                return;
            }
            StartButton.Variant = DaisyButtonVariant.Primary;
            StartButton.IconData = Icon("AppIconPlay");
            StartButton.Content = L("Side_Start");
            StartButton.IsEnabled = IsSlideshowTab ? SlideshowProblems().Count == 0 : Settings.Validate().Count == 0;
        }

        public void StartSlideshow()
        {
            if (SlideshowProblems().Count > 0)
                return;
            _slideshow?.Close();
            var window = new SlideshowWindow(Settings, AppSettings.SlideshowSeed, TimeSpan.FromSeconds(AppSettings.SlideshowSeconds),
                AppSettings.SlideshowFullScreen, AppSettings.SlideshowCrossfade, AppSettings.SlideshowShowInfo);
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_slideshow, window))
                    _slideshow = null;
            };
            _slideshow = window;
            window.Activate();
        }
    }
}
