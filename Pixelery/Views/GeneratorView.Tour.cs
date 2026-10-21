namespace Pixelery.Views
{
    /// <summary>Targets of the guided tour on the main page; the shell owns the tour itself.</summary>
    public sealed partial class GeneratorView
    {
        /// <summary>The section registered under its title key, or null while it is not built yet.</summary>
        public FrameworkElement? TourSection(string key) => _sectionTargets.GetValueOrDefault(key);

        public FrameworkElement TourRunPanel => RunPanel;

        public FrameworkElement TourSideTabs => SideTabs;

        public int SideTabIndex
        {
            get => SideTabs.SelectedIndex;
            set => SideTabs.SelectedIndex = value;
        }

        /// <summary>The header button of the slideshow tab; DaisyTabs builds its headers as plain buttons tagged with their index.</summary>
        public FrameworkElement? TourSlideshowTabHeader() => FindTabHeader(SideTabs, 1);

        private static FrameworkElement? FindTabHeader(DependencyObject parent, int index)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button { Tag: int tag } button && tag == index)
                    return button;
                // Skip the tab pages: only the header strip holds the tab buttons.
                if (child is ScrollViewer)
                    continue;
                if (FindTabHeader(child, index) is { } found)
                    return found;
            }
            return null;
        }
    }
}
