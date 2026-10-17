using Microsoft.UI.Xaml.Automation;
using Pixelery.Core.Generation;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Localization;
using Pixelery.Services;
using Windows.System;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>Colors, dimensions and output sections, built from small chip, block and section helpers.</summary>
    public sealed partial class GeneratorView
    {
        private readonly List<(DaisyButton Chip, Func<bool> IsSelected)> _chips = [];
        private TextBlock? _sizeSummary;
        private DaisyInput? _folderInput;
        private DaisyInput? _nameInput;
        private TextBlock? _nameError;
        private StackPanel? _nameExamples;

        /// <summary>
        /// The page is built in small steps, one per frame, so the window appears immediately and fills in:
        /// side panel first, then the styles one category at a time, then the other sections.
        /// </summary>
        private IEnumerable<Action> BuildSteps()
        {
            yield return () =>
            {
                InitializeRunPanel();
                InitializeSideTabs();
                _sectionLocalizerStart = _localizers.Count;
            };
            foreach (var step in SectionSteps())
                yield return step;
        }

        /// <summary>The configuration sections, built from the current settings; an import builds them again.</summary>
        private IEnumerable<Action> SectionSteps()
        {
            yield return () => ReplacePlaceholder(BuildStylesSection());
            foreach (var group in StyleCatalog.All.GroupBy(s => s.Category))
                yield return () => AddStyleCategory(group);
            yield return () => ReplacePlaceholder(BuildColorsSection());
            yield return () => ReplacePlaceholder(BuildDimensionsSection());
            yield return () => ReplacePlaceholder(BuildWatermarkSection());
            yield return () => ReplacePlaceholder(BuildOutputSection());
        }

        private const string PlaceholderTag = "placeholder";

        /// <summary>Skeleton blocks shown where the sections will appear.</summary>
        private void AddPlaceholders()
        {
            foreach (var height in new[] { 520.0, 300.0, 360.0, 200.0, 420.0 })
            {
                Sections.Children.Add(new Border
                {
                    Tag = PlaceholderTag,
                    Height = height,
                    CornerRadius = new CornerRadius(12),
                    Child = new DaisySkeleton { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch }
                });
            }
        }

        private void ReplacePlaceholder(UIElement section)
        {
            for (var i = 0; i < Sections.Children.Count; i++)
            {
                if (Sections.Children[i] is FrameworkElement { Tag: PlaceholderTag })
                {
                    Sections.Children[i] = section;
                    return;
                }
            }
            Sections.Children.Add(section);
        }

        #region Building blocks

        private Border Section(string iconKey, string titleKey, string? hintKey, UIElement body, UIElement? headerRight = null, double spacing = 18)
        {
            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tile = new Grid { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
            tile.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush"), Opacity = 0.16 });
            tile.Children.Add(new Viewbox
            {
                Margin = new Thickness(8),
                Child = new Path { Data = ToGeometry(Icon(iconKey)!), Fill = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush") }
            });

            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = Responsive(new TextBlock { Style = (Style)Application.Current.Resources["AppSectionHeaderStyle"] }, ResponsiveFontTier.SectionHeader);
            Localize(() => title.Text = L(titleKey));
            titles.Children.Add(title);
            if (hintKey != null)
            {
                var hint = Text(string.Empty, ResponsiveFontTier.Secondary, 0.65);
                Localize(() => hint.Text = L(hintKey));
                titles.Children.Add(hint);
            }

            Grid.SetColumn(titles, 1);
            header.Children.Add(tile);
            header.Children.Add(titles);
            if (headerRight is FrameworkElement right)
            {
                Grid.SetColumn(right, 2);
                header.Children.Add(right);
            }

            var stack = new StackPanel { Spacing = spacing };
            stack.Children.Add(header);
            stack.Children.Add(body);
            var section = new Border { Style = (Style)Application.Current.Resources["AppSectionStyle"], Child = stack };
            RegisterSection(titleKey, iconKey, section);
            return section;
        }

        /// <summary>Caption above content, optional hint below.</summary>
        private StackPanel Block(string captionKey, UIElement content, string? hintKey = null)
        {
            var block = new StackPanel { Spacing = 8 };
            var caption = Responsive(new TextBlock { Style = (Style)Application.Current.Resources["AppCaptionTextStyle"] }, ResponsiveFontTier.Secondary);
            Localize(() => caption.Text = L(captionKey));
            block.Children.Add(caption);
            block.Children.Add(content);
            if (hintKey != null)
            {
                var hint = Text(string.Empty, ResponsiveFontTier.Secondary, 0.6);
                Localize(() => hint.Text = L(hintKey));
                block.Children.Add(hint);
            }
            return block;
        }

        private static WrapPanel Columns(double minWidth, params UIElement[] children)
        {
            var panel = new WrapPanel { MinItemWidth = minWidth, HorizontalSpacing = 28, VerticalSpacing = 20 };
            foreach (var child in children)
                panel.Children.Add(child);
            return panel;
        }

        private Grid InlineField(string captionKey, FrameworkElement control, string hintKey)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var caption = Text(string.Empty, ResponsiveFontTier.Secondary, 0.8);
            Localize(() => caption.Text = L(captionKey));
            LocalizeTip(control, hintKey);
            Grid.SetColumn(control, 1);
            row.Children.Add(caption);
            row.Children.Add(control);
            return row;
        }

        /// <summary>A toggle chip: Primary when selected, Neutral otherwise.</summary>
        private DaisyButton Chip(Func<string> text, Func<bool> isSelected, Action onClick)
        {
            var chip = new DaisyButton { Size = DaisySize.Small };
            Localize(() => chip.Content = text());
            chip.Click += (_, _) =>
            {
                onClick();
                ApplyChip(chip, isSelected());
            };
            ApplyChip(chip, isSelected());
            _chips.Add((chip, isSelected));
            return chip;
        }

        private static void ApplyChip(DaisyButton chip, bool selected) =>
            chip.Variant = selected ? DaisyButtonVariant.Primary : DaisyButtonVariant.Neutral;

        private void RefreshChipVisuals()
        {
            foreach (var (chip, isSelected) in _chips)
                ApplyChip(chip, isSelected());
        }

        /// <summary>Multiple-choice chips bound to a settings list.</summary>
        private WrapPanel ChipGroup<T>(IEnumerable<(T Value, Func<string> Text)> items, Func<List<T>> target, Action? changed = null)
        {
            var panel = new WrapPanel();
            foreach (var (value, text) in items)
            {
                var item = value;
                panel.Children.Add(Chip(text, () => target().Contains(item), () =>
                {
                    var list = target();
                    if (!list.Remove(item))
                        list.Add(item);
                    OnSettingsChanged();
                    changed?.Invoke();
                }));
            }
            return panel;
        }

        private Grid LToggleRow(string key, bool isOn, Action<bool> onChanged)
        {
            var row = ToggleRow(key, isOn, v =>
            {
                onChanged(v);
                OnSettingsChanged();
            });
            var label = (TextBlock)row.Children[0];
            Localize(() => label.Text = L(key));
            return row;
        }

        private static DaisyNumericUpDown Number(decimal min, decimal max, decimal value, Action<int> onChanged, double width = 140)
        {
            var input = new DaisyNumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            input.RegisterPropertyChangedCallback(DaisyNumericUpDown.ValueProperty, (_, _) =>
            {
                if (input.Value is { } v)
                    onChanged((int)Math.Clamp(v, min, max));
            });
            return input;
        }

        #endregion

        #region Colors & finish

        private Border BuildColorsSection()
        {
            (PaletteFamily, Func<string>)[] families = [.. Enum.GetValues<PaletteFamily>().Select(f => (f, (Func<string>)(() => L($"Palette_{f}"))))];
            (BackgroundTone, Func<string>)[] tones = [.. Enum.GetValues<BackgroundTone>().Select(t => (t, (Func<string>)(() => L($"Tone_{t}"))))];
            (DetailLevel, Func<string>)[] details = [.. Enum.GetValues<DetailLevel>().Select(d => (d, (Func<string>)(() => L($"Detail_{d}"))))];

            void ColorsChanged()
            {
                ShufflePaletteSamples();
                SchedulePreviewRefresh();
            }

            var samples = BuildPaletteSamples();

            var finish = new StackPanel { Spacing = 10 };
            finish.Children.Add(LToggleRow("Colors_Grain", Settings.Grain, v =>
            {
                Settings.Grain = v;
                SchedulePreviewRefresh();
            }));
            finish.Children.Add(LToggleRow("Colors_Vignette", Settings.Vignette, v =>
            {
                Settings.Vignette = v;
                SchedulePreviewRefresh();
            }));

            var body = new StackPanel { Spacing = 22 };
            body.Children.Add(Block("Colors_Palettes", ChipGroup(families, () => Settings.Palettes, ColorsChanged), "Colors_PalettesHint"));
            body.Children.Add(Block("Colors_Samples", samples));
            body.Children.Add(Columns(300 * SizeFactor,
                Block("Colors_Tones", ChipGroup(tones, () => Settings.Tones, ColorsChanged)),
                Block("Colors_Detail", ChipGroup(details, () => Settings.Details, SchedulePreviewRefresh)),
                Block("Colors_Finish", finish)));

            UpdatePaletteSamples();
            return Section("AppIconPalette", "Section_Colors", "Section_ColorsHint", body);
        }

        #endregion

        #region Dimensions

        private Border BuildDimensionsSection()
        {
            List<Action> refreshSelections = [];
            void SelectionChanged()
            {
                foreach (var refresh in refreshSelections)
                    refresh();
                RefreshChipVisuals();
                UpdateSizeSummary();
                UpdateNamePreview();
                OnSettingsChanged();
            }

            var randomRow = ToggleRow("Size_Random", Settings.RandomSizes, value =>
            {
                if (Settings.RandomSizes == value) return;
                Settings.RandomSizes = Settings.MultipleSizes && value;
                SelectionChanged();
            });
            var random = (DaisyToggle)randomRow.Children[1];
            random.IsEnabled = Settings.MultipleSizes;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(random, "RandomSizes");
            var multiRow = ToggleRow("Size_Multiple", Settings.MultipleSizes, value =>
            {
                Settings.SetMultipleSizes(value);
                random.IsEnabled = Settings.MultipleSizes;
                random.IsOn = Settings.RandomSizes;
                SelectionChanged();
            });
            var multi = (DaisyToggle)multiRow.Children[1];
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(multi, "MultipleSizes");
            Localize(() =>
            {
                ((TextBlock)multiRow.Children[0]).Text = L("Size_Multiple");
                ((TextBlock)randomRow.Children[0]).Text = L("Size_Random");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(multi, L("Size_Multiple"));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(random, L("Size_Random"));
            });
            var modes = new WrapPanel { HorizontalSpacing = 24 };
            modes.Children.Add(multiRow);
            modes.Children.Add(randomRow);

            (ImageOrientation, Func<string>)[] orientations =
            [
                (ImageOrientation.Landscape, () => L("Size_Landscape")),
                (ImageOrientation.Portrait, () => L("Size_Portrait"))
            ];
            var orientationChips = new WrapPanel();
            foreach (var (value, text) in orientations)
                orientationChips.Children.Add(Chip(text, () => Settings.Orientations.Contains(value), () =>
                {
                    Settings.SelectOrientation(value);
                    SelectionChanged();
                }));

            var ratios = CustomSizeSelect("Size_Ratios", "SizeRatios",
                SizeCatalog.AspectRatios,
                () => Settings.AspectRatios,
                () => Settings.CustomAspectRatios,
                SizeCatalog.IsRatioInput,
                text => SizeCatalog.TryParseRatio(text, out var w, out var h) ? SizeCatalog.NormalizeRatio(w, h) : null,
                value => SizeCatalog.FormatRatio(value, AppLocalization.CurrentCulture),
                "16:9",
                (value, selected) => Settings.SelectRatio(value, selected),
                value => Settings.CustomAspectRatios.Add(value),
                value => Settings.RemoveCustomRatio(value), SelectionChanged);
            var edges = CustomSizeSelect("Size_LongEdges", "SizeResolution",
                SizeCatalog.LongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                () => Settings.LongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                () => Settings.CustomLongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                SizeCatalog.IsEdgeInput,
                text => SizeCatalog.TryParseEdge(text, out var edge) ? edge.ToString(CultureInfo.InvariantCulture) : null,
                value => $"{int.Parse(value, CultureInfo.InvariantCulture):N0} px",
                "2048",
                onSelect: (value, add) =>
                {
                    var edge = int.Parse(value, CultureInfo.InvariantCulture);
                    Settings.SelectLongEdge(edge, add);
                },
                onAdd: value => Settings.CustomLongEdges.Add(int.Parse(value, CultureInfo.InvariantCulture)),
                onRemove: value => Settings.RemoveCustomLongEdge(int.Parse(value, CultureInfo.InvariantCulture)),
                changed: SelectionChanged);
            var fixedSizes = CustomSizeSelect("Size_Fixed", "SizePresets",
                SizeCatalog.FixedSizes.Select(p => p.Value).ToList(),
                () => Settings.FixedSizes,
                () => Settings.CustomFixedSizes,
                SizeCatalog.IsSizeInput,
                text => SizeCatalog.TryParseSize(text, out var w, out var h) ? $"{w}x{h}" : null,
                value =>
                {
                    var preset = SizeCatalog.FixedSizes.FirstOrDefault(p => p.Value == value);
                    var dims = value.Replace("x", " × ", StringComparison.Ordinal);
                    return preset.Label != null ? $"{preset.Label} · {dims}" : dims;
                },
                "1600x900",
                (value, selected) => Settings.SelectFixedSize(value, selected),
                value => Settings.CustomFixedSizes.Add(value),
                value => Settings.RemoveCustomFixedSize(value), SelectionChanged);
            refreshSelections.AddRange([ratios.Refresh, edges.Refresh, fixedSizes.Refresh]);

            var fields = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            void Field(string key, string hintKey, FrameworkElement control, FrameworkElement? editor = null, TextBlock? error = null)
            {
                var row = fields.RowDefinitions.Count;
                fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var caption = Text(string.Empty, ResponsiveFontTier.Secondary, 0.8);
                Localize(() => caption.Text = L(key));
                Localize(() => ToolTipService.SetToolTip(caption, L(hintKey)));
                Grid.SetRow(caption, row);
                Grid.SetRow(control, row);
                Grid.SetColumn(control, 1);
                fields.Children.Add(caption);
                fields.Children.Add(control);
                if (editor == null) return;
                fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(editor, row + 1);
                Grid.SetColumn(editor, 1);
                fields.Children.Add(editor);
                if (error != null)
                {
                    Grid.SetRow(error, row + 1);
                    fields.Children.Add(error);
                }
            }
            Field("Size_Orientation", "Size_OrientationHint", orientationChips);
            Field("Size_Ratios", "Size_RatiosHint", ratios.Picker, ratios.Editor, ratios.Error);
            Field("Size_LongEdges", "Size_LongEdgesHint", edges.Picker, edges.Editor, edges.Error);
            Field("Size_Fixed", "Size_FixedHint", fixedSizes.Picker, fixedSizes.Editor, fixedSizes.Error);

            _sizeSummary = Text(string.Empty, ResponsiveFontTier.Primary, 0.8);
            _sizeSummary.TextWrapping = TextWrapping.NoWrap;
            _sizeSummary.TextTrimming = TextTrimming.CharacterEllipsis;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_sizeSummary, "SizeSummary");

            var body = new StackPanel { Spacing = 22 };
            body.Children.Add(modes);
            body.Children.Add(_sizeSummary);
            body.Children.Add(fields);
            Localize(UpdateSizeSummary);
            return Section("AppIconRuler", "Section_Dimensions", "Section_DimensionsHint", body);
        }

        /// <summary>A single-select and a checkbox popup share one slot; the model owns both remembered selections.</summary>
        private (Grid Picker, StackPanel Editor, TextBlock Error, Action Refresh) CustomSizeSelect(
            string captionKey,
            string automationId,
            IReadOnlyList<string> presets,
            Func<List<string>> selected,
            Func<List<string>> custom,
            Func<string, bool> acceptsText,
            Func<string, string?> parse,
            Func<string, string> label,
            string example,
            Action<string, bool> onSelect,
            Action<string> onAdd,
            Action<string> onRemove,
            Action changed)
        {
            var select = new DaisySelect { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            FollowPopupTheme(select);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(select, automationId);
            var selectionText = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            var chevron = new Path { Data = ToGeometry(Icon("DaisyIconChevronDown")!), Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            var triggerContent = new Grid { ColumnSpacing = 12 };
            triggerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            triggerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(chevron, 1);
            triggerContent.Children.Add(selectionText);
            triggerContent.Children.Add(chevron);
            var trigger = new Button
            {
                Content = triggerContent, Padding = new Thickness(12, 0, 12, 0), MinWidth = 0, MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            // Both modes use the select's theme and size tokens, including global size changes.
            foreach (var (property, name) in new[]
            {
                (HeightProperty, nameof(select.Height)),
                (Control.BackgroundProperty, nameof(select.Background)),
                (Control.BorderBrushProperty, nameof(select.BorderBrush)),
                (Control.BorderThicknessProperty, nameof(select.BorderThickness)),
                (Control.CornerRadiusProperty, nameof(select.CornerRadius)),
                (Control.ForegroundProperty, nameof(select.Foreground)),
                (Control.FontSizeProperty, nameof(select.FontSize)),
                (Control.FontFamilyProperty, nameof(select.FontFamily)),
                (Control.FontWeightProperty, nameof(select.FontWeight))
            })
                trigger.SetBinding(property, new Binding { Source = select, Path = new PropertyPath(name) });
            chevron.SetBinding(Microsoft.UI.Xaml.Shapes.Shape.FillProperty, new Binding { Source = select, Path = new PropertyPath(nameof(select.Foreground)) });
            chevron.SetBinding(WidthProperty, new Binding { Source = select, Path = new PropertyPath(nameof(select.FontSize)) });
            chevron.SetBinding(HeightProperty, new Binding { Source = select, Path = new PropertyPath(nameof(select.FontSize)) });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(trigger, automationId + "Multiple");
            var checks = new StackPanel { Spacing = 6 };
            var scroll = new ScrollViewer
            {
                Content = checks, MaxHeight = 320,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var popover = new DaisyPopover
            {
                TriggerContent = trigger, PopoverContent = scroll,
                ToggleOnClick = false, IsTabStop = false, HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var picker = new Grid { MinWidth = 0 };
            picker.Children.Add(select);
            picker.Children.Add(popover);
            trigger.Click += (_, _) =>
            {
                scroll.Width = Math.Max(200, picker.ActualWidth - 24);
                popover.IsOpen = !popover.IsOpen;
                SyncPopupThemes(popover);
            };

            // Leave room for both Segoe UI Variable's placeholder and editable text within the Small size token.
            var input = new DimensionInput(acceptsText)
            {
                Width = 125, MinWidth = 0, Size = DaisySize.Small, Padding = new Thickness(12, 3, 12, 3),
                Variant = DaisyInputVariant.Bordered, PlaceholderText = example
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(input, automationId + "Input");
            var add = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, IconSymbol = Symbol.Add };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(add, automationId + "Add");
            Localize(() => add.Content = L("Size_Add"));
            var removeSelected = new DaisyButton { Variant = DaisyButtonVariant.Ghost, Size = DaisySize.Small, Shape = DaisyButtonShape.Square, IconData = Icon("AppIconClose") };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(removeSelected, automationId + "Remove");
            LocalizeTip(removeSelected, "Size_Remove");
            var error = Text(string.Empty, ResponsiveFontTier.Secondary);
            error.Foreground = DaisyResourceLookup.GetBrush("DaisyErrorBrush");
            error.Visibility = Visibility.Collapsed;
            error.TextWrapping = TextWrapping.NoWrap;
            error.HorizontalAlignment = HorizontalAlignment.Right;
            error.VerticalAlignment = VerticalAlignment.Center;
            error.Margin = new Thickness(0, 0, 0, 12);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(error, automationId + "Error");
            Localize(() => error.Text = L("Size_Invalid"));

            List<SelectOption> options = [];
            List<(string Value, DaisyCheckBox Check)> checkItems = [];
            var updating = false;
            void RebuildOptions(List<string> values)
            {
                options = values.Select(value => new SelectOption(value, label(value))).ToList();
                select.ItemsSource = options;
                checks.Children.Clear();
                checkItems.Clear();
                foreach (var option in options)
                {
                    var value = (string)option.Key;
                    var check = new DaisyCheckBox { Content = option.Text, Variant = DaisyCheckBoxVariant.Primary };
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(check, automationId + "_" + value);
                    check.Click += (_, _) => { onSelect(value, check.IsChecked == true); changed(); };
                    checkItems.Add((value, check));
                    var row = new Grid { ColumnSpacing = 8 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(check);
                    if (custom().Contains(value) && !presets.Contains(value))
                    {
                        var remove = new DaisyButton { Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square, Size = DaisySize.ExtraSmall, IconData = Icon("AppIconClose") };
                        Tip(remove, L("Size_Remove"));
                        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(remove, automationId + "Remove_" + value);
                        remove.Click += (_, _) => { onRemove(value); changed(); };
                        Grid.SetColumn(remove, 1);
                        row.Children.Add(remove);
                    }
                    checks.Children.Add(row);
                }
            }
            void Refresh()
            {
                updating = true;
                var values = presets.Concat(custom()).Concat(selected()).Distinct().ToList();
                if (!values.SequenceEqual(options.Select(o => (string)o.Key))
                    || options.Any(o => o.Text != label((string)o.Key)))
                    RebuildOptions(values);
                var chosen = selected();
                select.SelectedItem = options.FirstOrDefault(o => chosen.Contains((string)o.Key));
                select.PlaceholderText = "—";
                select.Visibility = Settings.MultipleSizes ? Visibility.Collapsed : Visibility.Visible;
                popover.Visibility = Settings.MultipleSizes ? Visibility.Visible : Visibility.Collapsed;
                if (!Settings.MultipleSizes) popover.IsOpen = false;
                foreach (var (value, check) in checkItems) check.IsChecked = chosen.Contains(value);
                selectionText.Text = chosen.Count == 0 ? "—" : string.Join(", ", chosen.Select(label));
                ToolTipService.SetToolTip(trigger, selectionText.Text);
                removeSelected.IsEnabled = !Settings.MultipleSizes && chosen.Count == 1
                    && custom().Contains(chosen[0]) && !presets.Contains(chosen[0]);
                updating = false;
            }
            select.SelectionChanged += (_, _) =>
            {
                if (updating || select.SelectedItem is not SelectOption { Key: string value }) return;
                onSelect(value, true);
                changed();
            };
            removeSelected.Click += (_, _) =>
            {
                if (select.SelectedItem is not SelectOption { Key: string value }) return;
                onRemove(value);
                changed();
            };
            Localize(() =>
            {
                select.AccessibleText = L(captionKey);
                Tip(input, L(captionKey));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(trigger, L(captionKey));
                Refresh();
            });

            void Add()
            {
                var value = parse(input.Text ?? string.Empty);
                if (value == null)
                {
                    error.Visibility = Visibility.Visible;
                    return;
                }
                error.Visibility = Visibility.Collapsed;
                if (!presets.Contains(value) && !custom().Contains(value))
                    onAdd(value);
                if (!selected().Contains(value))
                    onSelect(value, true);
                input.Text = string.Empty;
                changed();
            }
            add.Click += (_, _) => Add();
            input.KeyDown += (_, e) =>
            {
                if (e.Key != VirtualKey.Enter) return;
                e.Handled = true;
                Add();
            };
            var editor = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 12) };
            editor.Children.Add(input);
            editor.Children.Add(add);
            editor.Children.Add(removeSelected);
            return (picker, editor, error, Refresh);
        }

        private void UpdateSizeSummary()
        {
            if (_sizeSummary == null)
                return;
            var pool = Settings.BuildSizePool();
            if (pool.Count == 0)
            {
                _sizeSummary.Text = L("Validation_Sizes");
            }
            else if (pool.Count == 1)
            {
                var size = pool[0];
                _sizeSummary.Text = AppLocalization.Format("Size_SelectedSummary", size.Width, size.Height, size.Megapixels);
            }
            else
            {
                var examples = string.Join(", ", pool.OrderBy(s => s.Megapixels).Take(4).Select(s => $"{s.Width}×{s.Height}"));
                _sizeSummary.Text = AppLocalization.Format("Size_Summary", pool.Count, pool.Min(s => s.Megapixels), pool.Max(s => s.Megapixels), examples);
            }
            ToolTipService.SetToolTip(_sizeSummary, _sizeSummary.Text);
        }

        #endregion

        #region Watermark

        private Border? _watermarkSection;

        private Border BuildWatermarkSection()
        {
            var watermark = Settings.Watermark;
            var body = new StackPanel { Spacing = 14 };
            body.Children.Add(LToggleRow("Wm_Apply", watermark.Enabled, v => Settings.Watermark.Enabled = v));
            body.Children.Add(LToggleRow("Wm_InSlideshow", watermark.ShowInSlideshow, v => Settings.Watermark.ShowInSlideshow = v));

            var layers = new StackPanel { Spacing = 6 };
            if (watermark.Layers.Count == 0)
                layers.Children.Add(Text(L("Wm_None"), ResponsiveFontTier.Secondary, 0.6));
            foreach (var layer in Enumerable.Reverse(watermark.Layers))
            {
                var isText = layer.Kind == Core.Watermark.WatermarkLayerKind.Text;
                var description = isText
                    ? AppLocalization.Format("Wm_LayerText", (layer.Text ?? string.Empty).Replace("\n", " "))
                    : AppLocalization.Format("Wm_LayerImage", string.IsNullOrEmpty(layer.ImagePath) ? L("Wm_ImageMissing") : System.IO.Path.GetFileName(layer.ImagePath));
                var line = SummaryLine(isText ? "AppIconTextbox" : "AppIconLandscape", description);
                line.Opacity = layer.Enabled ? 1 : 0.5;
                layers.Children.Add(line);
            }
            body.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush"),
                Child = layers
            });

            var edit = new DaisyButton { Variant = DaisyButtonVariant.Primary, IconData = Icon("AppIconCopyright"), HorizontalAlignment = HorizontalAlignment.Left };
            Localize(() => edit.Content = L("Wm_Edit"));
            edit.Click += (_, _) => _shell?.OpenWatermarkEditor(Settings.Watermark, Settings, result =>
            {
                Settings.Watermark = result;
                OnSettingsChanged();
                RebuildWatermarkSection();
            });
            body.Children.Add(edit);

            _watermarkSection = Section("AppIconCopyright", "Section_Watermark", "Section_WatermarkHint", body);
            return _watermarkSection;
        }

        private void RebuildWatermarkSection()
        {
            if (_watermarkSection == null)
                return;
            var index = Sections.Children.IndexOf(_watermarkSection);
            var section = BuildWatermarkSection();
            if (index >= 0)
                Sections.Children[index] = section;
        }

        #endregion

        #region Output

        private Border BuildOutputSection()
        {
            // Folder
            _folderInput = new DaisyInput { Text = Settings.OutputFolder, Variant = DaisyInputVariant.Bordered, StartIconData = Icon("DaisyIconFolder") };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_folderInput, "OutputFolderInput");
            _folderInput.TextChanged += (_, _) =>
            {
                Settings.OutputFolder = _folderInput.Text?.Trim() ?? string.Empty;
                OnSettingsChanged();
            };
            var browse = new DaisyButton { Variant = DaisyButtonVariant.Primary, IconSymbol = Symbol.Folder };
            Localize(() => browse.Content = L("Output_Browse"));
            browse.Click += async (_, _) =>
            {
                if (_shell == null)
                    return;
                var folder = await _shell.PickFolderAsync();
                if (folder != null)
                    _folderInput.Text = folder;
            };
            var open = new DaisyButton { Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square, IconSymbol = Symbol.OpenLocal };
            LocalizeTip(open, "Output_OpenFolder");
            open.Click += (_, _) => OpenOutputFolder();
            var folderRow = new Grid { ColumnSpacing = 8 };
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(browse, 1);
            Grid.SetColumn(open, 2);
            folderRow.Children.Add(_folderInput);
            folderRow.Children.Add(browse);
            folderRow.Children.Add(open);

            // Name schema
            var nameSelect = new DaisySelect { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
            FollowPopupTheme(nameSelect);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nameSelect, "NamePatternSelect");
            Localize(() => Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nameSelect, L("Output_Name")));
            var removeName = new DaisyButton { Variant = DaisyButtonVariant.Ghost, IconData = Icon("AppIconClose"), Shape = DaisyButtonShape.Square };
            LocalizeTip(removeName, "Output_NameRemove");
            var updatingNames = false;
            void RefreshNames()
            {
                updatingNames = true;
                nameSelect.ItemsSource = Settings.NamePatterns.ToArray();
                nameSelect.SelectedItem = Settings.NamePattern;
                removeName.IsEnabled = Settings.NamePatterns.Count > 1;
                updatingNames = false;
            }
            RefreshNames();
            nameSelect.SelectionChanged += (_, _) =>
            {
                if (updatingNames || nameSelect.SelectedItem is not string pattern) return;
                Settings.NamePattern = pattern;
                if (_nameInput != null) _nameInput.Text = string.Empty;
                OnSettingsChanged();
                UpdateNamePreview();
            };
            removeName.Click += (_, _) =>
            {
                Settings.RemoveNamePattern();
                if (_nameInput != null) _nameInput.Text = string.Empty;
                RefreshNames();
                OnSettingsChanged();
                UpdateNamePreview();
            };
            var nameSelectionRow = new Grid { ColumnSpacing = 8 };
            nameSelectionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameSelectionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(removeName, 1);
            nameSelectionRow.Children.Add(nameSelect);
            nameSelectionRow.Children.Add(removeName);
            _nameInput = new DaisyInput
            {
                Variant = DaisyInputVariant.Bordered,
                StartIconData = Icon("AppIconTextbox"),
                FontFamily = new FontFamily("Cascadia Mono, Consolas")
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_nameInput, "NamePatternDraft");
            Localize(() => _nameInput.PlaceholderText = L("Output_NameNew"));
            _nameInput.TextChanged += (_, _) => UpdateNamePreview();
            var addName = new DaisyButton { Variant = DaisyButtonVariant.Primary, IconSymbol = Symbol.Add };
            Localize(() => addName.Content = L("Size_Add"));
            void AddName()
            {
                if (!Settings.TryAddNamePattern(_nameInput.Text))
                {
                    if (_nameError != null)
                    {
                        _nameError.Text = L("Output_NameAddError");
                        _nameError.Visibility = Visibility.Visible;
                    }
                    return;
                }
                _nameInput.Text = string.Empty;
                RefreshNames();
                OnSettingsChanged();
                UpdateNamePreview();
            }
            addName.Click += (_, _) => AddName();
            _nameInput.KeyDown += (_, e) =>
            {
                if (e.Key != VirtualKey.Enter) return;
                e.Handled = true;
                AddName();
            };
            var nameAddRow = new Grid { ColumnSpacing = 8 };
            nameAddRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameAddRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(addName, 1);
            nameAddRow.Children.Add(_nameInput);
            nameAddRow.Children.Add(addName);
            var tokens = new WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
            foreach (var (token, example) in NameTemplate.Placeholders)
            {
                var button = new DaisyButton
                {
                    Content = token,
                    Variant = DaisyButtonVariant.Primary,
                    ButtonStyle = DaisyButtonStyle.Soft,
                    Size = DaisySize.ExtraSmall,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas")
                };
                var key = $"Name_Token_{token.Trim('{', '}')}";
                Localize(() => ToolTipService.SetToolTip(button, $"{example} — {L(key)}"));
                button.Click += (_, _) => InsertToken(example);
                tokens.Children.Add(button);
            }
            _nameError = Text(string.Empty, ResponsiveFontTier.Secondary);
            _nameError.Foreground = DaisyResourceLookup.GetBrush("DaisyErrorBrush");
            _nameExamples = new StackPanel { Spacing = 4 };
            var nameBlock = new StackPanel { Spacing = 10 };
            nameBlock.Children.Add(nameSelectionRow);
            nameBlock.Children.Add(nameAddRow);
            nameBlock.Children.Add(tokens);
            nameBlock.Children.Add(_nameError);
            nameBlock.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush"),
                Child = _nameExamples
            });

            // Format & quality
            (OutputFormat, Func<string>)[] formats = [.. Enum.GetValues<OutputFormat>().Select(f => (f, (Func<string>)(() => FormatName(f))))];
            var qualityValue = Text(string.Empty, ResponsiveFontTier.Primary, 0.7);
            qualityValue.Text = $"{Settings.Quality} %";
            var quality = Range(40, 100, Settings.Quality, 1, v =>
            {
                Settings.Quality = (int)Math.Round(v);
                qualityValue.Text = $"{Settings.Quality} %";
                OnSettingsChanged();
            });
            var qualityRow = new Grid { ColumnSpacing = 12 };
            qualityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            qualityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(qualityValue, 1);
            qualityRow.Children.Add(quality);
            qualityRow.Children.Add(qualityValue);
            var formatBlock = new StackPanel { Spacing = 12 };
            formatBlock.Children.Add(ChipGroup(formats, () => Settings.Formats, UpdateNamePreview));
            formatBlock.Children.Add(Block("Output_Quality", qualityRow, "Output_QualityHint"));

            // Dates & metadata
            var dateRange = BuildDateRangeEditor();
            dateRange.Visibility = Settings.SpreadDates ? Visibility.Visible : Visibility.Collapsed;
            var dates = new StackPanel { Spacing = 10 };
            var spreadRow = LToggleRow("Output_SpreadDates", Settings.SpreadDates, v =>
            {
                Settings.SpreadDates = v;
                dateRange.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
                UpdateNamePreview();
            });
            AutomationProperties.SetAutomationId(spreadRow.Children[1], "SpreadDates");
            Localize(() => AutomationProperties.SetName(spreadRow.Children[1], L("Output_SpreadDates")));
            dates.Children.Add(spreadRow);
            var spreadHint = Text(string.Empty, ResponsiveFontTier.Secondary, 0.6);
            Localize(() => spreadHint.Text = L("Output_SpreadDatesHint"));
            dates.Children.Add(spreadHint);
            dates.Children.Add(dateRange);
            dates.Children.Add(LToggleRow("Output_WriteExif", Settings.WriteExif, v => Settings.WriteExif = v));
            dates.Children.Add(LToggleRow("Output_Overwrite", Settings.Overwrite, v => Settings.Overwrite = v));

            var start = Number(0, 999_999_999, Settings.StartIndex, v =>
            {
                Settings.StartIndex = v;
                OnSettingsChanged();
                UpdateNamePreview();
            }, 160);

            // Reference date: empty means the day of the run; a malformed date is shown as an error and not applied.
            var referenceDate = new DaisyInput
            {
                Variant = DaisyInputVariant.Bordered,
                Text = Settings.ReferenceDate?.ToString(ReferenceDateFormat, CultureInfo.InvariantCulture) ?? string.Empty,
                Width = 160,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Localize(() => referenceDate.PlaceholderText = L("Output_ReferenceDateToday"));
            referenceDate.TextChanged += (_, _) =>
            {
                var text = referenceDate.Text?.Trim() ?? string.Empty;
                var valid = DateOnly.TryParseExact(text, ReferenceDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                referenceDate.Variant = valid || text.Length == 0 ? DaisyInputVariant.Bordered : DaisyInputVariant.Error;
                if (!valid && text.Length > 0)
                    return;
                Settings.ReferenceDate = valid ? date : null;
                OnSettingsChanged();
                UpdateNamePreview();
            };
            _referenceDateInput = referenceDate;

            var body = new StackPanel { Spacing = 22 };
            body.Children.Add(Block("Output_Folder", folderRow));
            body.Children.Add(Block("Output_Name", nameBlock, "Output_NameHint"));
            body.Children.Add(Columns(300 * SizeFactor,
                Block("Output_Format", formatBlock),
                Block("Output_Files", dates)));
            body.Children.Add(InlineField("Output_StartIndex", start, "Output_StartIndexHint"));
            body.Children.Add(InlineField("Output_ReferenceDate", referenceDate, "Output_ReferenceDateHint"));
            UpdateNamePreview();
            return Section("DaisyIconFolder", "Section_Output", "Section_OutputHint", body);
        }

        private Grid BuildDateRangeEditor()
        {
            var grid = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AutomationProperties.SetAutomationId(grid, "SpreadDateRange");
            var updating = false;
            List<Action> refreshers = [];

            void SetDate(bool from, DateOnly value)
            {
                if (updating) return;
                if (from)
                {
                    Settings.SpreadFrom = value;
                    if (Settings.SpreadTo < value) Settings.SpreadTo = value;
                }
                else
                {
                    Settings.SpreadTo = value;
                    if (Settings.SpreadFrom > value) Settings.SpreadFrom = value;
                }
                updating = true;
                foreach (var refresh in refreshers) refresh();
                updating = false;
                OnSettingsChanged();
                UpdateNamePreview();
            }

            void AddRow(bool from)
            {
                DateOnly Current() => from ? Settings.SpreadFrom : Settings.SpreadTo;
                var prefix = from ? "SpreadFrom" : "SpreadTo";
                var labelKey = from ? "Output_DateFrom" : "Output_DateTo";
                var label = Text(string.Empty, ResponsiveFontTier.Primary, 0.8);
                var month = new DaisySelect { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
                FollowPopupTheme(month);
                var year = Number(1601, 9999, Current().Year,
                    value => SetDate(from, new DateOnly(value, Current().Month, 1)), 110);
                AutomationProperties.SetAutomationId(month, prefix + "Month");
                AutomationProperties.SetAutomationId(year, prefix + "Year");
                refreshers.Add(() => { month.SelectedIndex = Current().Month - 1; year.Value = Current().Year; });
                Localize(() =>
                {
                    updating = true;
                    label.Text = L(labelKey);
                    var format = (DateTimeFormatInfo)AppLocalization.CurrentCulture.DateTimeFormat.Clone();
                    format.Calendar = new GregorianCalendar();
                    month.ItemsSource = Enumerable.Range(1, 12).Select(format.GetMonthName).ToList();
                    month.SelectedIndex = Current().Month - 1;
                    AutomationProperties.SetName(month, $"{L(labelKey)}: {L("Output_Month")}");
                    AutomationProperties.SetName(year, $"{L(labelKey)}: {L("Output_Year")}");
                    updating = false;
                });
                month.SelectionChanged += (_, _) =>
                {
                    if (!updating && month.SelectedIndex >= 0)
                        SetDate(from, new DateOnly(Current().Year, month.SelectedIndex + 1, 1));
                };
                var row = grid.RowDefinitions.Count;
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                FrameworkElement[] fields = [label, month, year];
                for (var column = 0; column < fields.Length; column++)
                {
                    Grid.SetRow(fields[column], row);
                    Grid.SetColumn(fields[column], column);
                    grid.Children.Add(fields[column]);
                }
            }

            AddRow(true);
            AddRow(false);
            return grid;
        }

        private void InsertToken(string token)
        {
            if (_nameInput == null)
                return;
            var text = _nameInput.Text ?? string.Empty;
            var caret = Math.Clamp(_nameInput.SelectionStart, 0, text.Length);
            var length = Math.Clamp(_nameInput.SelectionLength, 0, text.Length - caret);
            _nameInput.Text = text[..caret] + token + text[(caret + length)..];
            _nameInput.Focus(FocusState.Programmatic);
            _nameInput.SelectionStart = caret + token.Length;
            _nameInput.SelectionLength = 0;
        }

        /// <summary>Shows three example file names using real recipes of the current settings.</summary>
        private void UpdateNamePreview()
        {
            if (_nameExamples == null || _nameError == null)
                return;
            _nameExamples.Children.Clear();
            var pattern = string.IsNullOrWhiteSpace(_nameInput?.Text) ? Settings.NamePattern : _nameInput.Text.Trim();
            if (!NameTemplate.TryParse(pattern, out var template, out var error) || template == null)
            {
                _nameError.Text = AppLocalization.Format("Output_NameError", error ?? string.Empty);
                _nameError.Visibility = Visibility.Visible;
                return;
            }
            _nameError.Text = template.IsUnique ? string.Empty : L("Output_NameNotUnique");
            _nameError.Visibility = template.IsUnique ? Visibility.Collapsed : Visibility.Visible;

            var styles = Settings.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
            var sizes = Settings.BuildSizePool();
            if (styles.Count == 0 || sizes.Count == 0 || (Settings.UsePaletteSet ? !Settings.PaletteSet.IsValid() : Settings.Palettes.Count == 0 || Settings.Tones.Count == 0)
                || Settings.Details.Count == 0 || Settings.Formats.Count == 0)
            {
                _nameExamples.Children.Add(Text(L("Output_NamePreviewUnavailable"), ResponsiveFontTier.Secondary, 0.6));
                return;
            }
            var now = Settings.ReferenceTime();
            var master = ImageGenerator.ParseSeed(string.IsNullOrWhiteSpace(Settings.Seed) ? "preview" : Settings.Seed);
            for (var i = 0; i < 3; i++)
            {
                var seed = ImageGenerator.ImageSeed(master, i);
                var recipe = ImageGenerator.CreateRecipe(Settings, styles, sizes, seed, Settings.StartIndex + i, now);
                var palette = ImageGenerator.CreatePalette(recipe);
                var values = ImageGenerator.CreateNameValues(recipe, Settings.Count, ImageGenerator.PaletteSlug(palette), now);
                var name = template.Format(values, new Random(seed)) + Core.Output.ImageEncoder.Extension(recipe.Format);
                var line = Text(name, ResponsiveFontTier.Secondary, 0.85);
                line.FontFamily = new FontFamily("Cascadia Mono, Consolas");
                line.TextWrapping = TextWrapping.NoWrap;
                line.TextTrimming = TextTrimming.CharacterEllipsis;
                _nameExamples.Children.Add(line);
            }
        }

        private void OpenOutputFolder()
        {
            var folder = Settings.OutputFolder;
            try
            {
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);
                _ = Launcher.LaunchFolderPathAsync(folder);
            }
            catch (Exception ex)
            {
                _shell?.ShowToast(ex.Message, DaisyAlertVariant.Error);
            }
        }

        #endregion
    }
}
