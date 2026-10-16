using System.Globalization;

namespace AbstractImg.Core.Generation;

public enum ImageOrientation
{
    Landscape,
    Portrait
}

/// <summary>One concrete output size with a ratio label such as "16x9".</summary>
public readonly record struct SizeChoice(int Width, int Height, string Ratio)
{
    public string Orientation => Width == Height ? "square" : Width > Height ? "landscape" : "portrait";

    public double Megapixels => Width * (double)Height / 1_000_000.0;
}

/// <summary>A named preset with fixed pixel dimensions.</summary>
public readonly record struct FixedSizePreset(string Label, int Width, int Height)
{
    public string Value => $"{Width}x{Height}";
}

/// <summary>
/// Common aspect ratios, edge lengths and fixed dimensions, plus parsing of user-entered values.
/// </summary>
public static class SizeCatalog
{
    public const int MinEdge = 16;
    public const int MaxEdge = 16384;

    public static IReadOnlyList<string> AspectRatios { get; } = ["1:1", "5:4", "4:3", "3:2", "16:10", "16:9", "2:1", "21:9", "32:9"];

    public static IReadOnlyList<int> LongEdges { get; } = [640, 1024, 1280, 1920, 2560, 3840, 5120, 7680];

    public static IReadOnlyList<FixedSizePreset> FixedSizes { get; } =
    [
        new("Thumbnail", 320, 240),
        new("VGA", 640, 480),
        new("HD", 1280, 720),
        new("Full HD", 1920, 1080),
        new("QHD", 2560, 1440),
        new("4K UHD", 3840, 2160),
        new("8 MP", 3264, 2448),
        new("12 MP", 4000, 3000),
        new("24 MP", 6000, 4000),
        new("Phone", 1179, 2556),
        new("Portrait 4:5", 1080, 1350),
        new("Square", 2048, 2048),
        new("A4 300 dpi", 2480, 3508)
    ];

    /// <summary>Parses "16:9", "16x9" or "1.85" (as 1.85:1).</summary>
    public static bool TryParseRatio(string? text, out double width, out double height)
    {
        width = height = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().Split([':', 'x', 'X', '×', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && double.TryParse(parts[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
        {
            width = single;
            height = 1;
        }
        else if (parts.Length == 2
                 && double.TryParse(parts[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                 && double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            width = w;
            height = h;
        }
        else
        {
            return false;
        }
        return width > 0 && height > 0 && width / height is >= 0.05 and <= 20;
    }

    /// <summary>Normalizes a ratio for display and storage ("16:9", landscape first).</summary>
    public static string NormalizeRatio(double width, double height)
    {
        if (width < height)
            (width, height) = (height, width);
        static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{F(width)}:{F(height)}";
    }

    /// <summary>Parses "1920x1080", "1920 × 1080" or "1920*1080".</summary>
    public static bool TryParseSize(string? text, out int width, out int height)
    {
        width = height = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().Split(['x', 'X', '×', '*', ',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
               && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
               && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
               && width is >= MinEdge and <= MaxEdge
               && height is >= MinEdge and <= MaxEdge;
    }

    public static bool TryParseEdge(string? text, out int edge)
    {
        edge = 0;
        return int.TryParse(text?.Trim().TrimEnd('p', 'x', 'P', 'X').Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out edge)
               && edge is >= MinEdge and <= MaxEdge;
    }

    /// <summary>Short ratio label for file names, e.g. "16x9" or "1.85x1".</summary>
    public static string RatioLabel(int width, int height)
    {
        var g = Gcd(width, height);
        var w = width / g;
        var h = height / g;
        if (w <= 64 && h <= 64)
            return $"{w}x{h}";
        // Snap to a known ratio when close.
        foreach (var ratio in AspectRatios)
        {
            TryParseRatio(ratio, out var rw, out var rh);
            if (Math.Abs(width / (double)height - rw / rh) < 0.01)
                return $"{rw.ToString(CultureInfo.InvariantCulture)}x{rh.ToString(CultureInfo.InvariantCulture)}";
            if (Math.Abs(width / (double)height - rh / rw) < 0.01)
                return $"{rh.ToString(CultureInfo.InvariantCulture)}x{rw.ToString(CultureInfo.InvariantCulture)}";
        }
        return width >= height
            ? $"{(width / (double)height).ToString("0.##", CultureInfo.InvariantCulture)}x1"
            : $"1x{(height / (double)width).ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>
    /// All sizes that can be picked: every ratio × orientation × long edge, plus fixed sizes. Duplicates are removed.
    /// </summary>
    public static List<SizeChoice> BuildPool(IEnumerable<string> ratios, IEnumerable<ImageOrientation> orientations, IEnumerable<int> longEdges, IEnumerable<string> fixedSizes)
    {
        var result = new List<SizeChoice>();
        var seen = new HashSet<(int, int)>();
        var orientationList = orientations.Distinct().ToList();
        var edgeList = longEdges.Where(e => e is >= MinEdge and <= MaxEdge).Distinct().ToList();

        foreach (var ratio in ratios.Distinct())
        {
            if (!TryParseRatio(ratio, out var rw, out var rh))
                continue;
            if (rw < rh)
                (rw, rh) = (rh, rw);
            var square = Math.Abs(rw - rh) < 1e-6;
            foreach (var edge in edgeList)
            {
                var shortEdge = Math.Max(MinEdge, (int)Math.Round(edge * rh / rw));
                if (square)
                {
                    Add(edge, edge);
                    continue;
                }
                foreach (var orientation in orientationList)
                {
                    if (orientation == ImageOrientation.Landscape)
                        Add(edge, shortEdge);
                    else
                        Add(shortEdge, edge);
                }
            }
        }

        foreach (var text in fixedSizes.Distinct())
        {
            if (TryParseSize(text, out var w, out var h))
                Add(w, h);
        }

        return result;

        void Add(int w, int h)
        {
            if (seen.Add((w, h)))
                result.Add(new SizeChoice(w, h, RatioLabel(w, h)));
        }
    }
}
