using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Core.Watermark;

namespace Pixelery.Core.Generation;

public enum DetailLevel
{
    Low,
    Medium,
    High
}

public enum OutputFormat
{
    Jpeg,
    Png,
    Webp
}

/// <summary>
/// Everything that controls a generation run. Multiple sizes cycle in order unless RandomSizes is enabled;
/// the other multiple-choice options are sampled for every image.
/// </summary>
public sealed class GeneratorSettings
{
    public const int CurrentFormatVersion = 1;
    public const string DefaultNamePattern = "{style}_{n:00000}_{w}x{h}";
    public const int MaxNamePatterns = 100;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string OutputFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pixelery");

    public List<string> Styles { get; set; } = [.. StyleCatalog.All.Select(s => s.Id)];

    /// <summary>
    /// Style ids that existed when the settings were last saved. Styles added to the app later are selected
    /// automatically on load (see <see cref="SelectNewStyles"/>), while styles the user deselected stay off.
    /// </summary>
    public List<string> KnownStyles { get; set; } = [];

    public List<PaletteFamily> Palettes { get; set; } = [PaletteFamily.Curated, PaletteFamily.Harmony, PaletteFamily.Cosine];

    public List<BackgroundTone> Tones { get; set; } = [BackgroundTone.Dark, BackgroundTone.Light, BackgroundTone.Vivid];

    public PaletteSet PaletteSet { get; set; } = new();

    public bool UsePaletteSet { get; set; }

    public List<DetailLevel> Details { get; set; } = [DetailLevel.Medium, DetailLevel.High];

    public bool Grain { get; set; } = true;

    public bool Vignette { get; set; }

    public bool MultipleSizes { get; set; }

    public bool RandomSizes { get; set; }

    public SizeSelection InactiveSizeSelection { get; set; } = new();

    public List<string> AspectRatios { get; set; } = ["16:9"];

    public List<string> CustomAspectRatios { get; set; } = [];

    public List<ImageOrientation> Orientations { get; set; } = [ImageOrientation.Landscape];

    public List<int> LongEdges { get; set; } = [1920];

    public List<int> CustomLongEdges { get; set; } = [];

    public List<string> FixedSizes { get; set; } = [];

    public List<string> CustomFixedSizes { get; set; } = [];

    public List<OutputFormat> Formats { get; set; } = [OutputFormat.Jpeg];

    public int Quality { get; set; } = 90;

    public string NamePattern { get; set; } = DefaultNamePattern;

    /// <summary>Local pattern collection. Shared files contain only <see cref="NamePattern"/>.</summary>
    public List<string> NamePatterns { get; set; } = [DefaultNamePattern];

    public int StartIndex { get; set; } = 1;

    public bool Overwrite { get; set; }

    public bool SpreadDates { get; set; }

    public DateOnly SpreadFrom { get; set; } = new(DateTime.Today.Year, 1, 1);

