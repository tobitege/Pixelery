using System.Threading;
using System.Threading.Tasks;
using AbstractImg.Core.Generation;
using AbstractImg.Localization;
using AbstractImg.Services;
using Microsoft.UI.Dispatching;
using static AbstractImg.Views.UiHelpers;

namespace AbstractImg.Views
{
    /// <summary>Run panel: count, threads, seed, start/cancel and the live progress display.</summary>
    public sealed partial class GeneratorView
    {
        private CancellationTokenSource? _runCts;
        private GenerationRun? _run;
        private DispatcherQueueTimer? _progressTimer;
        private GenerationPreview? _shownPreview;
        private readonly Dictionary<string, TextBlock> _statValues = [];

        private void InitializeRunPanel()
        {
            CountInput.Minimum = 1;
            CountInput.Maximum = 1_000_000;
            CountInput.Increment = 10;
            CountInput.Value = Settings.Count;
            CountInput.RegisterPropertyChangedCallback(DaisyNumericUpDown.ValueProperty, (_, _) =>
            {
                Settings.Count = (int)Math.Clamp(CountInput.Value ?? 1, 1, 1_000_000);
                OnSettingsChanged();
            });
            foreach (var preset in new[] { 100, 1_000, 10_000 })
            {
                var value = preset;
                var button = new DaisyButton { Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small };
                Localize(() => button.Content = value.ToString("N0", AppLocalization.CurrentCulture));
                button.Click += (_, _) => CountInput.Value = value;
                CountPresets.Children.Add(button);
            }

            var cores = Environment.ProcessorCount;
            ThreadsInput.Minimum = 1;
            ThreadsInput.Maximum = Math.Max(64, cores * 2);
            ThreadsInput.Value = Math.Clamp(Settings.Threads, 1, cores * 2);
            ThreadsInput.RegisterPropertyChangedCallback(DaisyNumericUpDown.ValueProperty, (_, _) =>
            {
                Settings.Threads = (int)Math.Clamp(ThreadsInput.Value ?? 1, 1, 256);
                OnSettingsChanged();
            });
            Localize(() => ThreadsHint.Text = AppLocalization.Format("Run_ThreadsHint", cores));

            SeedInput.Text = Settings.Seed;
            SeedInput.TextChanged += (_, _) =>
            {
                Settings.Seed = SeedInput.Text?.Trim() ?? string.Empty;
                OnSettingsChanged();
                UpdateNamePreview();
            };
            LocalizeTip(SeedDiceButton, "Run_SeedDice");

            string[] stats = ["Rate", "Elapsed", "Remaining", "Written", "PerImage", "Failed"];
            for (var i = 0; i < stats.Length; i++)
            {
                var key = stats[i];
                if (i % 2 == 0)
                    StatsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cell = new StackPanel { Spacing = 1 };
                var caption = Responsive(new TextBlock { Style = (Style)Application.Current.Resources["AppCaptionTextStyle"] }, ResponsiveFontTier.Secondary);
                Localize(() => caption.Text = L($"Stat_{key}"));
                var value = Text("–", ResponsiveFontTier.Primary);
                value.FontWeight = FontWeights.SemiBold;
                cell.Children.Add(caption);
                cell.Children.Add(value);
                Grid.SetRow(cell, i / 2);
                Grid.SetColumn(cell, i % 2);
                StatsGrid.Children.Add(cell);
                _statValues[key] = value;
            }
        }

        private void SeedDiceButton_Click(object sender, RoutedEventArgs e) =>
            SeedInput.Text = Random.Shared.Next(1, int.MaxValue).ToString(CultureInfo.InvariantCulture);

        private void GenerateButton_Click(object sender, RoutedEventArgs e) => StartRun();

        private void CancelButton_Click(object sender, RoutedEventArgs e) => CancelRun();

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = _run?.OutputFolder ?? Settings.OutputFolder;
            if (System.IO.Directory.Exists(folder))
                _ = Windows.System.Launcher.LaunchFolderPathAsync(folder);
        }

        public void StartRun()
        {
            if (IsRunning || Settings.Validate().Count > 0)
                return;

            AppSettings.SaveGenerator();
            var cts = new CancellationTokenSource();
            Task completion;
            try
            {
                _run = ImageGenerator.Start(Settings, cts.Token, out completion);
            }
            catch (Exception ex)
            {
                _shell?.ShowToast(ex.Message, DaisyAlertVariant.Error);
                return;
            }
            _run.PreviewInterval = AppSettings.LivePreview ? TimeSpan.FromMilliseconds(AppSettings.PreviewIntervalMs) : TimeSpan.Zero;
            _runCts = cts;
            _shownPreview = null;

            SetLocked(true);
            GenerateButton.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;
            OpenFolderButton.Visibility = Visibility.Collapsed;
            ResultAlert.Visibility = Visibility.Collapsed;
            PreviewImage.Source = null;
            PreviewLoading.Visibility = AppSettings.LivePreview ? Visibility.Visible : Visibility.Collapsed;
            PreviewOffText.Visibility = AppSettings.LivePreview ? Visibility.Collapsed : Visibility.Visible;
            PreviewCaption.Text = string.Empty;

            _shell?.SetRunning(true);
            _progressTimer ??= CreateProgressTimer();
            _progressTimer.Start();
            UpdateProgressTexts();
            _ = AwaitRunAsync(completion);
        }

