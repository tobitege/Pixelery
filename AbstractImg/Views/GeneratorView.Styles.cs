using System.Threading;
using System.Threading.Tasks;
using AbstractImg.Core.Styles;
using AbstractImg.Services;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>Style selection: category groups of cards with live previews.</summary>
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

        private Border BuildStylesSection()
        {
            var body = new StackPanel { Spacing = 18 };
            foreach (var group in StyleCatalog.All.GroupBy(s => s.Category))
            {
                var category = group.Key;
                var check = new DaisyCheckBox { Variant = DaisyCheckBoxVariant.Primary, VerticalAlignment = VerticalAlignment.Center };
                Localize(() => check.Content = L($"Category_{category}"));
                check.Click += (_, _) => ToggleCategory(category);
                _categoryChecks[category] = check;

                var grid = new WrapPanel { MinItemWidth = 220 * SizeFactor, HorizontalSpacing = 14, VerticalSpacing = 14 };
                foreach (var style in group)
                {
                    var card = new StyleCard(style) { IsSelected = Settings.Styles.Contains(style.Id) };
                    card.Toggled += (_, _) => OnCardToggled(card);
                    card.PreviewRequested += (_, _) => _shell?.ShowStylePreview(style);
                    _cards.Add(card);
                    grid.Children.Add(card);
                }
                _styleGrids.Add(grid);

                var block = new StackPanel { Spacing = 10 };
                block.Children.Add(check);
                block.Children.Add(grid);
                body.Children.Add(block);
            }

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

            UpdateStyleSelectionUi();
            return Section("AppIconBrush", "Section_Styles", "Section_StylesHint", body, actions);
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
                _styleCountBadge.Content = $"{_cards.Count(c => c.IsSelected)} / {_cards.Count}";
        }

        /// <summary>Renders all card previews again (new seed or changed color settings).</summary>
        private void RefreshPreviews()
        {
            _previewCts?.Cancel();
            var cts = new CancellationTokenSource();
            _previewCts = cts;
            var settings = Settings.Clone();
            var follow = AppSettings.PreviewsFollowColors;
            var scale = XamlRoot?.RasterizationScale ?? 1;
            var width = (int)(PreviewWidth * Math.Min(scale, 1.5));
            var height = (int)(PreviewHeight * Math.Min(scale, 1.5));
            foreach (var card in _cards)
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
