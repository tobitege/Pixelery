namespace Pixelery.Services
{
    /// <summary>
    /// Captures everything the settings dialog can change so "Cancel" can restore it.
    /// Changes apply live as a preview while the dialog is open.
    /// </summary>
    public sealed record SettingsSnapshot(
        string? Theme,
        DaisySize Size,
        string Culture,
        bool UseWindowsAccent,
        bool LivePreview,
        int PreviewIntervalMs,
        bool PreviewsFollowColors,
        bool OpenFolderWhenDone)
    {
        public static SettingsSnapshot Capture() => new(
            DaisyThemeManager.CurrentThemeName,
            FlowerySizeManager.CurrentSize,
            FloweryLocalization.CurrentCultureName,
            AppSettings.UseWindowsAccent,
            AppSettings.LivePreview,
            AppSettings.PreviewIntervalMs,
            AppSettings.PreviewsFollowColors,
            AppSettings.OpenFolderWhenDone);

        public void Restore()
        {
            AppSettings.UseWindowsAccent = UseWindowsAccent;
            FluentThemes.ApplyAccentPreference(Theme);

            if (Size != FlowerySizeManager.CurrentSize)
                FlowerySizeManager.ApplySize(Size);

            if (!string.Equals(Culture, FloweryLocalization.CurrentCultureName, StringComparison.OrdinalIgnoreCase))
                FloweryLocalization.SetCulture(Culture);

            AppSettings.LivePreview = LivePreview;
            AppSettings.PreviewIntervalMs = PreviewIntervalMs;
            AppSettings.PreviewsFollowColors = PreviewsFollowColors;
            AppSettings.OpenFolderWhenDone = OpenFolderWhenDone;
            AppSettings.SaveOptions();
        }
    }
}