        public void CancelRun()
        {
            if (_runCts == null)
                return;
            _runCts.Cancel();
            CancelButton.IsEnabled = false;
        }

        /// <summary>Panels have no IsEnabled in WinUI; block input and dim the configuration while a run is active.</summary>
        private void SetLocked(bool locked)
        {
            Sections.IsHitTestVisible = !locked;
            Sections.Opacity = locked ? 0.55 : 1;
            ConfigScroller.IsTabStop = !locked;
            // The run setup makes room for the progress display, which would otherwise start below the fold.
            SetupPanel.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
        }

        private DispatcherQueueTimer CreateProgressTimer()
        {
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(120);
            timer.IsRepeating = true;
            timer.Tick += (_, _) =>
            {
                UpdateProgressTexts();
                _ = UpdatePreviewAsync();
            };
            return timer;
        }

        private async Task AwaitRunAsync(Task completion)
        {
            try
            {
                await completion;
            }
            catch (Exception ex)
            {
                _shell?.ShowToast(ex.Message, DaisyAlertVariant.Error);
            }

            _progressTimer?.Stop();
            _shell?.SetRunning(false);
            _runCts?.Dispose();
            _runCts = null;
            UpdateProgressTexts();
            await UpdatePreviewAsync();

            SetLocked(false);
            GenerateButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Collapsed;
            OpenFolderButton.Visibility = Visibility.Visible;
            PreviewLoading.Visibility = Visibility.Collapsed;
            ShowResult();
            UpdateSummary();
            ProgressPanel.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0 });

            if (AppSettings.OpenFolderWhenDone && _run is { Completed: > 0 })
                OpenFolderButton_Click(this, new RoutedEventArgs());
        }

        private void ShowResult()
        {
            if (_run == null)
                return;
            var written = _run.Completed - _run.Failed;
            string message;
            DaisyAlertVariant variant;
            if (_run.WasCancelled)
            {
                variant = DaisyAlertVariant.Warning;
                message = AppLocalization.Format("Run_ResultCancelled", written, _run.Total);
            }
            else if (_run.Failed > 0)
            {
                variant = DaisyAlertVariant.Error;
                message = AppLocalization.Format("Run_ResultFailed", written, _run.Failed, _run.FirstError ?? string.Empty);
            }
            else
            {
                variant = DaisyAlertVariant.Success;
                message = AppLocalization.Format("Run_ResultDone", written, FormatDuration(_run.Watch.Elapsed));
            }
            ResultAlert.Variant = variant;
            ResultAlert.Message = message;
            ResultAlert.Visibility = Visibility.Visible;
            _shell?.ShowToast(message, variant);
        }

        private void UpdateProgressTexts()
        {
            var run = _run;
            if (run == null)
                return;
            var culture = AppLocalization.CurrentCulture;
            var done = run.Completed;
            var elapsed = run.Watch.Elapsed;
            var rate = elapsed.TotalSeconds > 0.2 ? done / elapsed.TotalSeconds : 0;
            ProgressCount.Text = string.Format(culture, "{0:N0} / {1:N0}", done, run.Total);
            var percent = run.Total == 0 ? 0 : done * 100.0 / run.Total;
            ProgressPercent.Text = string.Format(culture, "{0:0.0} %", percent);
            ProgressBar.Value = percent;

            _statValues["Rate"].Text = AppLocalization.Format("Stat_RateValue", rate);
            _statValues["Elapsed"].Text = FormatDuration(elapsed);
            _statValues["Remaining"].Text = rate > 0 && done < run.Total
                ? FormatDuration(TimeSpan.FromSeconds((run.Total - done) / rate))
                : "–";
            _statValues["Written"].Text = FormatBytes(run.BytesWritten);
            // Average wall time each worker spends per image.
            _statValues["PerImage"].Text = done > 0
                ? string.Format(culture, "{0:N0} ms", elapsed.TotalMilliseconds * Math.Min(Settings.Threads, Math.Max(1, run.Total)) / done)
                : "–";
            _statValues["Failed"].Text = run.Failed.ToString("N0", culture);
            _statValues["Failed"].Foreground = run.Failed > 0
                ? DaisyResourceLookup.GetBrush("DaisyErrorBrush")
                : DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        }

        private async Task UpdatePreviewAsync()
        {
            var preview = _run?.Preview;
            if (preview == null || ReferenceEquals(preview, _shownPreview))
                return;
            _shownPreview = preview;
            try
            {
                var bitmap = await PreviewRenderer.ToBitmapAsync(preview.Jpeg);
                if (!ReferenceEquals(preview, _shownPreview))
                    return;
                PreviewImage.Source = bitmap;
                PreviewLoading.Visibility = Visibility.Collapsed;
                PreviewCaption.Text = $"{System.IO.Path.GetFileName(preview.FilePath)} · {preview.Width}×{preview.Height} · {preview.Style}";
                ToolTipService.SetToolTip(PreviewCaption, preview.FilePath);
            }
            catch
            {
                // A broken preview must not stop the run display.
            }
        }

        private static string FormatDuration(TimeSpan time) =>
            time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
