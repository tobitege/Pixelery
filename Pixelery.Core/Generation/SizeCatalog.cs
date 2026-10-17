using System.Globalization;
using System.Text.RegularExpressions;

namespace Pixelery.Core.Generation;

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
public static partial class SizeCatalog
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

    // Editing permits incomplete values (for example "16:"); submission still uses the parsers below.
    public static bool IsRatioInput(string text) => text.Length <= 64 && RatioInputPattern().IsMatch(text);

    public static bool IsEdgeInput(string text) => text.Length <= 5 && text.All(char.IsAsciiDigit);

    public static bool IsSizeInput(string text) => text.Length <= 64 && SizeInputPattern().IsMatch(text);

    [GeneratedRegex(@"\A *[0-9]*(?:[.,٫][0-9]*)? *(?:[:xX×/] *[0-9]*(?:[.,٫][0-9]*)? *)?\z")]
    private static partial Regex RatioInputPattern();

    [GeneratedRegex(@"\A *[0-9]{0,5}(?: *[xX×*,] *[0-9]{0,5}| +[0-9]{0,5})? *\z")]
    private static partial Regex SizeInputPattern();

    /// <summary>Parses "16:9", "16x9" or "1.85" (as 1.85:1).</summary>
    public static bool TryParseRatio(string? text, out double width, out double height)
    {
        width = height = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().Replace(',', '.').Replace('٫', '.')
            .Split([':', 'x', 'X', '×', '/'], StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
        {
            width = single;
            height = 1;
        }
        else if (parts.Length == 2
                 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
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

    /// <summary>Normalizes a ratio for culture-independent storage ("16:9", landscape first).</summary>
    public static string NormalizeRatio(double width, double height)
    {
        if (width < height)
            (width, height) = (height, width);
        static string F(double v) => v.ToString("G", CultureInfo.InvariantCulture);
        return $"{F(width)}:{F(height)}";
    }

    /// <summary>Displays a stored ratio with the selected culture's decimal separator.</summary>
    public static string FormatRatio(string ratio, CultureInfo culture) =>
        ratio.Replace(',', '.').Replace('٫', '.')
            .Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);

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

    /// <summary>
    /// Short ratio label for file names, e.g. "16x9", "6x13" or "2.39x1": the exact reduced fraction when small,
    /// otherwise the closest simple fraction (terms up to 32, within 0.5 %), otherwise a decimal.
    /// </summary>
    public static string RatioLabel(int width, int height)
    {
        var g = Gcd(width, height);
        var w = width / g;
        var h = height / g;
        if (w <= 32 && h <= 32)
            return $"{w}x{h}";

        var target = width / (double)height;
        (int W, int H, double Error) best = (0, 0, double.MaxValue);
        for (var dh = 1; dh <= 32; dh++)
        {
            var dw = (int)Math.Round(target * dh);
            if (dw < 1 || dw > 32)
                continue;
            var error = Math.Abs(dw / (double)dh - target) / target;
            // Strictly smaller error keeps the smallest terms (16x9 rather than 32x18).
            if (error < best.Error - 1e-12)
                best = (dw, dh, error);
        }
        if (best.Error < 0.005)
            return $"{best.W}x{best.H}";

        return width >= height
            ? $"{target.ToString("0.##", CultureInfo.InvariantCulture)}x1"
            : $"1x{(1 / target).ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Label of a ratio as entered (e.g. "7:5" → "7x5", portrait "5x7"), independent of pixel rounding.</summary>
    private static string RatioLabel(double longSide, double shortSide, bool portrait)
    {
        static string F(double v) => v.ToString("G", CultureInfo.InvariantCulture);
        return portrait ? $"{F(shortSide)}x{F(longSide)}" : $"{F(longSide)}x{F(shortSide)}";
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
                    Add(edge, edge, "1x1");
                    continue;
                }
                foreach (var orientation in orientationList)
                {
                    if (orientation == ImageOrientation.Landscape)
                        Add(edge, shortEdge, RatioLabel(rw, rh, portrait: false));
                    else
                        Add(shortEdge, edge, RatioLabel(rw, rh, portrait: true));
                }
            }
        }

        foreach (var text in fixedSizes.Distinct())
        {
            if (!TryParseSize(text, out var w, out var h))
                continue;
            var longSide = Math.Max(w, h);
            var shortSide = Math.Min(w, h);
            if (longSide == shortSide)
                Add(w, h, "1x1");
            else
                foreach (var orientation in orientationList)
                {
                    var portrait = orientation == ImageOrientation.Portrait;
                    var width = portrait ? shortSide : longSide;
                    var height = portrait ? longSide : shortSide;
                    Add(width, height, RatioLabel(width, height));
                }
        }

        return result;

        void Add(int w, int h, string ratio)
        {
            if (seen.Add((w, h)))
                result.Add(new SizeChoice(w, h, ratio));
        }
    }
}
