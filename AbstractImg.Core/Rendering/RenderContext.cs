using AbstractImg.Core.Palettes;
using SkiaSharp;

namespace AbstractImg.Core.Rendering;

/// <summary>
/// Everything a style needs to paint one image: canvas, size, randomness, palette and detail level.
/// Sizes are expressed relative to <see cref="U"/> (one thousandth of the shorter edge) so every style
/// looks the same from thumbnails up to poster sizes.
/// </summary>
public sealed class RenderContext(SKCanvas canvas, int width, int height, Random rng, Palette palette, float detail)
{
    private SimplexNoise? _noise;

    public SKCanvas Canvas { get; } = canvas;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public Random Rng { get; } = rng;

    public Palette Palette { get; } = palette;

    /// <summary>0 (sparse) .. 1 (dense).</summary>
    public float Detail { get; } = detail;

    public float Min => Math.Min(Width, Height);

    public float Max => Math.Max(Width, Height);

    /// <summary>One thousandth of the shorter edge.</summary>
    public float U => Min / 1000f;

    public SKPoint Center => new(Width / 2f, Height / 2f);

    public SKRect Bounds => new(0, 0, Width, Height);

    /// <summary>Lazily created noise seeded from the image random generator.</summary>
    public SimplexNoise Noise => _noise ??= new SimplexNoise(Rng.Next());

    public float R(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

    public float R01() => (float)Rng.NextDouble();

    public int Ri(int minInclusive, int maxExclusive) => Rng.Next(minInclusive, maxExclusive);

    public bool Chance(double probability) => Rng.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[Rng.Next(items.Count)];

    public SKColor Color() => Palette.Pick(Rng);

    /// <summary>Gaussian random number (Box–Muller).</summary>
    public float Gauss(float mean = 0, float sd = 1)
    {
        var u1 = 1.0 - Rng.NextDouble();
        var u2 = Rng.NextDouble();
        return mean + sd * (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }

    /// <summary>Linear interpolation of an element count by detail level.</summary>
    public int Count(int sparse, int dense) => (int)MathF.Round(sparse + (dense - sparse) * Detail);

    /// <summary>Scales a count with the image area relative to a 1 MP reference, within limits.</summary>
    public int AreaCount(int perMegapixel, int min = 1, int max = int.MaxValue)
    {
        var mp = Width * (double)Height / 1_000_000.0;
        return (int)Math.Clamp(perMegapixel * Math.Sqrt(mp) * (0.5 + Detail), min, max);
    }

    public SKPaint Fill(SKColor color) => new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };

    public SKPaint Stroke(SKColor color, float width) => new()
    {
        IsAntialias = true,
        Color = color,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    public void Clear(SKColor? color = null) => Canvas.Clear(color ?? Palette.Background);
}
