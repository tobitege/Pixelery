using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Core.Watermark;
using Pixelery.Localization;
using Pixelery.Services;
using Flowery.Controls.ColorPicker;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>
    /// Watermark editor: live preview on a sample image on the left, layer list and the properties of the selected
    /// layer on the right. Works on a copy; the host reads <see cref="Result"/> when the dialog is confirmed.
    /// </summary>
    public sealed partial class WatermarkEditor : UserControl
    {
        private enum SampleShape
        {
            Landscape,
            Portrait,
            Square
        }

        private readonly Border _previewFrame;
        private readonly Image _previewImage;
        private readonly DaisyLoading _previewLoading;
        private readonly DaisyBadge _inactiveBadge;
        private readonly StackPanel _toolbar;
        private readonly StackPanel _layerList;
        private readonly StackPanel _properties;
        private readonly DispatcherQueueTimer _renderTimer;
        private WatermarkSettings _settings = new();
        private GeneratorSettings _generator = new();
        private WatermarkLayer? _selected;
        private SampleShape _shape = SampleShape.Landscape;
        private int _sampleSeed = Random.Shared.Next();
        private SKImage? _base;
        private NameValues? _baseValues;
        private CancellationTokenSource? _baseCts;
        private int _renderVersion;
        private IReadOnlyList<string>? _fonts;

        public WatermarkEditor()
        {
            _previewImage = new Image { Stretch = Stretch.Uniform };
            _previewLoading = new DaisyLoading { Variant = DaisyLoadingVariant.Dots, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _inactiveBadge = new DaisyBadge
            {
                Variant = DaisyBadgeVariant.Warning,
                Margin = new Thickness(12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var previewGrid = new Grid();
            previewGrid.Children.Add(_previewLoading);
            previewGrid.Children.Add(_previewImage);
            previewGrid.Children.Add(_inactiveBadge);
            _previewFrame = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = DaisyResourceLookup.GetBrush("DaisyBase300Brush"),
                Child = previewGrid
            };
            _toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

            var left = new Grid { RowSpacing = 12 };
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_toolbar, 1);
            left.Children.Add(_previewFrame);
            left.Children.Add(_toolbar);

            _layerList = new StackPanel { Spacing = 6 };
            _properties = new StackPanel { Spacing = 18 };
            var rightStack = new StackPanel { Spacing = 18, Padding = new Thickness(0, 0, 14, 8) };
            rightStack.Children.Add(_layerList);
            rightStack.Children.Add(_properties);
            var right = new ScrollViewer { Content = rightStack, HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

            var root = new Grid { ColumnSpacing = 20 };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
            Grid.SetColumn(right, 1);
            root.Children.Add(left);
            root.Children.Add(right);
            Content = root;

            _renderTimer = DispatcherQueue.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(90);
            _renderTimer.IsRepeating = false;
            _renderTimer.Tick += (_, _) => _ = RenderPreviewAsync();
            AppLocalization.Instance.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RebuildAll);
        }

        /// <summary>Shell that hosts the editor (file picker, toasts).</summary>
        public AppShell? Shell { get; set; }

        /// <summary>The edited watermark (a copy of what <see cref="Begin"/> received).</summary>
        public WatermarkSettings Result => _settings;

        public void Begin(WatermarkSettings current, GeneratorSettings generator)
        {
            _settings = current.Clone();
            _generator = generator.Clone();
            _selected = _settings.Layers.FirstOrDefault();
            RebuildAll();
            _ = RenderBaseAsync();
        }

        private void RebuildAll()
        {
            UpdateInactiveBadge();
            BuildToolbar();
            BuildLayerList();
            BuildProperties();
        }

        private void Changed(bool rebuildList = true)
        {
            if (rebuildList)
                BuildLayerList();
            _renderTimer.Stop();
            _renderTimer.Start();
        }

        #region Preview

        private void BuildToolbar()
        {
            _toolbar.Children.Clear();
            foreach (var (shape, key) in new[] { (SampleShape.Landscape, "Size_Landscape"), (SampleShape.Portrait, "Size_Portrait"), (SampleShape.Square, "Wm_Square") })
            {
                var value = shape;
                var chip = new DaisyButton { Size = DaisySize.Small, Content = L(key), Variant = _shape == value ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral };
                chip.Click += (_, _) =>
                {
                    _shape = value;
                    BuildToolbar();
                    _ = RenderBaseAsync();
                };
                _toolbar.Children.Add(chip);
            }
            var shuffle = new DaisyButton { Size = DaisySize.Small, Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square, IconData = Icon("AppIconShuffle") };
            Tip(shuffle, L("Wm_SampleNew"));
            shuffle.Click += (_, _) =>
            {
                _sampleSeed = Random.Shared.Next();
                _ = RenderBaseAsync();
            };
            _toolbar.Children.Add(shuffle);

            var active = ToggleRow("Wm_Enabled", _settings.Enabled, v =>
            {
                _settings.Enabled = v;
                UpdateInactiveBadge();
                Changed(false);
            });
            active.Margin = new Thickness(18, 0, 0, 0);
            active.VerticalAlignment = VerticalAlignment.Center;
            _toolbar.Children.Add(active);
        }

        /// <summary>The preview always shows the layers; a badge tells when the watermark is switched off.</summary>
        private void UpdateInactiveBadge()
        {
            SetBadgeText(_inactiveBadge, L("Wm_Inactive"));
            _inactiveBadge.Visibility = _settings.Enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Renders a sample image with the configured styles; the watermark is drawn on top of it on every change.</summary>
        private async Task RenderBaseAsync()
        {
            _baseCts?.Cancel();
            var cts = new CancellationTokenSource();
            _baseCts = cts;
            _previewLoading.Visibility = Visibility.Visible;
            var (width, height) = _shape switch
            {
                SampleShape.Portrait => (800, 1200),
                SampleShape.Square => (1000, 1000),
                _ => (1280, 800)
            };
            _previewFrame.MaxWidth = _shape == SampleShape.Landscape ? double.PositiveInfinity : 620;
            var generator = _generator;
            var seed = _sampleSeed;
            try
            {
                var (image, values) = await Task.Run(() =>
                {
                    var styles = generator.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
                    if (styles.Count == 0)
                        styles = [.. StyleCatalog.All];
                    var settings = generator.Clone();
                    if (settings.Palettes.Count == 0)
                        settings.Palettes = [PaletteFamily.Curated];
                    if (settings.Tones.Count == 0)
                        settings.Tones = [BackgroundTone.Dark];
                    if (settings.Details.Count == 0)
                        settings.Details = [DetailLevel.Medium];
                    if (settings.Formats.Count == 0)
                        settings.Formats = [OutputFormat.Jpeg];
                    var size = new SizeChoice(width, height, SizeCatalog.RatioLabel(width, height));
                    var recipe = ImageGenerator.CreateRecipe(settings, styles, [size], seed, settings.StartIndex, DateTime.Now);
                    var (rendered, palette) = ImageGenerator.Render(recipe);
                    return (rendered, ImageGenerator.CreateNameValues(recipe, settings.Count, ImageGenerator.PaletteSlug(palette), DateTime.Now));
                }, cts.Token);
                if (cts.IsCancellationRequested)
                    return;
                _base = image;
                _baseValues = values;
                await RenderPreviewAsync();
            }
            catch (OperationCanceledException)
            {
                // Replaced by a newer sample.
            }
        }

        private async Task RenderPreviewAsync()
        {
            if (_base is not { } image || _baseValues is not { } values)
                return;
            var version = ++_renderVersion;
            var settings = _settings.Clone();
            settings.Enabled = true;
            var seed = _sampleSeed;
            try
            {
                var bytes = await Task.Run(() =>
                {
                    var marked = WatermarkRenderer.Apply(image, settings, values, seed);
                    try
                    {
                        return ImageEncoder.Encode(marked, OutputFormat.Jpeg, 90, null);
                    }
                    finally
                    {
                        if (!ReferenceEquals(marked, image))
                            marked.Dispose();
                    }
                });
                if (version != _renderVersion)
                    return;
                _previewImage.Source = await PreviewRenderer.ToBitmapAsync(bytes);
                _previewLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Shell?.ShowToast(ex.Message, DaisyAlertVariant.Error);
            }
        }

        #endregion

        #region Layer list

        private void BuildLayerList()
        {
            _layerList.Children.Clear();
            var header = new Grid { ColumnSpacing = 6 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Responsive(new TextBlock { Text = L("Wm_Layers"), Style = (Style)Application.Current.Resources["AppSectionHeaderStyle"] }, ResponsiveFontTier.SectionHeader);
            var addText = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, IconSymbol = Symbol.Add, Content = L("Wm_AddText") };
            addText.Click += (_, _) => AddLayer(WatermarkLayer.NewText());
            var addImage = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, IconSymbol = Symbol.Add, Content = L("Wm_AddImage") };
            addImage.Click += (_, _) => AddLayer(WatermarkLayer.NewImage());
            Grid.SetColumn(addText, 1);
            Grid.SetColumn(addImage, 2);
            header.Children.Add(title);
            header.Children.Add(addText);
            header.Children.Add(addImage);
            _layerList.Children.Add(header);

            if (_settings.Layers.Count == 0)
            {
                _layerList.Children.Add(Text(L("Wm_NoSelection"), ResponsiveFontTier.Secondary, 0.65));
                return;
            }
            // Top of the list is drawn last (on top), like layers in image editors.
            for (var i = _settings.Layers.Count - 1; i >= 0; i--)
                _layerList.Children.Add(LayerRow(_settings.Layers[i], i));
        }

        private Border LayerRow(WatermarkLayer layer, int index)
        {
            var selected = ReferenceEquals(layer, _selected);
            var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(10, 6, 6, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Viewbox
            {
                Width = 18,
                Height = 18,
                Opacity = 0.8,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Path { Data = ToGeometry(Icon(layer.Kind == WatermarkLayerKind.Text ? "AppIconTextbox" : "AppIconLandscape")!), Fill = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") }
            };
            var label = Text(LayerLabel(layer), ResponsiveFontTier.Primary, layer.Enabled ? 1 : 0.5);
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var toggle = new DaisyToggle { IsOn = layer.Enabled, Size = DaisySize.Small, Variant = DaisyToggleVariant.Primary, VerticalAlignment = VerticalAlignment.Center };
            FlowerySizeManager.SetIgnoreGlobalSize(toggle, true);
            Tip(toggle, L("Wm_LayerOn"));
            var current = layer.Enabled;
            toggle.Toggled += (_, _) =>
            {
                if (toggle.IsOn == current)
                    return;
                current = toggle.IsOn;
                layer.Enabled = current;
                Changed();
            };
            actions.Children.Add(toggle);
            actions.Children.Add(IconButton("Wm_MoveUp", "", index < _settings.Layers.Count - 1, () => Move(index, 1)));
            actions.Children.Add(IconButton("Wm_MoveDown", "", index > 0, () => Move(index, -1)));
            actions.Children.Add(IconButton("Wm_Delete", "", true, () => Delete(layer)));

            Grid.SetColumn(label, 1);
            Grid.SetColumn(actions, 2);
            grid.Children.Add(icon);
            grid.Children.Add(label);
            grid.Children.Add(actions);

            var row = new Border
            {
                Child = grid,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = selected ? WindowsAccent.Brush : DaisyResourceLookup.GetBrush("DaisyBase300Brush"),
                Background = DaisyResourceLookup.GetBrush(selected ? "DaisyBase100Brush" : "DaisyBase200Brush")
            };
            row.Tapped += (_, e) =>
            {
                if (e.OriginalSource is FrameworkElement { } source && IsInside(source, actions))
                    return;
                _selected = layer;
                BuildLayerList();
                BuildProperties();
            };
            return row;
        }

        private static bool IsInside(DependencyObject element, DependencyObject container)
        {
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current == container)
                    return true;
            }
            return false;
        }

        private static DaisyButton IconButton(string tipKey, string glyph, bool enabled, Action onClick)
        {
            var button = new DaisyButton
            {
                Variant = DaisyButtonVariant.Ghost,
                Shape = DaisyButtonShape.Square,
                Size = DaisySize.ExtraSmall,
                Content = new FontIcon { Glyph = glyph, FontSize = 12 },
                IsEnabled = enabled
            };
            FlowerySizeManager.SetIgnoreGlobalSize(button, true);
            Tip(button, L(tipKey));
            button.Click += (_, _) => onClick();
            return button;
        }

        private static string LayerLabel(WatermarkLayer layer)
        {
            if (layer.Kind == WatermarkLayerKind.Image)
            {
                var name = System.IO.Path.GetFileName(layer.ImagePath);
                return string.IsNullOrEmpty(name) ? L("Wm_ImageMissing") : name;
            }
            var firstLine = (layer.Text ?? string.Empty).Replace("\r", string.Empty).Split('\n')[0];
            return string.IsNullOrWhiteSpace(firstLine) ? L("Wm_Text") : firstLine;
        }

        private void AddLayer(WatermarkLayer layer)
        {
            _settings.Layers.Add(layer);
            _settings.Enabled = true;
            _selected = layer;
            UpdateInactiveBadge();
            BuildToolbar();
            BuildProperties();
            Changed();
            if (layer.Kind == WatermarkLayerKind.Image)
                _ = BrowseImageAsync(layer);
        }

        private void Move(int index, int delta)
        {
            var target = index + delta;
            if (target < 0 || target >= _settings.Layers.Count)
                return;
            (_settings.Layers[index], _settings.Layers[target]) = (_settings.Layers[target], _settings.Layers[index]);
            Changed();
        }

        private void Delete(WatermarkLayer layer)
        {
            var index = _settings.Layers.IndexOf(layer);
            _settings.Layers.Remove(layer);
            if (ReferenceEquals(_selected, layer))
                _selected = _settings.Layers.Count == 0 ? null : _settings.Layers[Math.Clamp(index - 1, 0, _settings.Layers.Count - 1)];
            BuildProperties();
            Changed();
        }

        #endregion

        #region Properties

        private void BuildProperties()
        {
            _properties.Children.Clear();
            if (_selected is not { } layer)
                return;
            if (layer.Kind == WatermarkLayerKind.Text)
                BuildTextProperties(layer);
            else
                BuildImageProperties(layer);
            BuildLookProperties(layer);
            BuildPositionProperties(layer);
        }

        private void BuildTextProperties(WatermarkLayer layer)
        {
            var input = new DaisyInput
            {
                Text = layer.Text,
                Variant = DaisyInputVariant.Bordered,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 76
            };
            input.TextChanged += (_, _) =>
            {
                layer.Text = (input.Text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
                Changed();
            };
            var tokens = new WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
            foreach (var token in new[] { "©", "{now:yyyy}", "{style}", "{seed}", "{w}×{h}", "{n}", "{palette}" })
            {
                var value = token;
                var button = new DaisyButton
                {
                    Content = token,
                    Variant = DaisyButtonVariant.Primary,
                    ButtonStyle = DaisyButtonStyle.Soft,
                    Size = DaisySize.ExtraSmall,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas")
                };
                button.Click += (_, _) =>
                {
                    var text = input.Text ?? string.Empty;
                    var caret = Math.Clamp(input.SelectionStart, 0, text.Length);
                    input.Text = text[..caret] + value + text[caret..];
                    input.Focus(FocusState.Programmatic);
                    input.SelectionStart = caret + value.Length;
                };
                tokens.Children.Add(button);
            }
            var textBlock = new StackPanel { Spacing = 8 };
            textBlock.Children.Add(input);
            textBlock.Children.Add(tokens);
            _properties.Children.Add(Group("Wm_Text", textBlock, "Wm_TextHint"));

            // Font and style
            _fonts ??= WatermarkRenderer.FontFamilies();
            var fonts = _fonts.Contains(layer.FontFamily) ? _fonts : [layer.FontFamily, .. _fonts];
            var fontSelect = new DaisySelect
            {
                ItemsSource = fonts.Select(f => new SelectOption(f, f)).ToList(),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            fontSelect.SelectedItem = ((List<SelectOption>)fontSelect.ItemsSource).FirstOrDefault(o => (string)o.Key == layer.FontFamily);
            fontSelect.SelectionChanged += (_, _) =>
            {
                if (fontSelect.SelectedItem is SelectOption { Key: string family })
                {
                    layer.FontFamily = family;
                    Changed(false);
                }
            };
            var styleChips = new WrapPanel();
            styleChips.Children.Add(FlagChip("Wm_Bold", () => layer.Bold, v => layer.Bold = v));
            styleChips.Children.Add(FlagChip("Wm_Italic", () => layer.Italic, v => layer.Italic = v));
            styleChips.Children.Add(FlagChip("Wm_Upper", () => layer.Uppercase, v => layer.Uppercase = v));
            var fontBlock = new StackPanel { Spacing = 10 };
            fontBlock.Children.Add(fontSelect);
            fontBlock.Children.Add(styleChips);
            fontBlock.Children.Add(Slider("Wm_Size", 0.5, 30, 0.1, () => layer.Size, v => layer.Size = v, v => AppLocalization.Format("Wm_SizeFormat", v)));
            fontBlock.Children.Add(Slider("Wm_LetterSpacing", -10, 80, 1, () => layer.LetterSpacing, v => layer.LetterSpacing = v, v => $"{v:0} %"));
            fontBlock.Children.Add(ColorRow("Wm_Color", () => layer.Color, v => layer.Color = v));
            _properties.Children.Add(Group("Wm_Font", fontBlock));

            // Effects
            var effects = new StackPanel { Spacing = 10 };
            effects.Children.Add(Switch("Wm_Outline", () => layer.Outline, v => layer.Outline = v, rebuild: true));
            if (layer.Outline)
            {
                effects.Children.Add(ColorRow("Wm_OutlineColor", () => layer.OutlineColor, v => layer.OutlineColor = v));
                effects.Children.Add(Slider("Wm_OutlineWidth", 1, 30, 1, () => layer.OutlineWidth, v => layer.OutlineWidth = v, v => $"{v:0} %"));
            }
            effects.Children.Add(Switch("Wm_Shadow", () => layer.Shadow, v => layer.Shadow = v, rebuild: true));
            if (layer.Shadow)
            {
                effects.Children.Add(ColorRow("Wm_ShadowColor", () => layer.ShadowColor, v => layer.ShadowColor = v));
                effects.Children.Add(Slider("Wm_ShadowSize", 1, 40, 1, () => layer.ShadowSize, v => layer.ShadowSize = v, v => $"{v:0} %"));
            }
            effects.Children.Add(Switch("Wm_Plate", () => layer.Plate, v => layer.Plate = v, rebuild: true));
            if (layer.Plate)
            {
                effects.Children.Add(ColorRow("Wm_PlateColor", () => layer.PlateColor, v => layer.PlateColor = v));
                effects.Children.Add(Slider("Wm_PlatePadding", 0, 150, 1, () => layer.PlatePadding, v => layer.PlatePadding = v, v => $"{v:0} %"));
                effects.Children.Add(Slider("Wm_PlateRadius", 0, 50, 1, () => layer.PlateRadius, v => layer.PlateRadius = v, v => $"{v:0} %"));
            }
            _properties.Children.Add(Group("Wm_Effects", effects));
        }

        private void BuildImageProperties(WatermarkLayer layer)
        {
            var path = new DaisyInput { Text = layer.ImagePath, Variant = DaisyInputVariant.Bordered, IsReadOnly = true };
            var browse = new DaisyButton { Variant = DaisyButtonVariant.Primary, IconSymbol = Symbol.OpenFile, Content = L("Wm_Browse") };
            browse.Click += async (_, _) => await BrowseImageAsync(layer);
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(browse, 1);
            row.Children.Add(path);
            row.Children.Add(browse);
            var block = new StackPanel { Spacing = 10 };
            block.Children.Add(row);
            if (!string.IsNullOrEmpty(layer.ImagePath) && !WatermarkRenderer.CanLoadImage(layer.ImagePath))
            {
                var error = Text(L("Validation_WatermarkImage"), ResponsiveFontTier.Secondary);
                error.Foreground = DaisyResourceLookup.GetBrush("DaisyErrorBrush");
                block.Children.Add(error);
            }
            block.Children.Add(Slider("Wm_ImageWidth", 1, 100, 0.5, () => layer.ImageWidth, v => layer.ImageWidth = v, v => AppLocalization.Format("Wm_ImageWidthFormat", v)));
            block.Children.Add(Switch("Wm_Grayscale", () => layer.Grayscale, v => layer.Grayscale = v));
            block.Children.Add(Switch("Wm_Tint", () => layer.Tint, v => layer.Tint = v, rebuild: true));
            if (layer.Tint)
                block.Children.Add(ColorRow("Wm_TintColor", () => layer.TintColor, v => layer.TintColor = v));
            block.Children.Add(Switch("Wm_Shadow", () => layer.Shadow, v => layer.Shadow = v, rebuild: true));
            if (layer.Shadow)
                block.Children.Add(Slider("Wm_ShadowSize", 1, 40, 1, () => layer.ShadowSize, v => layer.ShadowSize = v, v => $"{v:0} %"));
            _properties.Children.Add(Group("Wm_Image", block, "Wm_ImageHint"));
        }

        private void BuildLookProperties(WatermarkLayer layer)
        {
            var block = new StackPanel { Spacing = 10 };
            block.Children.Add(Slider("Wm_Opacity", 0, 100, 1, () => layer.Opacity, v => layer.Opacity = v, v => $"{v:0} %"));
            var blends = new WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
            foreach (var blend in Enum.GetValues<WatermarkBlend>())
                blends.Children.Add(ChoiceChip($"Blend_{blend}", () => layer.Blend == blend, () => layer.Blend = blend));
            block.Children.Add(Caption(L("Wm_Blend")));
            block.Children.Add(blends);
            _properties.Children.Add(Group("Wm_Look", block));
        }

        private void BuildPositionProperties(WatermarkLayer layer)
        {
            var block = new StackPanel { Spacing = 10 };
            var placements = new WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
            foreach (var placement in Enum.GetValues<WatermarkPlacement>())
            {
                placements.Children.Add(ChoiceChip($"Wm_Placement_{placement}", () => layer.Placement == placement, () =>
                {
                    layer.Placement = placement;
                    BuildProperties();
                }));
            }
            block.Children.Add(placements);

            if (layer.Placement == WatermarkPlacement.Single)
                block.Children.Add(AnchorGrid(layer));
            if (layer.Placement != WatermarkPlacement.Tiled)
                block.Children.Add(Slider("Wm_Margin", 0, 25, 0.5, () => layer.Margin, v => layer.Margin = v, v => $"{v:0.#} %"));
            else
                block.Children.Add(Slider("Wm_TileSpacing", 0, 400, 5, () => layer.TileSpacing, v => layer.TileSpacing = v, v => $"{v:0} %"));
            block.Children.Add(Slider("Wm_Rotation", -180, 180, 1, () => layer.Rotation, v => layer.Rotation = v, v => $"{v:0}°"));
            _properties.Children.Add(Group("Wm_Position", block));
        }

        /// <summary>3 × 3 buttons for the anchor position.</summary>
        private Grid AnchorGrid(WatermarkLayer layer)
        {
            var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
            for (var i = 0; i < 3; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            foreach (var anchor in Enum.GetValues<WatermarkAnchor>())
            {
                var value = anchor;
                var button = new DaisyButton
                {
                    Shape = DaisyButtonShape.Square,
                    Size = DaisySize.Small,
                    Variant = layer.Anchor == value ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral,
                    Content = new Ellipse { Width = 8, Height = 8, Fill = DaisyResourceLookup.GetBrush(layer.Anchor == value ? "DaisyPrimaryContentBrush" : "DaisyNeutralContentBrush") }
                };
                Tip(button, value.ToString());
                button.Click += (_, _) =>
                {
                    layer.Anchor = value;
                    BuildProperties();
                    Changed(false);
                };
                Grid.SetRow(button, (int)value / 3);
                Grid.SetColumn(button, (int)value % 3);
                grid.Children.Add(button);
            }
            return grid;
        }

        #endregion

        #region Building blocks

        private static StackPanel Group(string titleKey, UIElement content, string? hintKey = null)
        {
            var group = new StackPanel { Spacing = 10 };
            group.Children.Add(Responsive(new TextBlock { Text = L(titleKey), Style = (Style)Application.Current.Resources["AppSectionHeaderStyle"] }, ResponsiveFontTier.Primary));
            group.Children.Add(content);
            if (hintKey != null)
                group.Children.Add(Text(L(hintKey), ResponsiveFontTier.Secondary, 0.6));
            return group;
        }

        private static TextBlock Caption(string text) => Responsive(new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["AppCaptionTextStyle"]
        }, ResponsiveFontTier.Secondary);

        private Grid Slider(string key, double min, double max, double step, Func<double> get, Action<double> set, Func<double, string> format)
        {
            var grid = new Grid { ColumnSpacing = 12, RowSpacing = 2 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = Text(L(key), ResponsiveFontTier.Secondary, 0.85);
            var value = Text(format(get()), ResponsiveFontTier.Secondary, 0.7);
            var range = Range(min, max, Math.Clamp(get(), min, max), step, v =>
            {
                set(v);
                value.Text = format(v);
                Changed(false);
            });
            range.Size = DaisySize.Small;
            Grid.SetColumn(value, 1);
            Grid.SetRow(range, 1);
            Grid.SetColumnSpan(range, 2);
            grid.Children.Add(label);
            grid.Children.Add(value);
            grid.Children.Add(range);
            return grid;
        }

        private Grid Switch(string key, Func<bool> get, Action<bool> set, bool rebuild = false) =>
            ToggleRow(key, get(), v =>
            {
                set(v);
                if (rebuild)
                    BuildProperties();
                Changed(false);
            }, DaisySize.Small);

        private DaisyButton FlagChip(string key, Func<bool> get, Action<bool> set)
        {
            var chip = new DaisyButton { Size = DaisySize.Small, Content = L(key) };
            void Apply() => chip.Variant = get() ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral;
            chip.Click += (_, _) =>
            {
                set(!get());
                Apply();
                Changed(false);
            };
            Apply();
            return chip;
        }

        private DaisyButton ChoiceChip(string key, Func<bool> isSelected, Action select)
        {
            var chip = new DaisyButton
            {
                Size = DaisySize.Small,
                Content = L(key),
                Variant = isSelected() ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral
            };
            chip.Click += (_, _) =>
            {
                select();
                if (chip.Parent is Panel panel)
                {
                    foreach (var other in panel.Children.OfType<DaisyButton>())
                        other.Variant = ReferenceEquals(other, chip) ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral;
                }
                Changed(false);
            };
            return chip;
        }

        /// <summary>Color swatch button that opens the Flowery color picker (with alpha).</summary>
        private Grid ColorRow(string key, Func<string> get, Action<string> set)
        {
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = Text(L(key), ResponsiveFontTier.Secondary, 0.85);
            var swatch = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush") };
            var hex = new TextBlock { FontFamily = new FontFamily("Cascadia Mono, Consolas"), VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(swatch);
            content.Children.Add(hex);
            var button = new DaisyButton { Variant = DaisyButtonVariant.Neutral, Size = DaisySize.Small, Content = content };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, L(key));
            void Show()
            {
                var color = WatermarkRenderer.ParseColor(get(), SKColors.White);
                swatch.Background = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
                hex.Text = get().ToUpperInvariant();
            }
            button.Click += async (_, _) =>
            {
                var current = WatermarkRenderer.ParseColor(get(), SKColors.White);
                var picked = await DaisyColorPickerDialog.ShowAsync(XamlRoot, Color.FromArgb(current.Alpha, current.Red, current.Green, current.Blue), showAlphaChannel: true, customColors: null);
                if (picked is { } c)
                {
                    set($"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");
                    Show();
                    Changed(false);
                }
            };
            Show();
            Grid.SetColumn(button, 1);
            grid.Children.Add(label);
            grid.Children.Add(button);
            return grid;
        }

        private async Task BrowseImageAsync(WatermarkLayer layer)
        {
            if (Shell == null)
                return;
            var file = await Shell.PickImageFileAsync();
            if (file == null)
                return;
            layer.ImagePath = file;
            BuildProperties();
            Changed();
        }

        #endregion
    }
}
