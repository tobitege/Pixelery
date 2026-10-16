using System.Threading;
using System.Threading.Tasks;
using AbstractImg.Core.Generation;
using AbstractImg.Core.Output;
using AbstractImg.Core.Palettes;
using AbstractImg.Core.Styles;
using AbstractImg.Localization;
using AbstractImg.Services;
using Microsoft.UI.Xaml.Automation;
using Windows.System;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>
    /// Application shell: title bar, the generator page, toasts, the settings dialog and the style preview dialog.
    /// </summary>
    public sealed partial class AppShell : UserControl
    {
        private MainWindow? _window;
        private bool _settingsAccepted;
        private ArtStyle? _previewStyle;
        private CancellationTokenSource? _previewCts;

        public AppShell()
        {
            InitializeComponent();
            // Accelerators live on the shell; without this every tooltip would append the shortcut.
            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
            AppLocalization.Instance.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateLocalizedChrome);
        }

        public AppLocalization Localization => AppLocalization.Instance;

        public UIElement TitleBarElement => AppTitleBar;

        public void Initialize(MainWindow window)
        {
            _window = window;
            SettingsDialogFrame.BorderBrush = WindowsAccent.Brush;
            PreviewDialogFrame.BorderBrush = WindowsAccent.Brush;
            SettingsPage.Initialize(this);
            SettingsPage.SetDialogMode();
            SettingsDialog.RegisterPropertyChangedCallback(DaisyModal.IsOpenProperty, (_, _) => OnSettingsDialogOpenChanged());
            PreviewDialog.RegisterPropertyChangedCallback(DaisyModal.IsOpenProperty, (_, _) =>
            {
                if (!PreviewDialog.IsOpen)
                    _previewCts?.Cancel();
            });
            RootGrid.SizeChanged += (_, _) => UpdateDialogSizes();
            Generator.Initialize(this);

            KeyboardAccelerators.Add(CreateAccelerator((VirtualKey)188, VirtualKeyModifiers.Control, (_, a) =>
            {
                OpenSettings();
                a.Handled = true;
            }));
            KeyboardAccelerators.Add(CreateAccelerator(VirtualKey.Enter, VirtualKeyModifiers.Control, (_, a) =>
            {
                Generator.StartRun();
                a.Handled = true;
            }));
            KeyboardAccelerators.Add(CreateAccelerator(VirtualKey.Escape, VirtualKeyModifiers.None, (_, a) =>
            {
                if (Generator.IsRunning && !SettingsDialog.IsOpen && !PreviewDialog.IsOpen)
                {
                    Generator.CancelRun();
                    a.Handled = true;
                }
            }));

            UpdateLocalizedChrome();
        }

        public void Shutdown() => Generator.Shutdown();

        /// <summary>Shows a spinner in the title bar while a run is active.</summary>
        public void SetRunning(bool running) =>
            RunIndicator.Visibility = running ? Visibility.Visible : Visibility.Collapsed;

        private static KeyboardAccelerator CreateAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += handler;
            return accelerator;
        }

        private void UpdateLocalizedChrome()
        {
            Tip(SettingsButton, $"{L("Settings_Title")} (Ctrl+,)");
            Tip(SettingsCloseButton, L("App_Close"));
            Tip(PreviewCloseButton, L("App_Close"));
            if (_previewStyle != null)
            {
                PreviewDescription.Text = L($"Style_{_previewStyle.Id}");
                PreviewCategory.Content = L($"Category_{_previewStyle.Category}");
            }
        }

        private void UpdateDialogSizes()
        {
            var f = SizeFactor;
            SettingsDialogContent.Width = Math.Max(320, Math.Min(820 * f, RootGrid.ActualWidth - 96));
            SettingsDialogContent.Height = Math.Max(320, Math.Min(780, RootGrid.ActualHeight - 120));
            var previewWidth = Math.Max(320, Math.Min(1100, RootGrid.ActualWidth - 120));
            var imageHeight = Math.Min(previewWidth * 10 / 16, RootGrid.ActualHeight - 300);
            PreviewDialogContent.Width = previewWidth;
            PreviewImageFrame.Height = Math.Max(160, imageHeight);
        }

        #region Settings dialog

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

        public void OpenSettings()
        {
            if (SettingsDialog.IsOpen)
                return;
            _settingsAccepted = false;
            SettingsPage.BeginEdit();
            UpdateDialogSizes();
            SettingsDialog.IsOpen = true;
        }

        private void SettingsOk_Click(object sender, RoutedEventArgs e)
        {
            _settingsAccepted = true;
            SettingsDialog.IsOpen = false;
        }

        private void SettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            _settingsAccepted = false;
            SettingsDialog.IsOpen = false;
        }

        /// <summary>
        /// OK keeps the previewed changes; Cancel, Esc, the close button or a click outside restore the previous state.
        /// </summary>
        private void OnSettingsDialogOpenChanged()
        {
            if (SettingsDialog.IsOpen)
                return;
            if (_settingsAccepted)
                SettingsPage.CommitEdit();
            else
                SettingsPage.CancelEdit();
            _settingsAccepted = false;
        }

        /// <summary>Called by the settings page when an option affecting the style previews changed.</summary>
        public void OnPreviewOptionChanged() => Generator.OnPreviewOptionChanged();

        #endregion

        #region Style preview dialog

        public void ShowStylePreview(ArtStyle style)
        {
            _previewStyle = style;
            PreviewTitle.Text = style.Name;
            UpdateLocalizedChrome();
            UpdateDialogSizes();
            PreviewDialog.IsOpen = true;
            _ = RenderPreviewAsync();
        }

        private void PreviewShuffle_Click(object sender, RoutedEventArgs e) => _ = RenderPreviewAsync();

        private void PreviewClose_Click(object sender, RoutedEventArgs e) => PreviewDialog.IsOpen = false;

        private async Task RenderPreviewAsync()
        {
            if (_previewStyle is not { } style)
                return;
            _previewCts?.Cancel();
            var cts = new CancellationTokenSource();
            _previewCts = cts;
            PreviewDialogLoading.Visibility = Visibility.Visible;
            PreviewDialogImage.Opacity = 0.4;

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var width = (int)(PreviewDialogContent.Width * scale);
            var height = (int)(PreviewImageFrame.Height * scale);
            var settings = AppSettings.Generator.Clone();
            var seed = Random.Shared.Next();
            try
            {
                var (bytes, info) = await Task.Run(() =>
                {
                    var rng = new Random(seed);
                    var families = settings.Palettes.Count > 0 ? settings.Palettes : [PaletteFamily.Curated];
                    var tones = settings.Tones.Count > 0 ? settings.Tones : [BackgroundTone.Dark];
                    var details = settings.Details.Count > 0 ? settings.Details : [DetailLevel.Medium];
                    var family = families[rng.Next(families.Count)];
                    var tone = tones[rng.Next(tones.Count)];
                    var detail = details[rng.Next(details.Count)];
                    var imageSeed = rng.Next();
                    var (image, palette) = ImageGenerator.Render(style, imageSeed, family, tone, detail, width, height, settings.Grain, settings.Vignette);
                    using (image)
                    {
                        var info = $"{palette.Name} · {L($"Palette_{family}")} · {L($"Tone_{tone}")} · {L($"Detail_{detail}")} · {AppLocalization.Format("Preview_Seed", (uint)imageSeed)}";
                        return (ImageEncoder.Encode(image, OutputFormat.Jpeg, 92, null), info);
                    }
                }, cts.Token);
                if (cts.IsCancellationRequested)
                    return;
                PreviewDialogImage.Source = await PreviewRenderer.ToBitmapAsync(bytes);
                PreviewDialogImage.Opacity = 1;
                PreviewInfo.Text = info;
                ToolTipService.SetToolTip(PreviewInfo, info);
            }
            catch (OperationCanceledException)
            {
                // Closed or replaced.
            }
            catch (Exception ex)
            {
                ShowToast(ex.Message, DaisyAlertVariant.Error);
            }
            finally
            {
                if (ReferenceEquals(_previewCts, cts))
                    PreviewDialogLoading.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Toasts

        public void ShowToast(string message, DaisyAlertVariant variant = DaisyAlertVariant.Info)
        {
            var alert = new DaisyAlert
            {
                Variant = variant,
                Message = message,
                MaxWidth = 560,
                Margin = new Thickness(0, 6, 0, 0)
            };
            AutomationProperties.SetLiveSetting(alert, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            Toasts.AddToast(alert);
            UiTimers.RunOnce(DispatcherQueue, TimeSpan.FromSeconds(variant == DaisyAlertVariant.Error ? 6 : 3.5), () => Toasts.RemoveToast(alert));
        }

        #endregion

        #region Pickers

        public async Task<string?> PickFolderAsync()
        {
            if (_window == null)
                return null;
            try
            {
                var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(_window.AppWindow.Id)
                {
                    SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                };
                var result = await picker.PickSingleFolderAsync();
                return result?.Path;
            }
            catch (Exception ex)
            {
                ShowToast(ex.Message, DaisyAlertVariant.Error);
                return null;
            }
        }

        #endregion
    }
}
