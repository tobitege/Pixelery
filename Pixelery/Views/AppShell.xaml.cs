using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Core.Watermark;
using Pixelery.Localization;
using Pixelery.Services;
using Microsoft.UI.Xaml.Automation;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Windows.System;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>
    /// Application shell: title bar, the generator page, toasts, the settings dialog, the style preview dialog and the guided tour.
    /// </summary>
    public sealed partial class AppShell : UserControl
    {
        private MainWindow? _window;
        private bool _settingsAccepted;
        private ArtStyle? _previewStyle;
        private SlideOverride? _previewSlide;
        private Action<WatermarkSettings>? _watermarkCommit;
        private CancellationTokenSource? _previewCts;
        private DispatcherQueueTimer? _previewSlideshowTimer;
        private SlideshowWindow? _previewFullScreen;
        private bool _isPreviewRendering;
        private bool _previewSlideshowRunning;

        private bool HasOpenDialog => SettingsDialog?.IsOpen == true || PreviewDialog?.IsOpen == true || WatermarkDialog?.IsOpen == true || Tour.IsOpen;

        private void UpdateDialogInteractivity()
        {
            var enabled = !HasOpenDialog;
            Generator.IsEnabled = enabled;
            SettingsButton.IsEnabled = enabled;
        }

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
            // Version and copyright next to the app name, where everyone sees them.
            AppTitleBar.Subtitle = $"· {Core.AppInfo.Version(typeof(AppShell).Assembly)} · {Core.AppInfo.Copyright}";
            RootGrid.SizeChanged += (_, _) => UpdateDialogSizes();
            Generator.Initialize(this);

            KeyboardAccelerators.Add(CreateAccelerator((VirtualKey)188, VirtualKeyModifiers.Control, (_, a) =>
            {
                OpenSettings();
                a.Handled = true;
            }));
            KeyboardAccelerators.Add(CreateAccelerator(VirtualKey.Enter, VirtualKeyModifiers.Control, (_, a) =>
            {
                if (!HasOpenDialog)
                    Generator.StartFromButton();
                a.Handled = true;
            }));
            KeyboardAccelerators.Add(CreateAccelerator(VirtualKey.Escape, VirtualKeyModifiers.None, (_, a) =>
            {
                if (Generator.IsRunning && !HasOpenDialog)
                {
                    Generator.CancelRun();
                    a.Handled = true;
                }
            }));

            InitializeTour();
            UpdateLocalizedChrome();
        }

        public void Shutdown()
        {
            SettingsPage?.CancelUpdateOperation();
            if (PreviewSlideshowCheck != null)
                PreviewSlideshowCheck.IsChecked = false;
            UiTimers.Cancel(_previewSlideshowTimer);
            _previewCts?.Cancel();
            _previewFullScreen?.Close();
            Generator.Shutdown();
        }

        /// <summary>Shows a spinner in the title bar while a run is active.</summary>
        public void SetRunning(bool running)
        {
            RunIndicator.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage?.RefreshUpdateControls();
        }

        public bool CanInstallUpdate => !Generator.IsRunning;

        public void CloseForUpdate()
        {
            _settingsAccepted = true;
            SettingsDialog.IsOpen = false;
            _window?.Close();
        }

        private static KeyboardAccelerator CreateAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += handler;
            return accelerator;
        }

        private void UpdateLocalizedChrome()
        {
            Tip(SettingsButton, $"{L("Settings_Title")} (Ctrl+,)");
            Tip(GitHubButton, $"GitHub · {Core.AppInfo.RepositoryUrl}");
            // The dialogs exist only after their first use (x:Load).
            if (SettingsCloseButton != null)
                Tip(SettingsCloseButton, L("App_Close"));
            if (PreviewCloseButton != null)
                Tip(PreviewCloseButton, L("App_Close"));
            if (WatermarkCloseButton != null)
                Tip(WatermarkCloseButton, L("App_Close"));
            if (PreviewFullScreenButton != null)
                Tip(PreviewFullScreenButton, L("Preview_FullScreen"));
            UpdatePreviewActionText();
            UpdateTourTexts();
            if (_previewStyle != null)
            {
                PreviewDescription.Text = L($"Style_{_previewStyle.Id}");
                PreviewCategory.BadgeContent = L($"Category_{_previewStyle.Category}");
            }
        }

        private void UpdateDialogSizes()
        {
            var f = SizeFactor;
            if (SettingsDialogContent != null)
            {
                SettingsDialogContent.Width = Math.Max(320, Math.Min(820 * f, RootGrid.ActualWidth - 96));
                SettingsDialogContent.Height = Math.Max(320, Math.Min(780, RootGrid.ActualHeight - 120));
            }
            if (WatermarkDialogContent != null)
            {
                WatermarkDialogContent.Width = Math.Max(720, Math.Min(1440, RootGrid.ActualWidth - 96));
                WatermarkDialogContent.Height = Math.Max(480, Math.Min(940, RootGrid.ActualHeight - 100));
            }
            if (PreviewDialogContent == null)
                return;
            var previewWidth = Math.Max(320, Math.Min(1100, RootGrid.ActualWidth - 120));
            var imageHeight = Math.Min(previewWidth * 10 / 16, RootGrid.ActualHeight - 300);
            PreviewDialogContent.Width = previewWidth;
            PreviewImageFrame.Height = Math.Max(160, imageHeight);
        }

        #region Settings dialog

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void GitHubButton_Click(object sender, RoutedEventArgs e) =>
            _ = Launcher.LaunchUriAsync(new Uri(Core.AppInfo.RepositoryUrl));

        /// <summary>Creates the settings dialog on first use.</summary>
        private void EnsureSettingsDialog()
        {
            if (SettingsDialog != null)
                return;
            FindName(nameof(SettingsDialog));
            SettingsDialogFrame.BorderBrush = WindowsAccent.Brush;
            SettingsPage.Initialize(this);
            SettingsPage.SetDialogMode();
            SettingsDialog!.RegisterPropertyChangedCallback(DaisyModal.IsOpenProperty, (_, _) => OnSettingsDialogOpenChanged());
            UpdateLocalizedChrome();
        }

        /// <summary>Creates the style preview dialog on first use.</summary>
        private void EnsurePreviewDialog()
        {
            if (PreviewDialog != null)
                return;
            FindName(nameof(PreviewDialog));
            PreviewDialogFrame.BorderBrush = WindowsAccent.Brush;
            PreviewSlideshowSeconds.Minimum = 1;
            PreviewSlideshowSeconds.Maximum = 999;
            PreviewSlideshowSeconds.Value = 3;
            PreviewSlideshowSeconds.Increment = 1;
            PreviewDialog!.RegisterPropertyChangedCallback(DaisyModal.IsOpenProperty, (_, _) =>
            {
                UpdateDialogInteractivity();
                if (!PreviewDialog.IsOpen)
                {
                    UiTimers.Cancel(_previewSlideshowTimer);
                    _previewSlideshowTimer = null;
                    _previewSlideshowRunning = false;
                    PreviewSlideshowCheck.IsChecked = false;
                    _previewCts?.Cancel();
                }
            });
            PreviewSlideshowSeconds.RegisterPropertyChangedCallback(DaisyNumericUpDown.ValueProperty,
                (_, _) => SchedulePreviewSlideshow());
            PreviewSlideshowCheck.RegisterPropertyChangedCallback(DaisyCheckBox.IsCheckedProperty,
                (_, _) =>
                {
                    if (PreviewSlideshowCheck.IsChecked != true)
                        _previewSlideshowRunning = false;
                    UpdatePreviewActionText();
                    SchedulePreviewSlideshow();
                });
            UpdateLocalizedChrome();
        }

        public void OpenSettings()
        {
            if (HasOpenDialog)
                return;
            EnsureSettingsDialog();
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
            UpdateDialogInteractivity();
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

        #region Watermark editor

        /// <summary>Opens the watermark editor on a copy; <paramref name="commit"/> receives the result on OK.</summary>
        public void OpenWatermarkEditor(WatermarkSettings current, GeneratorSettings generator, Action<WatermarkSettings> commit)
        {
            if (HasOpenDialog)
                return;
            if (WatermarkDialog == null)
            {
                FindName(nameof(WatermarkDialog));
                WatermarkDialogFrame.BorderBrush = WindowsAccent.Brush;
                WatermarkEditorView.Shell = this;
                WatermarkDialog!.RegisterPropertyChangedCallback(DaisyModal.IsOpenProperty, (_, _) => UpdateDialogInteractivity());
                UpdateLocalizedChrome();
            }
            _watermarkCommit = commit;
            UpdateDialogSizes();
            WatermarkEditorView.Begin(current, generator);
            WatermarkDialog!.IsOpen = true;
        }

        private void WatermarkOk_Click(object sender, RoutedEventArgs e)
        {
            _watermarkCommit?.Invoke(WatermarkEditorView.Result);
            _watermarkCommit = null;
            WatermarkDialog.IsOpen = false;
        }

        private void WatermarkCancel_Click(object sender, RoutedEventArgs e)
        {
            _watermarkCommit = null;
            WatermarkDialog.IsOpen = false;
        }

        #endregion

        #region Style preview dialog

        public void ShowStylePreview(ArtStyle style)
        {
            if (HasOpenDialog)
                return;
            EnsurePreviewDialog();
            _previewStyle = style;
            PreviewTitle.Text = style.Name;
            UpdateLocalizedChrome();
            UpdateDialogSizes();
            PreviewDialog.IsOpen = true;
            _ = RenderPreviewAsync();
        }

        private void PreviewShuffle_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewSlideshowCheck.IsChecked == true)
            {
                _previewSlideshowRunning = !_previewSlideshowRunning;
                UpdatePreviewActionText();
                if (!_previewSlideshowRunning)
                {
                    UiTimers.Cancel(_previewSlideshowTimer);
                    _previewSlideshowTimer = null;
                    _previewCts?.Cancel();
                    return;
                }
            }
            _ = RenderPreviewAsync();
        }

        private void UpdatePreviewActionText()
        {
            if (PreviewShuffleButton == null)
                return;
            var slideshow = PreviewSlideshowCheck.IsChecked == true;
            PreviewShuffleButton.Content = L(slideshow ? (_previewSlideshowRunning ? "Preview_Stop" : "Side_Start") : "Preview_Another");
            PreviewShuffleButton.IconData = Icon(slideshow ? (_previewSlideshowRunning ? "AppIconStop" : "AppIconPlay") : "AppIconShuffle");
        }

        private void SchedulePreviewSlideshow()
        {
            UiTimers.Cancel(_previewSlideshowTimer);
            _previewSlideshowTimer = null;
            if (PreviewDialog?.IsOpen != true || !_previewSlideshowRunning || _isPreviewRendering || _previewFullScreen != null)
                return;

            var seconds = Math.Clamp(decimal.Truncate(PreviewSlideshowSeconds.Value ?? 3), 1, 999);
            _previewSlideshowTimer = UiTimers.RunOnce(DispatcherQueue, TimeSpan.FromSeconds((double)seconds), () =>
            {
                _previewSlideshowTimer = null;
                _ = RenderPreviewAsync();
            });
        }

        private void PreviewClose_Click(object sender, RoutedEventArgs e) => PreviewDialog.IsOpen = false;

        /// <summary>
        /// Full screen view of the previewed style, using the dialog's slideshow state and interval.
        /// The dialog's own timer resumes when the full screen window closes.
        /// </summary>
        private void PreviewFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (_previewStyle is not { } style)
                return;
            if (_previewFullScreen is { } existing)
            {
                existing.Activate();
                return;
            }
            var settings = AppSettings.Generator.Clone();
            settings.Styles = [style.Id];
            var seconds = Math.Clamp(decimal.Truncate(PreviewSlideshowSeconds.Value ?? 3), 1, 999);
            var window = new SlideshowWindow(XamlRoot.ContentIslandEnvironment.AppWindowId,
                settings, Random.Shared.Next().ToString(CultureInfo.InvariantCulture),
                TimeSpan.FromSeconds((double)seconds), AppSettings.SlideshowCrossfade,
                AppSettings.SlideshowShowInfo, startPaused: !_previewSlideshowRunning,
                first: _previewSlide is { } slide && slide.Style == style ? slide : null);
            _previewFullScreen = window;
            UiTimers.Cancel(_previewSlideshowTimer);
            _previewSlideshowTimer = null;
            _previewCts?.Cancel();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_previewFullScreen, window))
                {
                    _previewFullScreen = null;
                    _previewSlideshowRunning = PreviewSlideshowCheck.IsChecked == true && !window.IsPaused;
                    UpdatePreviewActionText();
                    SchedulePreviewSlideshow();
                }
            };
            window.Show(fullScreen: true);
        }

        private async Task RenderPreviewAsync()
        {
            if (_previewStyle is not { } style)
                return;
            UiTimers.Cancel(_previewSlideshowTimer);
            _previewSlideshowTimer = null;
            _isPreviewRendering = true;
            _previewCts?.Cancel();
            var cts = new CancellationTokenSource();
            _previewCts = cts;
            PreviewDialogLoading.Visibility = Visibility.Visible;

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var width = (int)(PreviewDialogContent.Width * scale);
            var height = (int)(PreviewImageFrame.Height * scale);
            var settings = AppSettings.Generator.Clone();
            var seed = Random.Shared.Next();
            try
            {
                var (bytes, info, slide) = await Task.Run(() =>
                {
                    var rng = new Random(seed);
                    var families = settings.Palettes.Count > 0 ? settings.Palettes : [PaletteFamily.Curated];
                    var tones = settings.Tones.Count > 0 ? settings.Tones : [BackgroundTone.Dark];
                    var details = settings.Details.Count > 0 ? settings.Details : [DetailLevel.Medium];
                    var family = families[rng.Next(families.Count)];
                    var tone = tones[rng.Next(tones.Count)];
                    var detail = details[rng.Next(details.Count)];
                    var imageSeed = rng.Next();
                    var sample = settings.UsePaletteSet ? settings.PaletteSet.Entries[rng.Next(settings.PaletteSet.Entries.Count)] : null;
                    family = sample?.Family ?? family;
                    tone = sample?.Tone ?? tone;
                    var (image, palette) = ImageGenerator.Render(style, imageSeed, family, tone, detail, width, height, settings.Grain, settings.Vignette, sample);
                    using (image)
                    {
                        var info = $"{palette.Name} · {L($"Palette_{family}")} · {L($"Tone_{tone}")} · {L($"Detail_{detail}")} · {AppLocalization.Format("Preview_Seed", (uint)imageSeed)}";
                        return (ImageEncoder.Encode(image, OutputFormat.Jpeg, 92, null), info, new SlideOverride(style, imageSeed, family, tone, detail, sample));
                    }
                }, cts.Token);
                if (cts.IsCancellationRequested)
                    return;
                var bitmap = await PreviewRenderer.ToBitmapAsync(bytes);
                if (cts.IsCancellationRequested)
                    return;
                PreviewDialogImage.Source = bitmap;
                PreviewInfo.Text = info;
                ToolTipService.SetToolTip(PreviewInfo, info);
                _previewSlide = slide;
            }
            catch (OperationCanceledException)
            {
                // Closed or replaced.
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_previewCts, cts))
                {
                    PreviewSlideshowCheck.IsChecked = false;
                    ShowToast(ex.Message, DaisyAlertVariant.Error);
                }
            }
            finally
            {
                if (ReferenceEquals(_previewCts, cts))
                {
                    _isPreviewRendering = false;
                    PreviewDialogLoading.Visibility = Visibility.Collapsed;
                    SchedulePreviewSlideshow();
                }
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
            UiTimers.RunOnce(DispatcherQueue, TimeSpan.FromSeconds(variant is DaisyAlertVariant.Error or DaisyAlertVariant.Warning ? 6 : 3.5), () => Toasts.RemoveToast(alert));
        }

        #endregion

        #region Pickers

        public Task<string?> PickPaletteSetFileAsync(bool save) =>
            PickJsonFileAsync(save, "palette-set", AppSettings.PaletteDirectory, "Pixelery.PaletteSets", "Colors_SetFileType");

        /// <summary>Settings files start in Documents; the picker remembers the last folder.</summary>
        public Task<string?> PickSettingsFileAsync(bool save) =>
            PickJsonFileAsync(save, "pixelery-settings", null, "Pixelery.SettingsFiles", "Run_SettingsFileType");

        private async Task<string?> PickJsonFileAsync(bool save, string suggestedName, string? folder, string settingsIdentifier, string fileTypeKey)
        {
            if (_window == null)
                return null;
            if (save)
            {
                var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(_window.AppWindow.Id)
                {
                    SuggestedFileName = suggestedName,
                    DefaultFileExtension = ".json",
                    SettingsIdentifier = settingsIdentifier
                };
                if (folder != null)
                    picker.SuggestedFolder = folder;
                else
                    picker.SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(L(fileTypeKey), [".json"]);
                return (await picker.PickSaveFileAsync())?.Path;
            }
            else
            {
                var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(_window.AppWindow.Id)
                {
                    SettingsIdentifier = settingsIdentifier
                };
                if (folder != null)
                    picker.SuggestedFolder = folder;
                else
                    picker.SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".json");
                return (await picker.PickSingleFileAsync())?.Path;
            }
        }

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

        public async Task<string?> PickImageFileAsync()
        {
            if (_window == null)
                return null;
            try
            {
                var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(_window.AppWindow.Id)
                {
                    SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.PicturesLibrary,
                    ViewMode = Microsoft.Windows.Storage.Pickers.PickerViewMode.Thumbnail
                };
                foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" })
                    picker.FileTypeFilter.Add(extension);
                var result = await picker.PickSingleFileAsync();
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