    public DateOnly SpreadTo { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public bool WriteExif { get; set; } = true;

    public int Count { get; set; } = 100;

    public int Threads { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Text and image watermarks drawn on every generated image.</summary>
    public WatermarkSettings Watermark { get; set; } = new();

    /// <summary>Master seed as text; empty means a new random seed for every run.</summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>Day for {now} and image dates when spreading is off; empty means the time of the run.</summary>
    public DateOnly? ReferenceDate { get; set; }

    /// <summary>Pixelery version that exported these settings; empty in the app's own settings.</summary>
    public string? AppVersion { get; set; }

    /// <summary>The "now" of a run: the end of <see cref="ReferenceDate"/>, or the current time.</summary>
    public DateTime ReferenceTime() => ReferenceDate?.ToDateTime(new TimeOnly(23, 59, 59)) ?? DateTime.Now;

    /// <summary>Range endpoints identify whole calendar months, in chronological order.</summary>
    public bool HasValidDateRange() => SpreadFrom.Year >= 1601 && SpreadFrom.Day == 1
        && SpreadTo.Day == 1 && SpreadFrom <= SpreadTo;

    /// <summary>Selects styles that were added since the settings were saved and marks all current styles as known.</summary>
    public void SelectNewStyles()
    {
        var knownSet = new HashSet<string>(KnownStyles, StringComparer.OrdinalIgnoreCase);
        foreach (var style in StyleCatalog.All.Where(s => !knownSet.Contains(s.Id) && !Styles.Contains(s.Id)))
            Styles.Add(style.Id);
        KnownStyles = [.. StyleCatalog.All.Select(s => s.Id)];
    }

    public List<SizeChoice> BuildSizePool() =>
        SizeCatalog.BuildPool(AspectRatios, Orientations, LongEdges, FixedSizes);

    public SizeSelection CaptureSizeSelection() => new()
    {
        AspectRatios = [.. AspectRatios], Orientations = [.. Orientations],
        LongEdges = [.. LongEdges], FixedSizes = [.. FixedSizes]
    };

    public void SetMultipleSizes(bool enabled)
    {
        if (MultipleSizes == enabled)
            return;
        var previous = CaptureSizeSelection();
        AspectRatios = [.. InactiveSizeSelection.AspectRatios];
        Orientations = [.. InactiveSizeSelection.Orientations];
        LongEdges = [.. InactiveSizeSelection.LongEdges];
        FixedSizes = [.. InactiveSizeSelection.FixedSizes];
        InactiveSizeSelection = previous;
        MultipleSizes = enabled;
        RandomSizes = false;
    }

    public void SelectOrientation(ImageOrientation value)
    {
        if (!MultipleSizes)
            Orientations = [value];
        else if (!Orientations.Contains(value))
            Orientations.Add(value);
        else if (Orientations.Count > 1)
            Orientations.Remove(value);
    }

    public void SelectRatio(string value, bool selected)
    {
        Select(AspectRatios, value, selected);
        if (!MultipleSizes && selected)
        {
            FixedSizes.Clear();
        }
        EnsureSingleSize();
    }

    public void SelectLongEdge(int value, bool selected)
    {
        Select(LongEdges, value, selected);
        if (!MultipleSizes && selected)
        {
            FixedSizes.Clear();
        }
        EnsureSingleSize();
    }

    public void SelectFixedSize(string value, bool selected)
    {
        Select(FixedSizes, value, selected);
        if (!MultipleSizes && selected)
        {
            AspectRatios.Clear();
            LongEdges.Clear();
        }
        EnsureSingleSize();
    }

    private void Select<T>(List<T> values, T value, bool selected)
    {
        if (!MultipleSizes && !selected) return;
        if (!MultipleSizes && selected) values.Clear();
        values.Remove(value);
        if (selected) values.Add(value);
    }

    private void EnsureSingleSize()
    {
        if (MultipleSizes || FixedSizes.Count != 0)
            return;
        if (AspectRatios.Count == 0) AspectRatios.Add("16:9");
        if (LongEdges.Count == 0) LongEdges.Add(1920);
    }

    public void RemoveCustomRatio(string value)
    {
        CustomAspectRatios.Remove(value);
        AspectRatios.Remove(value);
        InactiveSizeSelection.AspectRatios.Remove(value);
        RepairSizeSelections();
    }

    public void RemoveCustomLongEdge(int value)
    {
        CustomLongEdges.Remove(value);
        LongEdges.Remove(value);
        InactiveSizeSelection.LongEdges.Remove(value);
        RepairSizeSelections();
    }

    public void RemoveCustomFixedSize(string value)
    {
        CustomFixedSizes.Remove(value);
        FixedSizes.Remove(value);
        InactiveSizeSelection.FixedSizes.Remove(value);
        RepairSizeSelections();
    }

    private void RepairSizeSelections()
    {
        EnsureSingleSize();
        if (!MultipleSizes || InactiveSizeSelection.FixedSizes.Count != 0) return;
        if (InactiveSizeSelection.AspectRatios.Count == 0) InactiveSizeSelection.AspectRatios.Add("16:9");
        if (InactiveSizeSelection.LongEdges.Count == 0) InactiveSizeSelection.LongEdges.Add(1920);
    }

    public bool TryAddNamePattern(string? value)
    {
        var pattern = value?.Trim();
        if (!NameTemplate.TryParse(pattern, out _, out _))
            return false;
        var existing = NamePatterns.Find(p => p.Equals(pattern, StringComparison.Ordinal));
        if (existing == null)
        {
            if (NamePatterns.Count >= MaxNamePatterns)
                return false;
            NamePatterns.Add(pattern!);
        }
        NamePattern = existing ?? pattern!;
        return true;
    }

    public void RemoveNamePattern()
    {
        if (NamePatterns.Count <= 1)
            return;
        var index = NamePatterns.FindIndex(p => p.Equals(NamePattern, StringComparison.Ordinal));
        if (index < 0)
            return;
        NamePatterns.RemoveAt(index);
        NamePattern = NamePatterns[Math.Min(index, NamePatterns.Count - 1)];
    }

    public GeneratorSettings Clone()
    {
        var clone = (GeneratorSettings)MemberwiseClone();
        clone.Styles = [.. Styles];
        clone.KnownStyles = [.. KnownStyles];
        clone.Palettes = [.. Palettes];
        clone.Tones = [.. Tones];
        clone.PaletteSet = PaletteSet.Clone();
        clone.Details = [.. Details];
        clone.AspectRatios = [.. AspectRatios];
        clone.CustomAspectRatios = [.. CustomAspectRatios];
        clone.Orientations = [.. Orientations];
        clone.LongEdges = [.. LongEdges];
        clone.CustomLongEdges = [.. CustomLongEdges];
        clone.FixedSizes = [.. FixedSizes];
        clone.InactiveSizeSelection = InactiveSizeSelection.Clone();
        clone.CustomFixedSizes = [.. CustomFixedSizes];
        clone.Formats = [.. Formats];
        clone.NamePatterns = [.. NamePatterns];
        clone.Watermark = Watermark.Clone();
        return clone;
    }

    /// <summary>Returns validation problems as keys (localized by the app); empty when the settings can run.</summary>
    public List<string> Validate()
    {
        List<string> problems = [];
        if (string.IsNullOrWhiteSpace(OutputFolder) || !Path.IsPathFullyQualified(OutputFolder))
            problems.Add("Validation_Folder");
        if (!Styles.Any(id => StyleCatalog.Find(id) != null))
            problems.Add("Validation_Styles");
        if (UsePaletteSet ? !PaletteSet.IsValid() : Palettes.Count == 0)
            problems.Add("Validation_Palettes");
        if (!UsePaletteSet && Tones.Count == 0)
            problems.Add("Validation_Tones");
        if (Details.Count == 0)
            problems.Add("Validation_Details");
        if (!CaptureSizeSelection().IsValid(MultipleSizes) || (RandomSizes && !MultipleSizes) || BuildSizePool().Count == 0)
            problems.Add("Validation_Sizes");
        if (Formats.Count == 0)
            problems.Add("Validation_Formats");
        if (!NameTemplate.TryParse(NamePattern, out _, out _))
            problems.Add("Validation_Name");
        if (Count < 1)
            problems.Add("Validation_Count");
        if (!HasValidDateRange())
            problems.Add("Validation_DateRange");
        if (Watermark.IsActive && Watermark.Layers.Any(l => l is { Enabled: true, Kind: WatermarkLayerKind.Image } && !WatermarkRenderer.CanLoadImage(l.ImagePath)))
            problems.Add("Validation_WatermarkImage");
        return problems;
    }
}
