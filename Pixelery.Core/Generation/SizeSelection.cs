namespace Pixelery.Core.Generation;

/// <summary>The size choices remembered for the inactive single- or multiple-selection mode.</summary>
public sealed class SizeSelection
{
    public List<string> AspectRatios { get; set; } = ["16:9"];
    public List<ImageOrientation> Orientations { get; set; } = [ImageOrientation.Landscape];
    public List<int> LongEdges { get; set; } = [1920];
    public List<string> FixedSizes { get; set; } = [];

    public SizeSelection Clone() => new()
    {
        AspectRatios = [.. AspectRatios],
        Orientations = [.. Orientations],
        LongEdges = [.. LongEdges],
        FixedSizes = [.. FixedSizes]
    };

    public bool IsValid(bool multiple) =>
        AspectRatios != null && AspectRatios.All(r => SizeCatalog.TryParseRatio(r, out _, out _))
        && Orientations != null && Orientations.Count > 0 && Orientations.All(Enum.IsDefined)
        && LongEdges != null && LongEdges.All(e => e is >= SizeCatalog.MinEdge and <= SizeCatalog.MaxEdge)
        && FixedSizes != null && FixedSizes.All(s => SizeCatalog.TryParseSize(s, out _, out _))
        && (multiple || (Orientations.Count == 1 &&
            ((FixedSizes.Count == 1 && AspectRatios.Count == 0 && LongEdges.Count == 0)
             || (FixedSizes.Count == 0 && AspectRatios.Count == 1 && LongEdges.Count == 1))));
}
