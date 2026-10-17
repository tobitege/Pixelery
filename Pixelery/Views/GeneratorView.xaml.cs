using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Generation;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Localization;
using Pixelery.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>
    /// Main page: configuration sections on the left (styles, colors, dimensions, output) and the run panel on the right.
    /// Every change is written to <see cref="AppSettings.Generator"/> and saved shortly after.
    /// </summary>
    public sealed partial class GeneratorView : UserControl
    {
        private static readonly string[] HeroStyles = ["mesh-gradient", "aurora", "marble", "plasma", "flow-field", "paper-layers", "nebula", "stained-glass"];

        private readonly List<Action> _localizers = [];
        private AppShell? _shell;
        private DispatcherQueueTimer? _saveTimer;
        private CancellationTokenSource? _heroCts;
        private bool _initialized;

        public GeneratorView()
        {
            InitializeComponent();
            AppLocalization.Instance.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(OnLanguageChanged);
            DaisyThemeManager.ThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(OnThemeChanged);
            FlowerySizeManager.SizeChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyScale);
            Hero.SizeChanged += (_, e) =>
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 120)
                    _ = RenderHeroAsync(false);
            };
            RootGrid.SizeChanged += (_, e) => RunPanel.Width = e.NewSize.Width < 1100 ? 300 : 360;
        }

        public AppLocalization Localization => AppLocalization.Instance;

        private static GeneratorSettings Settings => AppSettings.Generator;

        public bool IsRunning => _runCts != null;

        public void Initialize(AppShell shell)
        {
            _shell = shell;
            StartButton.IsEnabled = false;
            AddPlaceholders();
            FooterText.Text = $"Pixelery {Core.AppInfo.Version(typeof(GeneratorView).Assembly)} · {Core.AppInfo.Copyright} · {Core.AppInfo.License} ·";
            ToolTipService.SetToolTip(FooterLink, Core.AppInfo.RepositoryUrl);
            UpdateHeroFade();
            HeroBadgeUpdate();
            Loaded += (_, _) => _ = RenderHeroAsync(true);
            _buildSteps = BuildSteps().GetEnumerator();
            CompositionTarget.Rendering += OnBuildFrame;
        }

        private IEnumerator<Action>? _buildSteps;
        private bool _firstFrameShown;

        /// <summary>Runs one build step per frame; the very first frame shows the empty shell.</summary>
        private void OnBuildFrame(object? sender, object e)
        {
            if (!_firstFrameShown)
            {
                _firstFrameShown = true;
                return;
            }
            if (_buildSteps != null && _buildSteps.MoveNext())
            {
                _buildSteps.Current();
                return;
            }
            CompositionTarget.Rendering -= OnBuildFrame;
            _buildSteps = null;
            _initialized = true;
            UpdateSummary();
            UpdateNamePreview();
            ApplyScale();
        }

        /// <summary>Registers a text update that runs now and again after every language change.</summary>
        private void Localize(Action apply)
        {
            apply();
            _localizers.Add(apply);
        }

        private void OnLanguageChanged()
        {
            foreach (var apply in _localizers)
                apply();
            foreach (var card in _cards)
                card.UpdateTexts();
            UpdateSummary();
            UpdateNamePreview();
            UpdateProgressTexts();
        }

        private void OnThemeChanged()
        {
            UpdateHeroFade();
            foreach (var card in _cards)
                card.UpdateVisual();
            RefreshChipVisuals();
            UpdatePaletteSamples();
        }

        private void ApplyScale()
        {
            var f = SizeFactor;
            StatsGrid.MinItemWidth = 110 * f;
            StatsGrid.InvalidateMeasure();
            HeroTitle.FontSize = 34 * f;
            ConfigStack.Spacing = 20 * f;
            if (_styleGrids.Count > 0)
            {
                foreach (var grid in _styleGrids)
                {
                    grid.MinItemWidth = 220 * f;
                    grid.InvalidateMeasure();
                }
            }
            foreach (var card in _cards)
                card.ApplyViewMode(AppSettings.StyleView);
        }

        /// <summary>Marks the settings as changed: refreshes the summary and saves after a short delay.</summary>
        private void OnSettingsChanged()
        {
            if (!_initialized)
                return;
            UpdateSummary();
            if (_saveTimer == null)
            {
                _saveTimer = DispatcherQueue.CreateTimer();
                _saveTimer.Interval = TimeSpan.FromMilliseconds(600);
                _saveTimer.IsRepeating = false;
                _saveTimer.Tick += (_, _) => AppSettings.SaveGenerator();
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void Shutdown()
        {
            _runCts?.Cancel();
            _previewCts?.Cancel();
            _slideshow?.Close();
            AppSettings.SaveGenerator();
        }

        private void FooterLink_Click(object sender, RoutedEventArgs e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(Core.AppInfo.RepositoryUrl));

        #region Hero

        private void HeroShuffleButton_Click(object sender, RoutedEventArgs e) => _ = RenderHeroAsync(true);

        private void UpdateHeroFade()
        {
            var surface = DaisyResourceLookup.GetBrush("DaisyBase200Brush") is SolidColorBrush b ? b.Color : Colors.Black;
            HeroFade.Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = surface, Offset = 0.32 },
                    new GradientStop { Color = Color.FromArgb(200, surface.R, surface.G, surface.B), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(0, surface.R, surface.G, surface.B), Offset = 0.85 }
                }
            };
        }

        private int _heroSeed = Random.Shared.Next();

        private async Task RenderHeroAsync(bool newSeed)
        {
            if (Hero.ActualWidth < 50 || Hero.ActualHeight < 50)
                return;
            if (newSeed)
                _heroSeed = Random.Shared.Next();
            _heroCts?.Cancel();
            var cts = new CancellationTokenSource();
            _heroCts = cts;
            var scale = XamlRoot?.RasterizationScale ?? 1;
            var width = (int)(Hero.ActualWidth * 0.7 * scale);
            var height = (int)(Hero.ActualHeight * scale);
            var rng = new Random(_heroSeed);
            var style = StyleCatalog.Find(HeroStyles[rng.Next(HeroStyles.Length)])!;
            HeroImage.Width = Hero.ActualWidth * 0.7;
            try
            {
                var heroSettings = new GeneratorSettings
                {
                    Palettes = [PaletteFamily.Curated, PaletteFamily.Cosine, PaletteFamily.Neon],
                    Tones = [BackgroundTone.Dark, BackgroundTone.Vivid],
                    Grain = true
                };
                var bytes = await PreviewRenderer.RenderAsync(style, rng.Next(), heroSettings, true, width, height, cts.Token);
                if (cts.IsCancellationRequested)
                    return;
                var bitmap = await PreviewRenderer.ToBitmapAsync(bytes);
                if (cts.IsCancellationRequested)
                    return;
                HeroImageBrush.ImageSource = bitmap;
                HeroImage.Opacity = 1;
                ToolTipService.SetToolTip(HeroShuffleButton, $"{L("Hero_Shuffle")} · {style.Name}");
            }
            catch (OperationCanceledException)
            {
                // A newer render replaced this one.
            }
        }

        #endregion

        #region Summary & validation

        private void UpdateSummary()
        {
            if (!_initialized)
                return;

            var sizes = Settings.BuildSizePool();
            var styles = Settings.Styles.Count(id => StyleCatalog.Find(id) != null);
            SummaryPanel.Children.Clear();
            SummaryPanel.Children.Add(SummaryLine("AppIconBrush", AppLocalization.Format("Summary_Styles", styles, StyleCatalog.All.Count)));
            SummaryPanel.Children.Add(SummaryLine("AppIconPalette", Settings.UsePaletteSet
                ? AppLocalization.Format("Colors_SetSummary", Settings.PaletteSet.Entries.Count)
                : AppLocalization.Format("Summary_Palettes", Settings.Palettes.Count, Settings.Tones.Count)));
            if (sizes.Count > 0)
            {
                var minMp = sizes.Min(s => s.Megapixels);
                var maxMp = sizes.Max(s => s.Megapixels);
                SummaryPanel.Children.Add(SummaryLine("AppIconRuler", AppLocalization.Format("Summary_Sizes", sizes.Count, minMp, maxMp)));
            }
            if (Settings.Formats.Count > 0)
            {
                var formats = string.Join(", ", Settings.Formats.Select(FormatName));
                SummaryPanel.Children.Add(SummaryLine("AppIconLandscape", formats));
                var estimate = EstimateBytes(sizes);
                if (estimate > 0)
                    SummaryPanel.Children.Add(SummaryLine("AppIconBolt", AppLocalization.Format("Summary_Estimate", FormatBytes(estimate))));
            }

            ValidationPanel.Children.Clear();
            var problems = Settings.Validate();
            foreach (var key in problems)
                ValidationPanel.Children.Add(new DaisyAlert { Variant = DaisyAlertVariant.Warning, Message = L(key) });
            UpdateStartButton();
            UpdateSlideshowSummary();
            HeroBadgeUpdate();
        }

        /// <summary>Rough output size: average megapixels × typical bytes per megapixel of the chosen formats.</summary>
        private static long EstimateBytes(IReadOnlyList<SizeChoice> sizes)
        {
            if (sizes.Count == 0 || Settings.Formats.Count == 0)
                return 0;
            var avgMp = sizes.Average(s => s.Megapixels);
            var qualityFactor = 0.4 + Settings.Quality / 100.0;
            var perMp = Settings.Formats.Average(f => f switch
            {
                OutputFormat.Png => 1_300_000.0,
                OutputFormat.Webp => 80_000.0 * qualityFactor,
                _ => 160_000.0 * qualityFactor
            });
            return (long)(avgMp * perMp * Settings.Count);
        }

        public static string FormatName(OutputFormat format) => format switch
        {
            OutputFormat.Png => "PNG",
            OutputFormat.Webp => "WebP",
            _ => "JPEG"
        };

        public static string FormatBytes(double bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            var unit = 0;
            while (bytes >= 1024 && unit < units.Length - 1)
            {
                bytes /= 1024;
                unit++;
            }
            return string.Format(AppLocalization.CurrentCulture, unit == 0 ? "{0:0} {1}" : "{0:0.0} {1}", bytes, units[unit]);
        }

        private static Grid SummaryLine(string iconKey, string text)
        {
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new Viewbox
            {
                Width = 16 * SizeFactor,
                Height = 16 * SizeFactor,
                Opacity = 0.65,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Path { Data = ToGeometry(Icon(iconKey)!), Fill = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") }
            };
            var label = Text(text, ResponsiveFontTier.Secondary);
            Grid.SetColumn(label, 1);
            grid.Children.Add(icon);
            grid.Children.Add(label);
            return grid;
        }

        private void HeroBadgeUpdate()
        {
            HeroBadges.Children.Clear();
            HeroBadges.Children.Add(new DaisyBadge { Variant = DaisyBadgeVariant.Primary, Content = AppLocalization.Format("Hero_BadgeStyles", StyleCatalog.All.Count) });
            HeroBadges.Children.Add(new DaisyBadge { Variant = DaisyBadgeVariant.Secondary, Content = AppLocalization.Format("Hero_BadgePalettes", Enum.GetValues<PaletteFamily>().Length) });
            HeroBadges.Children.Add(new DaisyBadge { Variant = DaisyBadgeVariant.Accent, Content = L("Hero_BadgeSeeds") });
        }

        #endregion

        /// <summary>Sets tooltip and accessible name for an icon-only button, also after language changes.</summary>
        private void LocalizeTip(FrameworkElement element, string key) =>
            Localize(() =>
            {
                ToolTipService.SetToolTip(element, L(key));
                AutomationProperties.SetName(element, L(key));
            });
    }
}
