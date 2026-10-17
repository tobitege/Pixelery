using Pixelery.Core.Styles;
using Pixelery.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Windows.System;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
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
        private readonly Path _checkMark;
        private readonly Button _previewButton;
        private readonly TextBlock _name;
        private readonly TextBlock _description;
        private readonly Grid _layout;
        private readonly StackPanel _text;
        private StyleViewMode _viewMode;
        private bool _isSelected;
        private bool _isHovered;

        public StyleCard(ArtStyle style)
        {
            Style = style;
            IsTabStop = true;
            UseSystemFocusVisuals = true;
            AutomationProperties.SetAutomationId(this, $"Style_{style.Id}");
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

            _image = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
            _placeholder = new DaisySkeleton { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            _checkMark = new Path { Data = ToGeometry(Icon("AppIconCheck")!) };
            _check = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new Viewbox
                {
                    Margin = new Thickness(5),
                    Child = _checkMark
                }
            };
            _previewButton = new Button
            {
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                UseSystemFocusVisuals = false,
                CornerRadius = new CornerRadius(22),
                Content = new Viewbox
                {
                    Width = 22,
                    Height = 22,
                    Child = new PathIcon { Data = ToGeometry(Icon("AppIconView")!), Width = 24, Height = 24 }
                },
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Opacity = 0.9
            };
            AutomationProperties.SetAutomationId(_previewButton, $"Preview_{style.Id}");
            _previewButton.GotFocus += (_, _) => UpdateVisual();
            _previewButton.LostFocus += (_, _) => UpdateVisual();
            _previewButton.Click += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);

            var imageGrid = new Grid();
            imageGrid.Children.Add(_placeholder);
            imageGrid.Children.Add(_image);
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

            _text = new StackPanel { Spacing = 2 };
            _text.Children.Add(_name);
            _text.Children.Add(_description);

            _layout = new Grid();
            _layout.Children.Add(_imageHost);
            _layout.Children.Add(_text);
            _layout.Children.Add(_check);
            _layout.Children.Add(_previewButton);

            _frame = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = _layout
            };
            Content = _frame;

            SizeChanged += (_, e) =>
            {
                if (_viewMode == StyleViewMode.Cards)
                    _imageHost.Height = Math.Round(e.NewSize.Width * 10 / 16);
            };
            PointerEntered += (_, _) => SetHover(true);
            PointerExited += (_, _) => SetHover(false);
            PointerCanceled += (_, _) => SetHover(false);
            Tapped += OnTapped;
            KeyDown += OnKeyDown;

            UpdateTexts();
            UpdateVisual();
            ApplyViewMode(StyleViewMode.Cards);
        }

        public void ApplyViewMode(StyleViewMode mode)
        {
            _viewMode = mode;
            var cards = mode == StyleViewMode.Cards;
            var textOnly = mode == StyleViewMode.TextOnly;
            var f = SizeFactor;
            _layout.RowDefinitions.Clear();
            _layout.ColumnDefinitions.Clear();
            _layout.Padding = cards ? new Thickness(0) : new Thickness(8, textOnly ? 4 : 6, 8, textOnly ? 4 : 6);
            _layout.ColumnSpacing = cards ? 0 : textOnly ? 8 : 10;
            _layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (cards)
                _layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            else
            {
                _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                if (!textOnly)
                    _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            Grid.SetRow(_text, cards ? 1 : 0);
            Grid.SetColumn(_imageHost, cards ? 0 : 1);
            Grid.SetColumn(_text, cards ? 0 : textOnly ? 1 : 2);
            Grid.SetColumn(_previewButton, cards ? 0 : textOnly ? 2 : 3);
            _imageHost.Visibility = mode is StyleViewMode.TextList or StyleViewMode.TextOnly ? Visibility.Collapsed : Visibility.Visible;
            _imageHost.Width = cards ? double.NaN : 108 * f;
            _imageHost.Height = cards ? Math.Round(ActualWidth * 10 / 16) : 32 * f;
            _imageHost.VerticalAlignment = cards ? VerticalAlignment.Stretch : VerticalAlignment.Center;
            _imageHost.CornerRadius = cards ? new CornerRadius(10, 10, 0, 0) : new CornerRadius(4);
            _text.Padding = cards ? new Thickness(12, 10, 12, 12) : new Thickness(0);
            _text.VerticalAlignment = VerticalAlignment.Center;
            _description.TextWrapping = cards ? TextWrapping.Wrap : TextWrapping.NoWrap;
            _description.MaxLines = cards ? 2 : 1;
            _description.Visibility = textOnly ? Visibility.Collapsed : Visibility.Visible;
            _check.Width = _check.Height = textOnly ? 20 * f : 26;
            _check.CornerRadius = new CornerRadius(_check.Width / 2);
            _check.Margin = cards ? new Thickness(8) : new Thickness(0);
            _check.HorizontalAlignment = cards ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            _check.VerticalAlignment = cards ? VerticalAlignment.Top : VerticalAlignment.Center;
            _previewButton.Margin = cards ? new Thickness(8) : new Thickness(0);
            _previewButton.VerticalAlignment = cards ? VerticalAlignment.Bottom : VerticalAlignment.Center;
            _previewButton.Width = _previewButton.Height = cards ? 44 : 32;
            _previewButton.CornerRadius = new CornerRadius(_previewButton.Width / 2);
            var previewIcon = (Viewbox)_previewButton.Content;
            previewIcon.Width = previewIcon.Height = cards ? 22 : 18;
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
            var primary = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
            _name.Foreground = content;
            _description.Foreground = content;
            var previewKeyboardFocus = _previewButton.FocusState == FocusState.Keyboard;
            _previewButton.Foreground = previewKeyboardFocus ? DaisyResourceLookup.GetBrush("DaisyPrimaryContentBrush") : content;
            _previewButton.Background = previewKeyboardFocus ? primary : DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            _frame.Background = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            _frame.BorderBrush = _isSelected || _isHovered ? primary : DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            _checkMark.Fill = DaisyResourceLookup.GetBrush("DaisyPrimaryContentBrush");
            _check.Background = _isSelected ? primary : DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            _check.BorderBrush = _isSelected ? primary : content;
            _check.BorderThickness = _isSelected ? new Thickness(0) : new Thickness(1);
            _check.Child.Visibility = _isSelected ? Visibility.Visible : Visibility.Collapsed;
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
            if (!ReferenceEquals(e.OriginalSource, this))
                return;
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
