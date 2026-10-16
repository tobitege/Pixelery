using SkiaSharp;

namespace Pixelery.Core.Rendering;

/// <summary>
/// Round, anti-aliased stars drawn at full resolution (shader-made stars turn blocky when soft styles are upscaled).
/// Most stars are tiny and faint, a few are larger with a soft glow.
/// </summary>
public static class Starfield
{
    private static readonly SKColor[] Tints = [SKColor.Parse("#FFFFFF"), SKColor.Parse("#DCE8FF"), SKColor.Parse("#FFF0DA")];

    /// <param name="ctx">Target.</param>
    /// <param name="perMegaUnit">Stars per 1000 × 1000 units (<see cref="RenderContext.U"/>), independent of the pixel size.</param>
    /// <param name="visibility">Optional fade by vertical position (0 top .. 1 bottom), returning 0..1.</param>
    public static void Draw(RenderContext ctx, float perMegaUnit, Func<float, float>? visibility = null)
    {
        var units = ctx.Width / ctx.U * (ctx.Height / ctx.U) / 1_000_000f;
        var count = (int)(perMegaUnit * units);
        using var core = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.Screen };
        using var glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(1f, ctx.U * 2.5f));
        using var glow = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.Screen, MaskFilter = glowMask };

        // Real skies clump: density follows a noise field, plus a few loose clusters.
        var noise = new SimplexNoise(ctx.Rng.Next());
        var scale = ctx.R(1.5f, 3.5f) / ctx.Min;
        var clusters = Enumerable.Range(0, ctx.Ri(2, 6))
            .Select(_ => (X: ctx.R(0, ctx.Width), Y: ctx.R(0, ctx.Height), Spread: ctx.Min * ctx.R(0.03f, 0.12f)))
            .ToList();

        for (var i = 0; i < count; i++)
        {
            float x, y;
            if (ctx.Chance(0.18))
            {
                var c = clusters[ctx.Ri(0, clusters.Count)];
                x = c.X + ctx.Gauss(0, c.Spread);
                y = c.Y + ctx.Gauss(0, c.Spread);
            }
            else
            {
                // Rejection sampling: keep a star with a probability given by the noise density.
                var attempts = 0;
                do
                {
                    x = ctx.R(0, ctx.Width);
                    y = ctx.R(0, ctx.Height);
                    var density = noise.Fbm(x * scale, y * scale, 3) * 0.5f + 0.5f;
                    if (ctx.R01() < 0.08f + density * density * 1.4f)
                        break;
                }
                while (++attempts < 12);
            }
            if (x < 0 || y < 0 || x > ctx.Width || y > ctx.Height)
                continue;
            var fade = visibility?.Invoke(y / ctx.Height) ?? 1f;
            if (fade <= 0.01f)
                continue;
            var t = ctx.R01();
            var radius = Math.Max(0.45f, ctx.U * (0.45f + 1.3f * t * t * t));
            var alpha = (0.3f + 0.7f * MathF.Pow(ctx.R01(), 1.5f)) * fade;
            var tint = Tints[ctx.Ri(0, Tints.Length)];

            // A few bright stars get a halo.
            if (t > 0.94f)
            {
                glow.Color = tint.WithAlpha((byte)(70 * fade));
                ctx.Canvas.DrawCircle(x, y, radius * 3.5f, glow);
                alpha = Math.Min(1f, alpha + 0.3f);
            }
            core.Color = tint.WithAlpha((byte)(255 * alpha));
            ctx.Canvas.DrawCircle(x, y, radius, core);
        }
    }
}
