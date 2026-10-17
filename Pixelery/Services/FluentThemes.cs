using Windows.UI.ViewManagement;

namespace Pixelery.Services
{
    internal static class FluentThemes
    {
        private const string Dark = "Fluent Dark";
        private const string Light = "Fluent Light";
        private const string DarkWindows = "Fluent Dark (Windows accent)";
        private const string LightWindows = "Fluent Light (Windows accent)";

        public static void Register()
        {
            // Separate palette identities let Flowery perform its normal full theme refresh
            // when the accent option changes. Only the active pair appears in the dropdown.
            foreach (var (name, display, dark, windows) in new[]
            {
                (Dark, Dark, true, false), (Light, Light, false, false),
                (DarkWindows, Dark, true, true), (LightWindows, Light, false, true)
            })
            {
                DaisyThemeManager.RegisterTheme(new DaisyThemeInfo(name, dark), () => CreatePalette(dark, windows));
                DaisyThemeManager.AddTheme(name, display, preferred: true);
            }
            UpdateThemeList();
        }

        public static bool IsFluent(string? name) => name is Dark or Light or DarkWindows or LightWindows;

        public static string ResolveName(string name) => name switch
        {
            Dark or DarkWindows => AppSettings.UseWindowsAccent ? DarkWindows : Dark,
            Light or LightWindows => AppSettings.UseWindowsAccent ? LightWindows : Light,
            _ => name
        };

        public static void ApplyAccentPreference(string? themeName)
        {
            if (themeName != null)
                DaisyThemeManager.ApplyTheme(ResolveName(themeName));
            UpdateThemeList();
        }

        private static void UpdateThemeList()
        {
            string[] hidden = AppSettings.UseWindowsAccent ? [Dark, Light] : [DarkWindows, LightWindows];
            DaisyThemeManager.ExcludedThemes = DaisyThemeManager.ExcludedThemes
                .Where(name => !IsFluent(name)).Concat(hidden).ToArray();
        }

        private static ResourceDictionary CreatePalette(bool isDark, bool useWindowsAccent)
        {
            var resources = new XamlControlsResources();
            // WinUI calls its dark resource dictionary "Default". Read colors directly:
            // brushes can resolve their ThemeResource references against the current theme.
            var native = (ResourceDictionary)resources.ThemeDictionaries[isDark ? "Default" : "Light"];
            var palette = new ResourceDictionary();
            // The Windows 11 default blue ramp, independent of personalisation settings.
            var accent = isDark ? Color.FromArgb(255, 76, 194, 255) : Color.FromArgb(255, 0, 103, 192);
            var accentText = isDark ? Color.FromArgb(255, 153, 235, 255) : Color.FromArgb(255, 0, 62, 146);
            if (useWindowsAccent)
            {
                var settings = new UISettings();
                accent = settings.GetColorValue(isDark ? UIColorType.AccentLight2 : UIColorType.AccentDark1);
                accentText = settings.GetColorValue(isDark ? UIColorType.AccentLight3 : UIColorType.AccentDark2);
            }
            var onAccent = (Color)native["TextOnAccentFillColorPrimary"];
            var accentHover = Color.FromArgb(230, accent.R, accent.G, accent.B);

            // WinUI uses the same system accent for these Daisy semantic variants.
            foreach (var variant in new[] { "Primary", "Secondary", "Accent" })
            {
                Add("Daisy" + variant, accent);
                Add("Daisy" + variant + "Focus", accentHover);
                Add("Daisy" + variant + "Content", onAccent);
                Add("Daisy" + variant + "Foreground", accentText);
            }

            Add("DaisyBase100", (Color)native["SolidBackgroundFillColorTertiary"]);
            Add("DaisyBase200", (Color)native["SolidBackgroundFillColorBase"]);
            Add("DaisyBase300", (Color)native["ControlStrokeColorDefault"]);
            Add("DaisyBaseContent", (Color)native["TextFillColorPrimary"]);
            Add("DaisyNeutral", (Color)native["ControlFillColorDefault"]);
            Add("DaisyNeutralFocus", (Color)native["ControlFillColorSecondary"]);
            Add("DaisyNeutralContent", (Color)native["TextFillColorPrimary"]);
            Add("DaisyInfo", accent);
            Add("DaisyInfoContent", onAccent);
            Add("DaisySuccess", (Color)native["SystemFillColorSuccess"]);
            Add("DaisySuccessContent", onAccent);
            Add("DaisyWarning", (Color)native["SystemFillColorCaution"]);
            Add("DaisyWarningContent", onAccent);
            Add("DaisyError", (Color)native["SystemFillColorCritical"]);
            Add("DaisyErrorContent", onAccent);
            Add("DaisyButtonPrimaryForeground", accentText);
            Add("DaisyButtonPrimaryDisabled", (Color)native["AccentFillColorDisabled"]);
            Add("DaisyDisabledForeground", (Color)native["TextFillColorDisabled"]);
            return palette;

            void Add(string key, Color color)
            {
                palette[key + "Color"] = color;
                palette[key + "Brush"] = new SolidColorBrush(color);
            }
        }
    }
}
