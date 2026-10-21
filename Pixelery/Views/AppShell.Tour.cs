using System.Threading.Tasks;
using Pixelery.Services;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views
{
    /// <summary>
    /// Guided tour through the main areas: shown once after the first start and again from the settings.
    /// Completing or skipping it counts as seen; a cancelled tour comes back on the next start.
    /// </summary>
    public sealed partial class AppShell
    {
        private bool _tourChecked;
        private int _tourTabIndex;

        private void InitializeTour()
        {
            AddTourStep("Styles", () => Generator.TourSection("Section_Styles"));
            AddTourStep("Colors", () => Generator.TourSection("Section_Colors"));
            AddTourStep("Dimensions", () => Generator.TourSection("Section_Dimensions"));
            AddTourStep("Watermark", () => Generator.TourSection("Section_Watermark"));
            AddTourStep("Output", () => Generator.TourSection("Section_Output"));
            AddTourStep("Run", () =>
            {
                Generator.SideTabIndex = 0;
                return Generator.TourRunPanel;
            });
            AddTourStep("Slideshow", () =>
            {
                Generator.SideTabIndex = 1;
                return Generator.TourSideTabs;
            }, Generator.TourSlideshowTabHeader);
            AddTourStep("Settings", () => SettingsButton);
            Tour.Closed += Tour_Closed;
        }

        /// <summary>
        /// Adds a step whose target is resolved right before it is shown, since sections are rebuilt after an import.
        /// A large arrow points from the card at the target, or at <paramref name="arrowTarget"/> inside it.
        /// </summary>
        private void AddTourStep(string id, Func<FrameworkElement?> target, Func<FrameworkElement?>? arrowTarget = null)
        {
            var arrow = new DaisyTourAnnotation { Id = id + "Arrow", Preset = DaisyTourAnnotationPreset.BlockArrow };
            var step = new DaisyTourStep { Id = id };
            step.Annotations.Add(arrow);
            step.PrepareAsync = (s, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                s.Target = target();
                arrow.Target = arrowTarget?.Invoke();
                return Task.CompletedTask;
            };
            Tour.Steps.Add(step);
        }

        private void UpdateTourTexts()
        {
            foreach (var step in Tour.Steps)
            {
                step.Title = L($"Tour_{step.Id}Title");
                step.Description = L($"Tour_{step.Id}Text");
            }
        }

        /// <summary>Called by the page whenever its sections are built; starts the tour once if it was never seen.</summary>
        public void OnSectionsBuilt()
        {
            if (_tourChecked)
                return;
            _tourChecked = true;
            if (!AppSettings.TourSeen)
                StartTour();
        }

        /// <summary>"Show tour again" in the settings: keeps the previewed settings like OK, then starts the tour.</summary>
        public void ReplayTour()
        {
            _settingsAccepted = true;
            SettingsDialog.IsOpen = false;
            DispatcherQueue.TryEnqueue(StartTour);
        }

        private async void StartTour()
        {
            if (HasOpenDialog)
                return;
            _tourTabIndex = Generator.SideTabIndex;
            try
            {
                await Tour.StartAsync();
            }
            catch (Exception ex)
            {
                ShowToast(ex.Message, DaisyAlertVariant.Error);
            }
        }

        private void Tour_Closed(object? sender, DaisyTourClosedEventArgs e)
        {
            Generator.SideTabIndex = _tourTabIndex;
            if (e.Reason is DaisyTourCloseReason.Completed or DaisyTourCloseReason.Skipped && !AppSettings.TourSeen)
            {
                AppSettings.TourSeen = true;
                AppSettings.SaveOptions();
            }
        }
    }
}
