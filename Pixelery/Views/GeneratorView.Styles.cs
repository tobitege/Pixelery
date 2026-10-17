using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Styles;
using Pixelery.Services;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media.Imaging;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>Style selection: category groups with cards or compact rows.</summary>
    public sealed partial class GeneratorView
    {
        private const int PreviewWidth = 400;
        private const int PreviewHeight = 250;

        private readonly List<StyleCard> _cards = [];
        private readonly List<WrapPanel> _styleGrids = [];
        private readonly Dictionary<StyleCategory, DaisyCheckBox> _categoryChecks = [];
        private CancellationTokenSource? _previewCts;
        private int _previewSeed = Random.Shared.Next();
        private DaisyBadge? _styleCountBadge;

        private StackPanel? _stylesBody;

        /// <summary>The styles section with its header; categories are added one by one (<see cref="AddStyleCategory"/>).</summary>
        private Border BuildStylesSection()
        {
            var body = new StackPanel { Spacing = 6 };
            _stylesBody = new StackPanel { Spacing = 18 };
            var viewSelector = new DaisySelect { Width = 240, HorizontalAlignment = HorizontalAlignment.Right };
            StyleViewMode[] viewModes = [StyleViewMode.PreviewList, StyleViewMode.Cards, StyleViewMode.TextList, StyleViewMode.TextOnly];
            var updatingViewSelector = false;
            AutomationProperties.SetAutomationId(viewSelector, "StyleViewSelector");
            Localize(() =>
            {
                updatingViewSelector = true;
                var items = viewModes
                    .Select(mode => new SelectOption(mode, L($"Styles_View_{mode}"))).ToList();
                viewSelector.ItemsSource = items;
                viewSelector.SelectedItem = items.First(item => Equals(item.Key, AppSettings.StyleView));
                AutomationProperties.SetName(viewSelector, L("Styles_View"));
                updatingViewSelector = false;
            });
            viewSelector.SelectionChanged += (_, _) =>
            {
                if (updatingViewSelector || viewSelector.SelectedItem is not SelectOption { Key: StyleViewMode mode } || mode == AppSettings.StyleView)
                    return;
                AppSettings.StyleView = mode;
                ApplyStyleView();
                AppSettings.SaveOptions();
            };
            body.Children.Add(viewSelector);
            body.Children.Add(_stylesBody);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            _styleCountBadge = new DaisyBadge { Variant = DaisyBadgeVariant.Primary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            actions.Children.Add(_styleCountBadge);
            actions.Children.Add(ActionButton("Styles_All", () => SetAllStyles(true)));
            actions.Children.Add(ActionButton("Styles_None", () => SetAllStyles(false)));
            var shuffle = new DaisyButton
            {
                Variant = DaisyButtonVariant.Ghost,
                Shape = DaisyButtonShape.Square,
                Size = DaisySize.Small,
                IconData = Icon("AppIconShuffle")
            };
            LocalizeTip(shuffle, "Styles_ShufflePreviews");
            shuffle.Click += (_, _) =>
            {
                _previewSeed = Random.Shared.Next();
                RefreshPreviews();
            };
            actions.Children.Add(shuffle);

            return Section("AppIconBrush", "Section_Styles", "Section_StylesHint", body, actions, spacing: 8);
        }

        /// <summary>Adds one category with bundled previews shared by all style views.</summary>
        private void AddStyleCategory(IGrouping<StyleCategory, ArtStyle> group)
        {
            var category = group.Key;
            var check = new DaisyCheckBox { Variant = DaisyCheckBoxVariant.Primary, VerticalAlignment = VerticalAlignment.Center };
            Localize(() => check.Content = L($"Category_{category}"));
            check.Click += (_, _) => ToggleCategory(category);
            _categoryChecks[category] = check;

            var grid = new WrapPanel
            {
                MinItemWidth = 220 * SizeFactor,
                HorizontalSpacing = 14
            };
            ApplyStyleGridLayout(grid);
            var cards = new List<StyleCard>();
            foreach (var style in group)
            {
                var card = new StyleCard(style) { IsSelected = Settings.Styles.Contains(style.Id) };
                card.SetImage(new BitmapImage(new Uri($"ms-appx:///Assets/Previews/{style.Id}.jpg")));
                card.ApplyViewMode(AppSettings.StyleView);
                card.Toggled += (_, _) => OnCardToggled(card);
                card.PreviewRequested += (_, _) => _shell?.ShowStylePreview(style);
                cards.Add(card);
                grid.Children.Add(card);
            }
            _cards.AddRange(cards);
            _styleGrids.Add(grid);

            var block = new StackPanel { Spacing = 10 };
            block.Children.Add(check);
            block.Children.Add(grid);
            _stylesBody?.Children.Add(block);
            UpdateStyleSelectionUi();
        }

        private void ApplyStyleView()
        {
            foreach (var grid in _styleGrids)
                ApplyStyleGridLayout(grid);
            foreach (var card in _cards)
                card.ApplyViewMode(AppSettings.StyleView);
        }

        private static void ApplyStyleGridLayout(WrapPanel grid)
        {
            grid.ColumnCount = AppSettings.StyleView switch
            {
                StyleViewMode.Cards => 0,
                StyleViewMode.TextOnly => 2,
                _ => 1
            };
            grid.VerticalSpacing = AppSettings.StyleView switch
            {
                StyleViewMode.Cards => 14,
                StyleViewMode.TextOnly => 4,
                _ => 6
            };
            grid.InvalidateMeasure();
        }

        private DaisyButton ActionButton(string key, Action onClick)
        {
            var button = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small };
            Localize(() => button.Content = L(key));
            button.Click += (_, _) => onClick();
            return button;
        }

        private void OnCardToggled(StyleCard card)
        {
            if (card.IsSelected)
            {
                if (!Settings.Styles.Contains(card.Style.Id))
                    Settings.Styles.Add(card.Style.Id);
            }
            else
            {
                Settings.Styles.Remove(card.Style.Id);
            }
            UpdateStyleSelectionUi();
            OnSettingsChanged();
        }

        private void SetAllStyles(bool selected)
        {
            Settings.Styles = selected ? [.. StyleCatalog.All.Select(s => s.Id)] : [];
            foreach (var card in _cards)
                card.IsSelected = selected;
            UpdateStyleSelectionUi();
            OnSettingsChanged();
        }

        private void ToggleCategory(StyleCategory category)
        {
            var cards = _cards.Where(c => c.Style.Category == category).ToList();
            var selectAll = cards.Any(c => !c.IsSelected);
            foreach (var card in cards)
            {
                card.IsSelected = selectAll;
                Settings.Styles.Remove(card.Style.Id);
                if (selectAll)
                    Settings.Styles.Add(card.Style.Id);
            }
            UpdateStyleSelectionUi();
            OnSettingsChanged();
        }

        private void UpdateStyleSelectionUi()
        {
            foreach (var (category, check) in _categoryChecks)
            {
                var cards = _cards.Where(c => c.Style.Category == category).ToList();
                var selected = cards.Count(c => c.IsSelected);
                check.IsChecked = selected == cards.Count ? true : selected == 0 ? false : null;
            }
            if (_styleCountBadge != null)
                SetBadgeText(_styleCountBadge, $"{_cards.Count(c => c.IsSelected)} / {_cards.Count}");
        }

        /// <summary>Renders all card previews again (new seed or changed color settings).</summary>
        private void RefreshPreviews()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            RenderCards(_cards);
        }

        /// <summary>Renders the previews of the given cards within the current preview batch.</summary>
        private void RenderCards(IEnumerable<StyleCard> cards)
        {
            _previewCts ??= new CancellationTokenSource();
            var cts = _previewCts;
            var settings = Settings.Clone();
            var follow = AppSettings.PreviewsFollowColors;
            var scale = XamlRoot?.RasterizationScale ?? 1;
            var width = (int)(PreviewWidth * Math.Min(scale, 1.5));
            var height = (int)(PreviewHeight * Math.Min(scale, 1.5));
            foreach (var card in cards)
                _ = RenderCardAsync(card, settings, follow, width, height, cts.Token);
        }

        private async Task RenderCardAsync(StyleCard card, Core.Generation.GeneratorSettings settings, bool follow, int width, int height, CancellationToken token)
        {
            try
            {
                var seed = _previewSeed ^ card.Style.Id.GetHashCode(StringComparison.Ordinal);
                var bytes = await PreviewRenderer.RenderAsync(card.Style, seed, settings, follow, width, height, token);
                if (token.IsCancellationRequested)
                    return;
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    card.SetImage(await PreviewRenderer.ToBitmapAsync(bytes));
                });
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer refresh.
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => _shell?.ShowToast($"{card.Style.Name}: {ex.Message}", DaisyAlertVariant.Error));
            }
        }

        private DispatcherQueueTimerHolder? _previewDebounce;

        /// <summary>Color settings changed: re-render previews shortly after the last change.</summary>
        private void SchedulePreviewRefresh()
        {
            if (!AppSettings.PreviewsFollowColors)
                return;
            _previewDebounce ??= new DispatcherQueueTimerHolder(DispatcherQueue, TimeSpan.FromMilliseconds(500), RefreshPreviews);
            _previewDebounce.Restart();
        }

        /// <summary>Call after the "previews follow colors" option changed.</summary>
        public void OnPreviewOptionChanged() => RefreshPreviews();

        private sealed class DispatcherQueueTimerHolder
        {
            private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;

            public DispatcherQueueTimerHolder(Microsoft.UI.Dispatching.DispatcherQueue queue, TimeSpan interval, Action action)
            {
                _timer = queue.CreateTimer();
                _timer.Interval = interval;
                _timer.IsRepeating = false;
                _timer.Tick += (_, _) => action();
            }

            public void Restart()
            {
                _timer.Stop();
                _timer.Start();
            }
        }
    }
}
