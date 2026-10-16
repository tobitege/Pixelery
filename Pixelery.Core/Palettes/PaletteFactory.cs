using SkiaSharp;

namespace Pixelery.Core.Palettes;

/// <summary>
/// Creates palettes from curated sets and from color-theory generators.
/// </summary>
public static class PaletteFactory
{
    private static readonly (string Name, string[] Colors)[] Curated =
    [
        ("Sunset Boulevard", ["#FF6B6B", "#FFA36C", "#FFD166", "#06D6A0", "#118AB2"]),
        ("Nordic Fjord", ["#2E3440", "#5E81AC", "#88C0D0", "#A3BE8C", "#ECEFF4"]),
        ("Bauhaus", ["#E63946", "#F1C40F", "#1D3557", "#F1FAEE", "#111111"]),
        ("Miami", ["#FF006E", "#FB5607", "#FFBE0B", "#3A86FF", "#8338EC"]),
        ("Desert Bloom", ["#E07A5F", "#3D405B", "#81B29A", "#F2CC8F", "#F4F1DE"]),
        ("Deep Ocean", ["#03045E", "#0077B6", "#00B4D8", "#90E0EF", "#CAF0F8"]),
        ("Forest Floor", ["#283618", "#606C38", "#DDA15E", "#BC6C25", "#FEFAE0"]),
        ("Berry Smoothie", ["#5F0F40", "#9A031E", "#FB8B24", "#E36414", "#0F4C5C"]),
        ("Cotton Candy", ["#CDB4DB", "#FFC8DD", "#FFAFCC", "#BDE0FE", "#A2D2FF"]),
        ("Retro Diner", ["#264653", "#2A9D8F", "#E9C46A", "#F4A261", "#E76F51"]),
        ("Ultraviolet", ["#10002B", "#3C096C", "#7B2CBF", "#C77DFF", "#E0AAFF"]),
        ("Citrus Pop", ["#F94144", "#F3722C", "#F8961E", "#F9C74F", "#90BE6D"]),
        ("Glacier", ["#0B132B", "#1C2541", "#3A506B", "#5BC0BE", "#E0FBFC"]),
        ("Terracotta", ["#582F0E", "#7F4F24", "#936639", "#A68A64", "#C2C5AA"]),
        ("Matisse", ["#1B4965", "#FF595E", "#FFCA3A", "#8AC926", "#F7F3E3"]),
        ("Vaporwave", ["#FF71CE", "#01CDFE", "#05FFA1", "#B967FF", "#FFFB96"]),
        ("Ink & Paper", ["#1A1A1A", "#3D3D3D", "#8C8C8C", "#D9D4C7", "#F5F0E6"]),
        ("Koi Pond", ["#003049", "#D62828", "#F77F00", "#FCBF49", "#EAE2B7"]),
        ("Mint Chocolate", ["#3E2723", "#6D4C41", "#A5D6A7", "#E8F5E9", "#26A69A"]),
        ("Lagoon", ["#001219", "#005F73", "#0A9396", "#94D2BD", "#E9D8A6"]),
        ("Ember", ["#370617", "#6A040F", "#D00000", "#E85D04", "#FFBA08"]),
        ("Lavender Fields", ["#2D2A4A", "#6A5ACD", "#9D8DF1", "#B8CDF8", "#F5E6FF"]),
        ("Pop Art", ["#FFE600", "#FF2E63", "#08D9D6", "#252A34", "#EAEAEA"]),
        ("Autumn Walk", ["#540B0E", "#9E2A2B", "#E09F3E", "#FFF3B0", "#335C67"]),
        ("Cyberpunk", ["#0D0221", "#0F084B", "#26408B", "#A6CFD5", "#FF00A0"]),
        ("Sage Garden", ["#344E41", "#3A5A40", "#588157", "#A3B18A", "#DAD7CD"]),
        ("Peach Fuzz", ["#FFBE98", "#FF9B85", "#FFD6A5", "#FDFFB6", "#9BF6FF"]),
        ("Midnight Gold", ["#000814", "#001D3D", "#003566", "#FFC300", "#FFD60A"]),
        ("Rothko", ["#8C1C13", "#BF4342", "#E7D7C1", "#A78A7F", "#735751"]),
        ("Hokusai", ["#0B2545", "#13315C", "#8DA9C4", "#EEF4ED", "#D1495B"]),
        ("Candy Shop", ["#FF4D6D", "#FF8FA3", "#FFB3C1", "#7AE7C7", "#4361EE"]),
        ("Moss & Stone", ["#2F3E46", "#354F52", "#52796F", "#84A98C", "#CAD2C5"]),
        ("Saffron", ["#4A1942", "#893168", "#C76D7E", "#F2A541", "#F7E9B7"]),
        ("Electric Blue", ["#03071E", "#023E8A", "#0096C7", "#48CAE4", "#F72585"]),
        ("Sorbet", ["#F8B195", "#F67280", "#C06C84", "#6C5B7B", "#355C7D"]),
        ("Kandinsky", ["#14213D", "#FCA311", "#E5E5E5", "#D62828", "#2EC4B6"])
    ];

