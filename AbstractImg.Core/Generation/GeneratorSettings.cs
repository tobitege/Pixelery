using AbstractImg.Core.Palettes;
using AbstractImg.Core.Styles;

namespace AbstractImg.Core.Generation;

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
/// Everything that controls a generation run. Lists are "multiple choice": when several values are enabled,
/// one is picked at random for every image.
/// </summary>
public sealed class GeneratorSettings
{
    public string OutputFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AbstractImg");

    public List<string> Styles { get; set; } = [.. StyleCatalog.All.Select(s => s.Id)];

    /// <summary>
    /// Style ids that existed when the settings were last saved. Styles added to the app later are selected
    /// automatically on load (see <see cref="SelectNewStyles"/>), while styles the user deselected stay off.
    /// </summary>
    public List<string> KnownStyles { get; set; } = [];

    public List<PaletteFamily> Palettes { get; set; } = [PaletteFamily.Curated, PaletteFamily.Harmony, PaletteFamily.Cosine];

    public List<BackgroundTone> Tones { get; set; } = [BackgroundTone.Dark, BackgroundTone.Light, BackgroundTone.Vivid];

    public List<DetailLevel> Details { get; set; } = [DetailLevel.Medium, DetailLevel.High];

    public bool Grain { get; set; } = true;

    public bool Vignette { get; set; }

    public List<string> AspectRatios { get; set; } = ["4:3", "3:2", "16:9"];

    public List<string> CustomAspectRatios { get; set; } = [];

    public List<ImageOrientation> Orientations { get; set; } = [ImageOrientation.Landscape, ImageOrientation.Portrait];

    public List<int> LongEdges { get; set; } = [1920, 3840];

    public List<int> CustomLongEdges { get; set; } = [];

    public List<string> FixedSizes { get; set; } = [];

    public List<string> CustomFixedSizes { get; set; } = [];

    public List<OutputFormat> Formats { get; set; } = [OutputFormat.Jpeg];

    public int Quality { get; set; } = 90;

    public string NamePattern { get; set; } = "{style}_{n:00000}_{w}x{h}";

    public int StartIndex { get; set; } = 1;

    public bool Overwrite { get; set; }

    public bool SpreadDates { get; set; } = true;

    public int SpreadYears { get; set; } = 5;

    public bool WriteExif { get; set; } = true;

    public int Count { get; set; } = 100;

    public int Threads { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Master seed as text; empty means a new random seed for every run.</summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>The styles of the first release; settings files without <see cref="KnownStyles"/> knew exactly these.</summary>
    private static readonly string[] FirstReleaseStyles =
    [
        "mesh-gradient", "aurora", "gradient-field", "bokeh", "nebula", "bauhaus", "truchet", "low-poly", "subdivision",
        "circle-packing", "memphis", "isometric", "halftone", "kaleidoscope", "op-art", "flow-field", "marble", "topographic",
        "watercolor", "paper-layers", "ridgelines", "stained-glass", "plasma", "fractal", "harmonograph", "synthwave",
        "glitch", "pixel-mosaic", "neon-glow"
    ];

    /// <summary>Selects styles that were added since the settings were saved and marks all current styles as known.</summary>
    public void SelectNewStyles()
    {
        IEnumerable<string> known = KnownStyles.Count > 0 ? KnownStyles : FirstReleaseStyles;
        var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
        foreach (var style in StyleCatalog.All.Where(s => !knownSet.Contains(s.Id) && !Styles.Contains(s.Id)))
            Styles.Add(style.Id);
        KnownStyles = [.. StyleCatalog.All.Select(s => s.Id)];
    }

    public List<SizeChoice> BuildSizePool() =>
        SizeCatalog.BuildPool(AspectRatios, Orientations, LongEdges, FixedSizes);

    public GeneratorSettings Clone()
    {
        var clone = (GeneratorSettings)MemberwiseClone();
        clone.Styles = [.. Styles];
        clone.KnownStyles = [.. KnownStyles];
        clone.Palettes = [.. Palettes];
        clone.Tones = [.. Tones];
        clone.Details = [.. Details];
        clone.AspectRatios = [.. AspectRatios];
        clone.CustomAspectRatios = [.. CustomAspectRatios];
        clone.Orientations = [.. Orientations];
        clone.LongEdges = [.. LongEdges];
        clone.CustomLongEdges = [.. CustomLongEdges];
        clone.FixedSizes = [.. FixedSizes];
        clone.CustomFixedSizes = [.. CustomFixedSizes];
        clone.Formats = [.. Formats];
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
        if (Palettes.Count == 0)
            problems.Add("Validation_Palettes");
        if (Tones.Count == 0)
            problems.Add("Validation_Tones");
        if (Details.Count == 0)
            problems.Add("Validation_Details");
        if (BuildSizePool().Count == 0)
            problems.Add("Validation_Sizes");
        if (Formats.Count == 0)
            problems.Add("Validation_Formats");
        if (!NameTemplate.TryParse(NamePattern, out _, out _))
            problems.Add("Validation_Name");
        if (Count < 1)
            problems.Add("Validation_Count");
        return problems;
    }
}
