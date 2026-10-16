using AbstractImg.Core.Styles;
using AbstractImg.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Windows.System;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>
    /// Selectable card for one art style: live preview thumbnail, name and description.
    /// Clicking toggles the selection; the eye button opens a large preview.
    /// </summary>
    public sealed partial class StyleCard : UserControl
    {
        private readonly Border _frame;
        private readonly Border _imageHost;
        private readonly Image _image;
        private readonly DaisySkeleton _placeholder;
        private readonly Border _check;
        private readonly DaisyButton _previewButton;
        private readonly TextBlock _name;
        private readonly TextBlock _description;
        private bool _isSelected;
        private bool _isHovered;

        public StyleCard(ArtStyle style)
        {
            Style = style;
            IsTabStop = true;
            UseSystemFocusVisuals = true;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

            _image = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
            _placeholder = new DaisySkeleton { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            _check = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = WindowsAccent.Brush,
                Child = new Viewbox
                {
                    Margin = new Thickness(5),
                    Child = new Path { Data = ToGeometry(Icon("AppIconCheck")!), Fill = new SolidColorBrush(Colors.White) }
                }
            };
            _previewButton = new DaisyButton
            {
                Variant = DaisyButtonVariant.Neutral,
                Shape = DaisyButtonShape.Circle,
                Size = DaisySize.Small,
                IconData = Icon("AppIconView"),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Opacity = 0.9
            };
            FlowerySizeManager.SetIgnoreGlobalSize(_previewButton, true);
            _previewButton.Click += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);

            var imageGrid = new Grid();
            imageGrid.Children.Add(_placeholder);
            imageGrid.Children.Add(_image);
            imageGrid.Children.Add(_check);
            imageGrid.Children.Add(_previewButton);
            _imageHost = new Border { CornerRadius = new CornerRadius(10, 10, 0, 0), Child = imageGrid };

            _name = Responsive(new TextBlock
            {
                Text = style.Name,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            }, ResponsiveFontTier.Primary);
            _description = Responsive(new TextBlock
            {
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            }, ResponsiveFontTier.Secondary);

            var text = new StackPanel { Spacing = 2, Padding = new Thickness(12, 10, 12, 12) };
            text.Children.Add(_name);
            text.Children.Add(_description);

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(text, 1);
            layout.Children.Add(_imageHost);
            layout.Children.Add(text);

            _frame = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(2),
                Child = layout
            };
            Content = _frame;

            SizeChanged += (_, e) => _imageHost.Height = Math.Round(e.NewSize.Width * 10 / 16);
            PointerEntered += (_, _) => SetHover(true);
            PointerExited += (_, _) => SetHover(false);
            PointerCanceled += (_, _) => SetHover(false);
            Tapped += OnTapped;
            KeyDown += OnKeyDown;

            UpdateTexts();
            UpdateVisual();
        }

        public new ArtStyle Style { get; }

        public event EventHandler? Toggled;

        public event EventHandler? PreviewRequested;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                UpdateVisual();
            }
        }

        public void SetImage(ImageSource source)
        {
            _image.Source = source;
            _image.Opacity = _isSelected ? 1 : 0.5;
            _placeholder.Visibility = Visibility.Collapsed;
        }

        public void UpdateTexts()
        {
            _description.Text = L($"Style_{Style.Id}");
            ToolTipService.SetToolTip(_previewButton, L("Styles_Preview"));
            AutomationProperties.SetName(_previewButton, L("Styles_Preview"));
            AutomationProperties.SetName(this, $"{Style.Name}, {_description.Text}");
        }

        /// <summary>Re-reads theme brushes (after a theme change) and applies the selection state.</summary>
        public void UpdateVisual()
        {
            var content = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
            _name.Foreground = content;
            _description.Foreground = content;
            _frame.Background = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            if (_isSelected)
            {
                _frame.BorderBrush = WindowsAccent.Brush;
            }
            else if (_isHovered)
            {
                var accent = WindowsAccent.Color;
                _frame.BorderBrush = new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B));
            }
            else
            {
                _frame.BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            }
            _check.Visibility = _isSelected ? Visibility.Visible : Visibility.Collapsed;
            if (_image.Source != null)
                _image.Opacity = _isSelected || _isHovered ? 1 : 0.5;
            _name.Opacity = _isSelected ? 1 : 0.75;
            AutomationProperties.SetItemStatus(this, _isSelected ? L("Styles_Selected") : string.Empty);
        }

        private void SetHover(bool hovered)
        {
            _isHovered = hovered;
            _previewButton.Opacity = hovered ? 1 : 0.85;
            UpdateVisual();
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            // The preview button handles its own click.
            for (var element = e.OriginalSource as DependencyObject; element != null && element != this; element = VisualTreeHelper.GetParent(element))
            {
                if (element == _previewButton)
                    return;
            }
            Toggle();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Space or VirtualKey.Enter)
            {
                e.Handled = true;
                Toggle();
            }
        }

        private void Toggle()
        {
            IsSelected = !IsSelected;
            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }
}