    public static int CuratedCount => Curated.Length;

    public static IEnumerable<string> CuratedNames => Curated.Select(c => c.Name);

    public static Palette Create(Random rng, PaletteFamily family, BackgroundTone tone)
    {
        var (name, colors) = family switch
        {
            PaletteFamily.Curated => FromCurated(rng),
            PaletteFamily.Harmony => FromHarmony(rng),
            PaletteFamily.Cosine => FromCosine(rng),
            PaletteFamily.Pastel => FromPastel(rng),
            PaletteFamily.Neon => FromNeon(rng),
            PaletteFamily.Earth => FromEarth(rng),
            _ => FromMonochrome(rng)
        };

        // Shuffle so the first color is not always the same role.
        var list = colors.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        var background = CreateBackground(rng, list, tone);
        // Keep foreground colors that are distinguishable from the background.
        var visible = list.Where(c => Distance(c, background) > 0.08f).ToList();
        if (visible.Count < 2)
            visible = list;
        return new Palette(name, background, visible, tone);
    }

    private static float Distance(SKColor a, SKColor b)
    {
        var la = ColorMath.ToOkLab(a);
        var lb = ColorMath.ToOkLab(b);
        return MathF.Sqrt((la.L - lb.L) * (la.L - lb.L) + (la.A - lb.A) * (la.A - lb.A) + (la.B - lb.B) * (la.B - lb.B));
    }

    private static SKColor CreateBackground(Random rng, List<SKColor> colors, BackgroundTone tone)
    {
        var reference = colors[rng.Next(colors.Count)];
        var (_, c, h) = ColorMath.ToOkLch(reference);
        return tone switch
        {
            BackgroundTone.Dark => ColorMath.FromOkLch(0.12f + (float)rng.NextDouble() * 0.1f, Math.Min(c, 0.05f), h),
            BackgroundTone.Light => ColorMath.FromOkLch(0.95f + (float)rng.NextDouble() * 0.03f, Math.Min(c, 0.025f), h),
            _ => VividBackground(rng, colors)
        };
    }

    private static SKColor VividBackground(Random rng, List<SKColor> colors)
    {
        // The most saturated palette color, slightly deepened so foreground shapes stay visible.
        var pick = colors.OrderByDescending(c => ColorMath.ToOkLch(c).C).Take(2).ElementAt(rng.Next(Math.Min(2, colors.Count)));
        var (l, c, h) = ColorMath.ToOkLch(pick);
        return ColorMath.FromOkLch(Math.Clamp(l * 0.85f, 0.3f, 0.62f), c * 0.8f, h);
    }

    private static (string, SKColor[]) FromCurated(Random rng)
    {
        var (name, hex) = Curated[rng.Next(Curated.Length)];
        return (name, [.. hex.Select(SKColor.Parse)]);
    }

