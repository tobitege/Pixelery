using System.IO;
using System.Text.Json;
using Pixelery.Core.Generation;
using Pixelery.Core.Watermark;
using Pixelery.Localization;
using Pixelery.Services;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views;

/// <summary>
/// Settings file: export the configuration for another PC and import it again. The same file must give the same images,
/// so export fixes the seed and the reference date when they are empty.
/// </summary>
public sealed partial class GeneratorView
{
    private const string ReferenceDateFormat = "yyyy-MM-dd";

    /// <summary>Localizers before this index belong to the run panel and the side tabs; the sections add theirs after it.</summary>
    private int _sectionLocalizerStart;

    private DaisyInput? _referenceDateInput;

    private static string AppVersion => Core.AppInfo.Version(typeof(GeneratorView).Assembly);

    private async void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shell == null || !_initialized || IsRunning)
            return;
        try
        {
            var path = await _shell.PickSettingsFileAsync(save: true);
            if (path == null)
                return;
            // TextChanged arrives asynchronously, so the settings are set here and the inputs only show them.
            if (string.IsNullOrWhiteSpace(Settings.Seed))
            {
                Settings.Seed = Random.Shared.Next(1, int.MaxValue).ToString(CultureInfo.InvariantCulture);
                SeedInput.Text = Settings.Seed;
            }
            var date = (Settings.ReferenceDate ??= DateOnly.FromDateTime(DateTime.Now)).ToString(ReferenceDateFormat, CultureInfo.InvariantCulture);
            if (_referenceDateInput != null)
                _referenceDateInput.Text = date;
            await File.WriteAllTextAsync(path, SettingsJson.Export(Settings, AppVersion));
            _shell.ShowToast(AppLocalization.Format("Run_SettingsExported", Settings.Seed, date), DaisyAlertVariant.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _shell.ShowToast(L("Run_SettingsExportError"), DaisyAlertVariant.Error);
        }
    }

    private async void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shell == null || !_initialized || IsRunning)
            return;
        GeneratorSettings imported;
        try
        {
            var path = await _shell.PickSettingsFileAsync(save: false);
            if (path == null)
                return;
            imported = SettingsJson.Import(await File.ReadAllTextAsync(path), Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _shell.ShowToast(L("Run_SettingsImportError"), DaisyAlertVariant.Error);
            return;
        }
        var notes = ImportNotes(imported);
        imported.AppVersion = null;
        ApplySettings(imported);
        _shell.ShowToast(notes.Count == 0 ? L("Run_SettingsImported") : string.Join(Environment.NewLine, notes),
            notes.Count == 0 ? DaisyAlertVariant.Success : DaisyAlertVariant.Warning);
    }

    /// <summary>Reasons why imported settings can give different images on this PC.</summary>
    private static List<string> ImportNotes(GeneratorSettings settings)
    {
        List<string> notes = [];
        if (settings.AppVersion is { } exported && exported != AppVersion)
            notes.Add(AppLocalization.Format("Run_SettingsOtherVersion", exported, AppVersion));
        if (settings.Watermark.IsActive)
        {
            var installed = new HashSet<string>(WatermarkRenderer.FontFamilies(), StringComparer.OrdinalIgnoreCase);
            notes.AddRange(settings.Watermark.Layers
                .Where(l => l is { Enabled: true, Kind: WatermarkLayerKind.Text } && !string.IsNullOrWhiteSpace(l.FontFamily) && !installed.Contains(l.FontFamily))
                .Select(l => l.FontFamily)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(font => AppLocalization.Format("Run_SettingsMissingFont", font)));
        }
        return notes;
    }

    /// <summary>Uses <paramref name="settings"/> from now on and builds the sections again from them, like at startup.</summary>
    private void ApplySettings(GeneratorSettings settings)
    {
        _initialized = false;
        StartButton.IsEnabled = false;
        GoToOutputButton.IsEnabled = false;
        _previewCts?.Cancel();
        _previewCts = null;
        AppSettings.ReplaceGenerator(settings);

        // The sections registered localizers, chips, cards and navigation buttons; drop the old ones before rebuilding.
        _localizers.RemoveRange(_sectionLocalizerStart, _localizers.Count - _sectionLocalizerStart);
        _chips.Clear();
        _cards.Clear();
        _styleGrids.Clear();
        _categoryChecks.Clear();
        _paletteActions.Clear();
        _sectionTargets.Clear();
        SectionNavigation.Children.Clear();
        SectionNavigation.Children.Add(HomeButton);
        Sections.Children.Clear();
        AddPlaceholders();

        CountInput.Value = Settings.Count;
        SeedInput.Text = Settings.Seed;
        ConfigScroller.ChangeView(null, 0, null, true);
        _buildSteps = SectionSteps().GetEnumerator();
        CompositionTarget.Rendering += OnBuildFrame;
    }
}
