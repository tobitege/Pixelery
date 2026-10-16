using SkiaSharp;

namespace AbstractImg.Core.Palettes;

/// <summary>Where palettes come from. Several families can be enabled; one is picked per image.</summary>
public enum PaletteFamily
{
    Curated,
    Harmony,
    Cosine,
    Pastel,
    Neon,
    Earth,
    Monochrome
}

/// <summary>Base tone of the image background.</summary>
public enum BackgroundTone
{
    Dark,
    Light,
    Vivid
}

/// <summary>
/// Colors for one image: a background, an ordered set of foreground colors and an ink color
/// that contrasts with the background (lines, outlines).
/// </summary>
public sealed class Palette
{
    public Palette(string name, SKColor background, IReadOnlyList<SKColor> colors, BackgroundTone tone)
    {
        Name = name;
        Background = background;
        Colors = colors;
        Tone = tone;
        IsDark = ColorMath.Luminance(background) < 0.55f;
        Ink = IsDark ? ColorMath.FromOkLch(0.96f, 0.01f, ColorMath.ToOkLch(background).H) : ColorMath.FromOkLch(0.18f, 0.02f, ColorMath.ToOkLch(background).H);
    }

    public string Name { get; }

    public SKColor Background { get; }

    public IReadOnlyList<SKColor> Colors { get; }

    public BackgroundTone Tone { get; }

    public bool IsDark { get; }

    /// <summary>High-contrast color for lines and outlines on the background.</summary>
    public SKColor Ink { get; }

    public SKColor this[int index] => Colors[((index % Colors.Count) + Colors.Count) % Colors.Count];

    public SKColor Pick(Random rng) => Colors[rng.Next(Colors.Count)];

    /// <summary>Picks a color that differs from <paramref name="avoid"/> when possible.</summary>
    public SKColor PickOther(Random rng, SKColor avoid)
    {
        for (var i = 0; i < 6; i++)
        {
            var c = Pick(rng);
            if (c != avoid)
                return c;
        }
        return Pick(rng);
    }

    /// <summary>Samples the palette as a continuous gradient (OKLab interpolation), t in 0..1.</summary>
    public SKColor Sample(float t, bool cyclic = false)
    {
        var count = Colors.Count;
        if (count == 1)
            return Colors[0];
        t = cyclic ? t - MathF.Floor(t) : Math.Clamp(t, 0, 1);
        var segments = cyclic ? count : count - 1;
        var x = t * segments;
        var i = Math.Min((int)x, segments - 1);
        var f = x - i;
        return ColorMath.Mix(Colors[i % count], Colors[(i + 1) % count], f);
    }

    /// <summary>Colors with the background prepended, useful for full-bleed gradients.</summary>
    public SKColor[] WithBackground() => [Background, .. Colors];

    /// <summary>
    /// A horizontal 256px gradient shader of the palette for runtime shaders (sample with x = t * 256).
    /// </summary>
    public SKShader CreateRamp(bool cyclic = false, bool includeBackground = false)
    {
        var colors = includeBackground ? WithBackground() : [.. Colors];
        if (cyclic)
            colors = [.. colors, colors[0]];
        var stops = new SKColor[64];
        var src = new Palette(Name, Background, colors, Tone);
        for (var i = 0; i < stops.Length; i++)
            stops[i] = src.Sample(i / (stops.Length - 1f));
        return SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(256, 0), stops, null, SKShaderTileMode.Clamp);
    }
}