    private static float R(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private static (string, SKColor[]) FromHarmony(Random rng)
    {
        var baseHue = R(rng, 0, 360);
        string[] schemes = ["Analogous", "Complementary", "Split", "Triadic", "Tetradic"];
        var scheme = schemes[rng.Next(schemes.Length)];
        float[] offsets = scheme switch
        {
            "Analogous" => [0, 25, 50, -25, 75],
            "Complementary" => [0, 180, 15, 195, -15],
            "Split" => [0, 150, 210, 20, 170],
            "Triadic" => [0, 120, 240, 30, 150],
            _ => [0, 90, 180, 270, 45]
        };
        var chroma = R(rng, 0.1f, 0.2f);
        var colors = offsets.Select((o, i) =>
            ColorMath.FromOkLch(R(rng, 0.45f, 0.85f), chroma * R(rng, 0.7f, 1.2f), (baseHue + o + 360) % 360)).ToArray();
        return ($"{scheme} {baseHue:0}°", colors);
    }

    /// <summary>Inigo Quilez' cosine palettes: color(t) = a + b * cos(2π(c t + d)).</summary>
    private static (string, SKColor[]) FromCosine(Random rng)
    {
        var a = new[] { R(rng, 0.4f, 0.6f), R(rng, 0.4f, 0.6f), R(rng, 0.4f, 0.6f) };
        var b = new[] { R(rng, 0.3f, 0.5f), R(rng, 0.3f, 0.5f), R(rng, 0.3f, 0.5f) };
        var c = rng.NextDouble() < 0.5 ? new[] { 1f, 1f, 1f } : [R(rng, 0.5f, 1.5f), R(rng, 0.5f, 1.5f), R(rng, 0.5f, 1.5f)];
        var d = new[] { R(rng, 0, 1), R(rng, 0, 1), R(rng, 0, 1) };
        var count = rng.Next(5, 8);
        var colors = new SKColor[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)count;
            byte Ch(int k) => (byte)Math.Clamp((int)(255 * (a[k] + b[k] * MathF.Cos(MathF.Tau * (c[k] * t + d[k])))), 0, 255);
            colors[i] = new SKColor(Ch(0), Ch(1), Ch(2));
        }
        return ("Cosine", colors);
    }

    private static (string, SKColor[]) FromPastel(Random rng)
    {
        var start = R(rng, 0, 360);
        var step = R(rng, 35, 80);
        var colors = Enumerable.Range(0, 5)
            .Select(i => ColorMath.FromOkLch(R(rng, 0.82f, 0.92f), R(rng, 0.05f, 0.1f), (start + i * step) % 360))
            .ToArray();
        return ("Pastel", colors);
    }

    private static (string, SKColor[]) FromNeon(Random rng)
    {
        var start = R(rng, 0, 360);
        var colors = Enumerable.Range(0, 5)
            .Select(i => ColorMath.FromOkLch(R(rng, 0.65f, 0.8f), 0.32f, (start + i * R(rng, 50, 90)) % 360))
            .ToArray();
        return ("Neon", colors);
    }

    private static (string, SKColor[]) FromEarth(Random rng)
    {
        var colors = Enumerable.Range(0, 5)
            .Select(_ =>
            {
                var hue = rng.NextDouble() < 0.8 ? R(rng, 25, 110) : R(rng, 140, 200);
                return ColorMath.FromOkLch(R(rng, 0.35f, 0.85f), R(rng, 0.03f, 0.11f), hue);
            })
            .ToArray();
        return ("Earth", colors);
    }

    private static (string, SKColor[]) FromMonochrome(Random rng)
    {
        var hue = R(rng, 0, 360);
        var chroma = rng.NextDouble() < 0.15 ? 0f : R(rng, 0.06f, 0.18f);
        var colors = Enumerable.Range(0, 5)
            .Select(i => ColorMath.FromOkLch(0.3f + i * 0.14f, chroma * (1 - i * 0.08f), hue + R(rng, -6, 6)))
            .ToArray();
        return (chroma == 0 ? "Grayscale" : "Monochrome", colors);
    }
}
