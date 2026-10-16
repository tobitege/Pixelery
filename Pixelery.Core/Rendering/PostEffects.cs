using SkiaSharp;

namespace Pixelery.Core.Rendering;

/// <summary>
/// Finishing passes applied after a style rendered: film grain and vignette.
/// </summary>
public static class PostEffects
{
    private static readonly Lazy<SKImage> GrainTile = new(CreateGrainTile);

    private static SKImage CreateGrainTile()
    {
        const int size = 256;
        var rng = new Random(1234567);
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            // Roughly gaussian luminance noise around mid gray.
            var v = (rng.Next(256) + rng.Next(256) + rng.Next(256)) / 3;
            pixels[i * 4] = (byte)v;
            pixels[i * 4 + 1] = (byte)v;
            pixels[i * 4 + 2] = (byte)v;
            pixels[i * 4 + 3] = 255;
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>Overlays monochrome film grain. Intensity 0..1.</summary>
    public static void Grain(RenderContext ctx, float intensity)
    {
        var scale = Math.Max(1f, ctx.Min / 1400f);
        var matrix = SKMatrix.CreateScale(scale, scale).PostConcat(SKMatrix.CreateTranslation(ctx.R(0, 256), ctx.R(0, 256)));
        using var shader = GrainTile.Value.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Nearest), matrix);
        using var paint = new SKPaint
        {
            Shader = shader,
            BlendMode = SKBlendMode.Overlay,
            Color = SKColors.White.WithAlpha((byte)Math.Clamp(intensity * 255, 0, 255))
        };
        ctx.Canvas.DrawRect(ctx.Bounds, paint);
    }

    /// <summary>Darkens the edges. Strength 0..1.</summary>
    public static void Vignette(RenderContext ctx, float strength)
    {
        var radius = MathF.Sqrt(ctx.Width * ctx.Width + ctx.Height * ctx.Height) / 2f;
        using var shader = SKShader.CreateRadialGradient(
            ctx.Center,
            radius,
            [SKColors.Transparent, SKColors.Transparent, SKColors.Black.WithAlpha((byte)(strength * 255))],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsDither = true };
        ctx.Canvas.DrawRect(ctx.Bounds, paint);
    }
}
