using AbstractImg.Core.Generation;
using AbstractImg.Core.Palettes;
using AbstractImg.Core.Styles;
using AbstractImg.Localization;
using AbstractImg.Services;
using Windows.System;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>Colors, dimensions and output sections, built from small chip, block and section helpers.</summary>
    public sealed partial class GeneratorView
    {
        private readonly List<(DaisyButton Chip, Func<bool> IsSelected)> _chips = [];
        private WrapPanel? _paletteSamples;
        private int _paletteSampleSeed = Random.Shared.Next();
        private TextBlock? _sizeSummary;
        private DaisyInput? _folderInput;
        private DaisyInput? _nameInput;
        private TextBlock? _nameError;
        private StackPanel? _nameExamples;

        private void BuildSections()
        {
            Sections.Children.Clear();
            Sections.Children.Add(BuildStylesSection());
            Sections.Children.Add(BuildColorsSection());
            Sections.Children.Add(BuildDimensionsSection());
            Sections.Children.Add(BuildOutputSection());
            RefreshPreviews();
        }

        #region Building blocks

        private Border Section(string iconKey, string titleKey, string? hintKey, UIElement body, UIElement? headerRight = null)
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

            var stack = new StackPanel { Spacing = 18 };
            stack.Children.Add(header);
            stack.Children.Add(body);
            return new Border { Style = (Style)Application.Current.Resources["AppSectionStyle"], Child = stack };
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
                UpdatePaletteSamples();
                SchedulePreviewRefresh();
            }

            _paletteSamples = new WrapPanel { HorizontalSpacing = 10, VerticalSpacing = 10 };
            var sampleShuffle = new DaisyButton
            {
                Variant = DaisyButtonVariant.Ghost,
                Shape = DaisyButtonShape.Square,
                Size = DaisySize.Small,
                IconData = Icon("AppIconShuffle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            LocalizeTip(sampleShuffle, "Colors_ShuffleSamples");
            sampleShuffle.Click += (_, _) =>
            {
                _paletteSampleSeed = Random.Shared.Next();
                UpdatePaletteSamples();
            };
            var samplesRow = new Grid { ColumnSpacing = 8 };
            samplesRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            samplesRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(sampleShuffle, 1);
            samplesRow.Children.Add(_paletteSamples);
            samplesRow.Children.Add(sampleShuffle);

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
            body.Children.Add(Block("Colors_Samples", samplesRow));
            body.Children.Add(Columns(300 * SizeFactor,
                Block("Colors_Tones", ChipGroup(tones, () => Settings.Tones, ColorsChanged)),
                Block("Colors_Detail", ChipGroup(details, () => Settings.Details, SchedulePreviewRefresh)),
                Block("Colors_Finish", finish)));

            UpdatePaletteSamples();
            return Section("AppIconPalette", "Section_Colors", "Section_ColorsHint", body);
        }

        private void UpdatePaletteSamples()
        {
            if (_paletteSamples == null)
                return;
            _paletteSamples.Children.Clear();
            if (Settings.Palettes.Count == 0 || Settings.Tones.Count == 0)
            {
                _paletteSamples.Children.Add(Text(L("Validation_Palettes"), ResponsiveFontTier.Secondary, 0.6));
                return;
            }
            var rng = new Random(_paletteSampleSeed);
            var count = 8;
            for (var i = 0; i < count; i++)
            {
                var family = Settings.Palettes[rng.Next(Settings.Palettes.Count)];
                var tone = Settings.Tones[rng.Next(Settings.Tones.Count)];
                var palette = PaletteFactory.Create(new Random(rng.Next()), family, tone);
                var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
                foreach (var color in palette.Colors)
                {
                    strip.Children.Add(new Border
                    {
                        Width = 16 * SizeFactor,
                        Height = 26 * SizeFactor,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(Color.FromArgb(255, color.Red, color.Green, color.Blue))
                    });
                }
                var bg = palette.Background;
                var frame = new Border
                {
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush"),
                    Background = new SolidColorBrush(Color.FromArgb(255, bg.Red, bg.Green, bg.Blue)),
                    Child = strip
                };
                ToolTipService.SetToolTip(frame, $"{palette.Name} · {L($"Palette_{family}")} · {L($"Tone_{tone}")}");
                _paletteSamples.Children.Add(frame);
            }
        }

        #endregion

        #region Dimensions

        private Border BuildDimensionsSection()
        {
            (ImageOrientation, Func<string>)[] orientations =
            [
                (ImageOrientation.Landscape, () => L("Size_Landscape")),
                (ImageOrientation.Portrait, () => L("Size_Portrait"))
            ];

            var ratios = CustomChipGroup(
                SizeCatalog.AspectRatios,
                () => Settings.AspectRatios,
                () => Settings.CustomAspectRatios,
                text => SizeCatalog.TryParseRatio(text, out var w, out var h) ? SizeCatalog.NormalizeRatio(w, h) : null,
                value => value,
                "16:9");
            var edges = CustomChipGroup(
                SizeCatalog.LongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                () => Settings.LongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                () => Settings.CustomLongEdges.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToList(),
                text => SizeCatalog.TryParseEdge(text, out var edge) ? edge.ToString(CultureInfo.InvariantCulture) : null,
                value => $"{int.Parse(value, CultureInfo.InvariantCulture):N0} px",
                "2048",
                onSelect: (value, add) =>
                {
                    var edge = int.Parse(value, CultureInfo.InvariantCulture);
                    Settings.LongEdges.Remove(edge);
                    if (add)
                        Settings.LongEdges.Add(edge);
                },
                onCustom: (value, add) =>
                {
                    var edge = int.Parse(value, CultureInfo.InvariantCulture);
                    Settings.CustomLongEdges.Remove(edge);
                    if (add)
                        Settings.CustomLongEdges.Add(edge);
                });
            var fixedSizes = CustomChipGroup(
                SizeCatalog.FixedSizes.Select(p => p.Value).ToList(),
                () => Settings.FixedSizes,
                () => Settings.CustomFixedSizes,
                text => SizeCatalog.TryParseSize(text, out var w, out var h) ? $"{w}x{h}" : null,
                value =>
                {
                    var preset = SizeCatalog.FixedSizes.FirstOrDefault(p => p.Value == value);
                    var dims = value.Replace("x", " × ", StringComparison.Ordinal);
                    return preset.Label != null ? $"{preset.Label} · {dims}" : dims;
                },
                "1600x900");

            _sizeSummary = Text(string.Empty, ResponsiveFontTier.Primary, 0.8);

            var body = new StackPanel { Spacing = 22 };
            body.Children.Add(Columns(300 * SizeFactor,
                Block("Size_Orientation", ChipGroup(orientations, () => Settings.Orientations, UpdateSizeSummary), "Size_OrientationHint"),
                Block("Size_Ratios", ratios, "Size_RatiosHint")));
            body.Children.Add(Block("Size_LongEdges", edges, "Size_LongEdgesHint"));
            body.Children.Add(Block("Size_Fixed", fixedSizes, "Size_FixedHint"));
            body.Children.Add(_sizeSummary);
            UpdateSizeSummary();
            return Section("AppIconRuler", "Section_Dimensions", "Section_DimensionsHint", body);
        }

        /// <summary>
        /// Preset chips plus user-defined chips (removable) and an input to add more. Values are strings;
        /// optional callbacks map them to typed settings lists.
        /// </summary>
        private StackPanel CustomChipGroup(
            IReadOnlyList<string> presets,
            Func<List<string>> selected,
            Func<List<string>> custom,
            Func<string, string?> parse,
            Func<string, string> label,
            string example,
            Action<string, bool>? onSelect = null,
            Action<string, bool>? onCustom = null)
        {
            var chips = new WrapPanel();
            var input = new DaisyInput { Width = 150, Size = DaisySize.Small, Variant = DaisyInputVariant.Bordered, PlaceholderText = example };
            var add = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, IconSymbol = Symbol.Add };
            Localize(() => add.Content = L("Size_Add"));
            var error = Text(string.Empty, ResponsiveFontTier.Secondary);
            error.Foreground = DaisyResourceLookup.GetBrush("DaisyErrorBrush");
            error.Visibility = Visibility.Collapsed;

            void Select(string value, bool on)
            {
                if (onSelect != null)
                {
                    onSelect(value, on);
                    return;
                }
                var list = selected();
                list.Remove(value);
                if (on)
                    list.Add(value);
            }

            void SetCustom(string value, bool on)
            {
                if (onCustom != null)
                {
                    onCustom(value, on);
                    return;
                }
                var list = custom();
                list.Remove(value);
                if (on)
                    list.Add(value);
            }

            void Rebuild()
            {
                chips.Children.Clear();
                foreach (var value in presets.Concat(custom().Where(c => !presets.Contains(c))))
                {
                    var item = value;
                    var chip = Chip(() => label(item), () => selected().Contains(item), () =>
                    {
                        Select(item, !selected().Contains(item));
                        OnSettingsChanged();
                        UpdateSizeSummary();
                    });
                    if (presets.Contains(item))
                    {
                        chips.Children.Add(chip);
                        continue;
                    }
                    var remove = new DaisyButton
                    {
                        Variant = DaisyButtonVariant.Ghost,
                        Shape = DaisyButtonShape.Circle,
                        Size = DaisySize.ExtraSmall,
                        IconData = Icon("AppIconClose"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Tip(remove, L("Size_Remove"));
                    remove.Click += (_, _) =>
                    {
                        SetCustom(item, false);
                        Select(item, false);
                        Rebuild();
                        OnSettingsChanged();
                        UpdateSizeSummary();
                    };
                    var pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
                    pair.Children.Add(chip);
                    pair.Children.Add(remove);
                    chips.Children.Add(pair);
                }
            }

            void Add()
            {
                var value = parse(input.Text ?? string.Empty);
                if (value == null)
                {
                    error.Text = AppLocalization.Format("Size_Invalid", example);
                    error.Visibility = Visibility.Visible;
                    return;
                }
                error.Visibility = Visibility.Collapsed;
                if (!presets.Contains(value) && !custom().Contains(value))
                    SetCustom(value, true);
                if (!selected().Contains(value))
                    Select(value, true);
                input.Text = string.Empty;
                Rebuild();
                OnSettingsChanged();
                UpdateSizeSummary();
            }

            add.Click += (_, _) => Add();
            input.KeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    Add();
                }
            };

            Rebuild();
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            addRow.Children.Add(input);
            addRow.Children.Add(add);
            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(chips);
            stack.Children.Add(addRow);
            stack.Children.Add(error);
            return stack;
        }

        private void UpdateSizeSummary()
        {
            if (_sizeSummary == null)
                return;
            var pool = Settings.BuildSizePool();
            if (pool.Count == 0)
            {
                _sizeSummary.Text = L("Validation_Sizes");
                return;
            }
            var examples = string.Join(", ", pool.OrderBy(s => s.Megapixels).Take(4).Select(s => $"{s.Width}×{s.Height}"));
            _sizeSummary.Text = AppLocalization.Format("Size_Summary", pool.Count, pool.Min(s => s.Megapixels), pool.Max(s => s.Megapixels), examples);
        }

        #endregion

        #region Output

        private Border BuildOutputSection()
        {
            // Folder
            _folderInput = new DaisyInput { Text = Settings.OutputFolder, Variant = DaisyInputVariant.Bordered, StartIconData = Icon("DaisyIconFolder") };
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
            _nameInput = new DaisyInput
            {
                Text = Settings.NamePattern,
                Variant = DaisyInputVariant.Bordered,
                StartIconData = Icon("AppIconTextbox"),
                FontFamily = new FontFamily("Cascadia Mono, Consolas")
            };
            _nameInput.TextChanged += (_, _) =>
            {
                Settings.NamePattern = _nameInput.Text ?? string.Empty;
                OnSettingsChanged();
                UpdateNamePreview();
            };
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
            nameBlock.Children.Add(_nameInput);
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
            var years = Number(1, 50, Settings.SpreadYears, v =>
            {
                Settings.SpreadYears = v;
                OnSettingsChanged();
            }, 120);
            var yearsRow = new Grid { ColumnSpacing = 10 };
            yearsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            yearsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var yearsLabel = Text(string.Empty, ResponsiveFontTier.Primary, 0.8);
            Localize(() => yearsLabel.Text = L("Output_SpreadYears"));
            Grid.SetColumn(yearsLabel, 1);
            yearsRow.Children.Add(years);
            yearsRow.Children.Add(yearsLabel);
            yearsRow.Visibility = Settings.SpreadDates ? Visibility.Visible : Visibility.Collapsed;
            var dates = new StackPanel { Spacing = 10 };
            dates.Children.Add(LToggleRow("Output_SpreadDates", Settings.SpreadDates, v =>
            {
                Settings.SpreadDates = v;
                yearsRow.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
                UpdateNamePreview();
            }));
            dates.Children.Add(yearsRow);
            dates.Children.Add(LToggleRow("Output_WriteExif", Settings.WriteExif, v => Settings.WriteExif = v));
            dates.Children.Add(LToggleRow("Output_Overwrite", Settings.Overwrite, v => Settings.Overwrite = v));

            var start = Number(0, 999_999_999, Settings.StartIndex, v =>
            {
                Settings.StartIndex = v;
                OnSettingsChanged();
                UpdateNamePreview();
            }, 160);

            var body = new StackPanel { Spacing = 22 };
            body.Children.Add(Block("Output_Folder", folderRow));
            body.Children.Add(Block("Output_Name", nameBlock, "Output_NameHint"));
            body.Children.Add(Columns(300 * SizeFactor,
                Block("Output_Format", formatBlock),
                Block("Output_Files", dates),
                Block("Output_StartIndex", start, "Output_StartIndexHint")));
            UpdateNamePreview();
            return Section("DaisyIconFolder", "Section_Output", "Section_OutputHint", body);
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
            if (!NameTemplate.TryParse(Settings.NamePattern, out var template, out var error) || template == null)
            {
                _nameError.Text = AppLocalization.Format("Output_NameError", error ?? string.Empty);
                _nameError.Visibility = Visibility.Visible;
                return;
            }
            _nameError.Text = template.IsUnique ? string.Empty : L("Output_NameNotUnique");
            _nameError.Visibility = template.IsUnique ? Visibility.Collapsed : Visibility.Visible;

            var styles = Settings.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
            var sizes = Settings.BuildSizePool();
            if (styles.Count == 0 || sizes.Count == 0 || Settings.Palettes.Count == 0 || Settings.Tones.Count == 0
                || Settings.Details.Count == 0 || Settings.Formats.Count == 0)
            {
                _nameExamples.Children.Add(Text(L("Output_NamePreviewUnavailable"), ResponsiveFontTier.Secondary, 0.6));
                return;
            }
            var now = DateTime.Now;
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
