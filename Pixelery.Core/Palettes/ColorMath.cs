using SkiaSharp;

namespace Pixelery.Core.Palettes;

/// <summary>
/// Perceptual color helpers (OKLab / OKLCH by Björn Ottosson) used to build and blend palettes.
/// </summary>
public static class ColorMath
{
    public readonly record struct Lab(float L, float A, float B);

    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float ToGamma(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;

    public static Lab ToOkLab(SKColor color)
    {
        var r = ToLinear(color.Red / 255f);
        var g = ToLinear(color.Green / 255f);
        var b = ToLinear(color.Blue / 255f);

        var l = MathF.Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
        var m = MathF.Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
        var s = MathF.Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);

        return new Lab(
            0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
            1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
            0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
    }

    public static SKColor FromOkLab(Lab lab, byte alpha = 255)
    {
        var l = lab.L + 0.3963377774f * lab.A + 0.2158037573f * lab.B;
        var m = lab.L - 0.1055613458f * lab.A - 0.0638541728f * lab.B;
        var s = lab.L - 0.0894841775f * lab.A - 1.2914855480f * lab.B;
        l *= l * l;
        m *= m * m;
        s *= s * s;

        var r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        var g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        var b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

        return new SKColor(ToByte(ToGamma(r)), ToByte(ToGamma(g)), ToByte(ToGamma(b)), alpha);
    }

    /// <summary>OKLCH to sRGB. Lightness 0..1, chroma ~0..0.37, hue in degrees. Out-of-gamut colors are clipped.</summary>
    public static SKColor FromOkLch(float lightness, float chroma, float hueDegrees, byte alpha = 255)
    {
        var h = hueDegrees * MathF.PI / 180f;
        // Reduce chroma until the color fits into sRGB so hues keep their character instead of clipping.
        for (var i = 0; i < 12; i++)
        {
            var lab = new Lab(lightness, chroma * MathF.Cos(h), chroma * MathF.Sin(h));
            if (InGamut(lab))
                return FromOkLab(lab, alpha);
            chroma *= 0.85f;
        }
        return FromOkLab(new Lab(lightness, chroma * MathF.Cos(h), chroma * MathF.Sin(h)), alpha);
    }

    public static (float L, float C, float H) ToOkLch(SKColor color)
    {
        var lab = ToOkLab(color);
        var c = MathF.Sqrt(lab.A * lab.A + lab.B * lab.B);
        var h = MathF.Atan2(lab.B, lab.A) * 180f / MathF.PI;
        if (h < 0)
            h += 360f;
        return (lab.L, c, h);
    }

    private static bool InGamut(Lab lab)
    {
        var l = lab.L + 0.3963377774f * lab.A + 0.2158037573f * lab.B;
        var m = lab.L - 0.1055613458f * lab.A - 0.0638541728f * lab.B;
        var s = lab.L - 0.0894841775f * lab.A - 1.2914855480f * lab.B;
        l *= l * l;
        m *= m * m;
        s *= s * s;
        var r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        var g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        var b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        const float eps = 0.001f;
        return r >= -eps && r <= 1 + eps && g >= -eps && g <= 1 + eps && b >= -eps && b <= 1 + eps;
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    /// <summary>Perceptual blend in OKLab.</summary>
    public static SKColor Mix(SKColor a, SKColor b, float t)
    {
        var la = ToOkLab(a);
        var lb = ToOkLab(b);
        var alpha = (byte)Math.Clamp((int)MathF.Round(a.Alpha + (b.Alpha - a.Alpha) * t), 0, 255);
        return FromOkLab(new Lab(la.L + (lb.L - la.L) * t, la.A + (lb.A - la.A) * t, la.B + (lb.B - la.B) * t), alpha);
    }

    public static float Luminance(SKColor c) => ToOkLab(c).L;

    public static SKColor Lighten(SKColor c, float amount)
    {
        var (l, ch, h) = ToOkLch(c);
        return FromOkLch(Math.Clamp(l + amount, 0, 1), ch, h, c.Alpha);
    }

    public static SKColor Darken(SKColor c, float amount) => Lighten(c, -amount);

    public static SKColor WithChroma(SKColor c, float factor)
    {
        var (l, ch, h) = ToOkLch(c);
        return FromOkLch(l, ch * factor, h, c.Alpha);
    }

    public static SKColor Parse(string hex) => SKColor.Parse(hex);
}
