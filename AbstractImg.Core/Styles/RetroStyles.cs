using AbstractImg.Core.Palettes;
using AbstractImg.Core.Rendering;
using SkiaSharp;

namespace AbstractImg.Core.Styles;

/// <summary>80s synthwave: striped sun, mountains and a glowing perspective grid.</summary>
public sealed class SynthwaveStyle : ArtStyle
{
    public override string Id => "synthwave";
    public override string Name => "Synthwave";
    public override StyleCategory Category => StyleCategory.Retro;

    public override void Render(RenderContext ctx)
    {
        var classic = ctx.Chance(0.7);
        SKColor[] sky = classic
            ? [SKColor.Parse("#0B0033"), SKColor.Parse("#3B0A6B"), SKColor.Parse("#B5179E"), SKColor.Parse("#FF7A59")]
            : [ColorMath.FromOkLch(0.12f, 0.06f, ColorMath.ToOkLch(ctx.Palette[0]).H), ColorMath.FromOkLch(0.3f, 0.12f, ColorMath.ToOkLch(ctx.Palette[0]).H), ColorMath.FromOkLch(0.55f, 0.18f, ColorMath.ToOkLch(ctx.Palette[1]).H), ColorMath.FromOkLch(0.75f, 0.15f, ColorMath.ToOkLch(ctx.Palette[2]).H)];
        var sunTop = classic ? SKColor.Parse("#FFE66D") : ColorMath.Lighten(ctx.Palette[3], 0.15f);
        var sunBottom = classic ? SKColor.Parse("#FF2E88") : ctx.Palette[4];
        var gridColor = classic ? SKColor.Parse("#FF3CAC") : ColorMath.Lighten(ctx.Palette[2], 0.2f);
        var horizon = ctx.Height * ctx.R(0.58f, 0.7f);

        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, horizon), sky, null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = shader, IsDither = true })
        {
            ctx.Canvas.DrawRect(0, 0, ctx.Width, horizon + 1, paint);
        }

        using (var star = ctx.Fill(SKColors.White))
        {
            var stars = ctx.AreaCount(120, 30, 800);
            for (var i = 0; i < stars; i++)
            {
                var y = ctx.R(0, horizon * 0.6f);
                star.Color = SKColors.White.WithAlpha((byte)(255 * (1 - y / (horizon * 0.6f)) * ctx.R(0.3f, 1f)));
                ctx.Canvas.DrawCircle(ctx.R(0, ctx.Width), y, ctx.U * ctx.R(0.6f, 2.2f), star);
            }
        }

        // Sun with glow and horizontal cuts in its lower half.
        var sunR = ctx.Min * ctx.R(0.18f, 0.3f);
        var sunX = ctx.Width * ctx.R(0.35f, 0.65f);
        var sunY = horizon - sunR * ctx.R(0.2f, 0.6f);
        using (var glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sunR * 0.35f))
        using (var glow = new SKPaint { IsAntialias = true, Color = sunBottom.WithAlpha(120), MaskFilter = glowMask })
        {
            ctx.Canvas.DrawCircle(sunX, sunY, sunR * 1.1f, glow);
        }
        ctx.Canvas.SaveLayer(new SKRect(sunX - sunR - 2, sunY - sunR - 2, sunX + sunR + 2, sunY + sunR + 2), null);
        using (var sunShader = SKShader.CreateLinearGradient(new SKPoint(0, sunY - sunR), new SKPoint(0, sunY + sunR), [sunTop, sunBottom], null, SKShaderTileMode.Clamp))
        using (var sun = new SKPaint { IsAntialias = true, Shader = sunShader })
        {
            ctx.Canvas.DrawCircle(sunX, sunY, sunR, sun);
        }
        // Horizontal cuts through the lower half, growing towards the bottom.
        using (var cut = new SKPaint { BlendMode = SKBlendMode.Clear })
        {
            // Spread the cuts over the visible part of the sun (above the horizon).
            var top = sunY - sunR * 0.15f;
            var bottom = Math.Min(sunY + sunR, horizon - ctx.Height * 0.02f);
            var cuts = ctx.Ri(5, 9);
            for (var i = 0; i < cuts; i++)
            {
                var t = i / (float)cuts;
                var y = top + (bottom - top) * t;
                var h = Math.Max(1.5f, (bottom - top) / cuts * (0.15f + t * 0.45f));
                ctx.Canvas.DrawRect(sunX - sunR - 2, y, sunR * 2 + 4, h, cut);
            }
        }
        ctx.Canvas.Restore();

        // Mountains.
        for (var layer = 0; layer < 2; layer++)
        {
            var points = new List<SKPoint>();
            var peaks = ctx.Ri(5, 12);
            var maxH = ctx.Height * (layer == 0 ? 0.22f : 0.14f);
            points.Add(new SKPoint(-10, horizon));
            for (var i = 0; i <= peaks; i++)
            {
                var x = ctx.Width * i / (float)peaks + ctx.R(-0.03f, 0.03f) * ctx.Width;
                var h = maxH * MathF.Pow(ctx.R01(), 0.7f);
                // Keep the sun visible: valleys near it.
                if (MathF.Abs(x - sunX) < sunR * 0.9f)
                    h *= 0.35f;
                points.Add(new SKPoint(x, horizon - h));
            }
            points.Add(new SKPoint(ctx.Width + 10, horizon));
            using var path = new SKPath();
            path.AddPoly([.. points], true);
            using var fill = ctx.Fill(ColorMath.Darken(sky[0], layer == 0 ? 0.02f : -0.06f));
            ctx.Canvas.DrawPath(path, fill);
            using var edge = ctx.Stroke(gridColor.WithAlpha(layer == 0 ? (byte)140 : (byte)90), Math.Max(1, ctx.U * 1.5f));
            ctx.Canvas.DrawPath(path, edge);
        }

        // Ground and grid.
        using (var ground = SKShader.CreateLinearGradient(new SKPoint(0, horizon), new SKPoint(0, ctx.Height), [SKColor.Parse("#120024"), SKColor.Parse("#05000D")], null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = ground })
        {
            ctx.Canvas.DrawRect(0, horizon, ctx.Width, ctx.Height - horizon, paint);
        }

        using var gridPath = new SKPath();
        var vanishX = ctx.Width / 2f;
        var lines = ctx.Count(12, 30);
        for (var i = -lines; i <= lines; i++)
        {
            var bottomX = vanishX + i * ctx.Width * 2.2f / lines;
            gridPath.MoveTo(vanishX + i * ctx.Width * 0.01f, horizon);
            gridPath.LineTo(bottomX, ctx.Height);
        }
        var rows = ctx.Count(8, 20);
        for (var i = 1; i <= rows; i++)
        {
            var z = i / (float)rows;
            var y = horizon + (ctx.Height - horizon) * z * z;
            gridPath.MoveTo(0, y);
            gridPath.LineTo(ctx.Width, y);
        }
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(new SKRect(0, horizon, ctx.Width, ctx.Height));
        using (var glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, ctx.U * 6))
        using (var glow = ctx.Stroke(gridColor.WithAlpha(150), ctx.U * 6))
        {
            glow.MaskFilter = glowMask;
            ctx.Canvas.DrawPath(gridPath, glow);
        }
        using (var line = ctx.Stroke(ColorMath.Lighten(gridColor, 0.15f), Math.Max(1, ctx.U * 1.8f)))
        {
            ctx.Canvas.DrawPath(gridPath, line);
        }
        ctx.Canvas.Restore();
    }
}

/// <summary>Digital glitch: RGB split, displaced slices, pixel smears and scanlines.</summary>
public sealed class GlitchStyle : ArtStyle
{
    public override string Id => "glitch";
    public override string Name => "Glitch";
    public override StyleCategory Category => StyleCategory.Retro;

    public override void Render(RenderContext ctx)
    {
        using var baseSurface = SKSurface.Create(new SKImageInfo(ctx.Width, ctx.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        PaintBase(new RenderContext(baseSurface.Canvas, ctx.Width, ctx.Height, ctx.Rng, ctx.Palette, ctx.Detail));
        using var image = baseSurface.Snapshot();

        // RGB split: each channel drawn with its own offset and added together.
        ctx.Clear(SKColors.Black);
        var shift = ctx.U * ctx.R(4, 18);
        float[][] channels =
        [
            [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0],
            [0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0]
        ];
        float[] offsets = [-shift, 0, shift];
        for (var i = 0; i < 3; i++)
        {
            using var filter = SKColorFilter.CreateColorMatrix(channels[i]);
            using var paint = new SKPaint { ColorFilter = filter, BlendMode = SKBlendMode.Plus };
            ctx.Canvas.DrawImage(image, offsets[i], ctx.R(-shift, shift) * 0.3f, paint);
        }

        // Displaced horizontal slices.
        var slices = ctx.Count(6, 30);
        using var plain = new SKPaint();
        for (var i = 0; i < slices; i++)
        {
            var y = ctx.R(0, ctx.Height);
            var h = ctx.Height * ctx.R(0.005f, 0.06f);
            var dx = ctx.Width * ctx.R(-0.15f, 0.15f);
            var src = new SKRect(0, y, ctx.Width, y + h);
            ctx.Canvas.DrawImage(image, src, new SKRect(dx, y, ctx.Width + dx, y + h), plain);
        }

        // Pixel smears: a one pixel column stretched sideways.
        var smears = ctx.Count(2, 10);
        for (var i = 0; i < smears; i++)
        {
            var x = ctx.R(0, ctx.Width * 0.8f);
            var y = ctx.R(0, ctx.Height);
            var h = ctx.Height * ctx.R(0.02f, 0.15f);
            var len = ctx.Width * ctx.R(0.1f, 0.5f);
            ctx.Canvas.DrawImage(image, new SKRect(x, y, x + 1, y + h), new SKRect(x, y, x + len, y + h), plain);
        }

        // Inverted and colored blocks.
        var blocks = ctx.Count(3, 16);
        for (var i = 0; i < blocks; i++)
        {
            var w = ctx.Width * ctx.R(0.02f, 0.2f);
            var h = ctx.Height * ctx.R(0.005f, 0.05f);
            using var paint = ctx.Chance(0.5)
                ? new SKPaint { Color = SKColors.White, BlendMode = SKBlendMode.Difference }
                : ctx.Fill(ctx.Color().WithAlpha(200));
            ctx.Canvas.DrawRect(ctx.R(0, ctx.Width), ctx.R(0, ctx.Height), w, h, paint);
        }

        // Scanlines.
        var spacing = Math.Max(2f, ctx.U * ctx.R(2, 5));
        using var scan = new SKPaint { Color = SKColors.Black.WithAlpha((byte)ctx.Ri(25, 70)), StrokeWidth = spacing * 0.4f };
        for (var y = 0f; y < ctx.Height; y += spacing)
            ctx.Canvas.DrawLine(0, y, ctx.Width, y, scan);
    }

    private static void PaintBase(RenderContext ctx)
    {
        var angle = ctx.R(0, MathF.Tau);
        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(ctx.Width * MathF.Cos(angle), ctx.Height * MathF.Abs(MathF.Sin(angle))),
                   [ctx.Palette.Background, ctx.Color(), ctx.Color()], null, SKShaderTileMode.Mirror))
        using (var paint = new SKPaint { Shader = shader })
        {
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }

        var shapes = ctx.Count(3, 10);
        for (var i = 0; i < shapes; i++)
        {
            using var paint = ctx.Fill(ctx.Color());
            switch (ctx.Ri(0, 3))
            {
                case 0:
                    ctx.Canvas.DrawCircle(ctx.R(0, ctx.Width), ctx.R(0, ctx.Height), ctx.Min * ctx.R(0.08f, 0.35f), paint);
                    break;
                case 1:
                    ctx.Canvas.DrawRect(ctx.R(-0.1f, 0.8f) * ctx.Width, ctx.R(0, ctx.Height), ctx.Width * ctx.R(0.2f, 0.8f), ctx.Height * ctx.R(0.02f, 0.12f), paint);
                    break;
                default:
                {
                    using var path = new SKPath();
                    path.MoveTo(ctx.R(0, ctx.Width), ctx.R(0, ctx.Height));
                    path.LineTo(ctx.R(0, ctx.Width), ctx.R(0, ctx.Height));
                    path.LineTo(ctx.R(0, ctx.Width), ctx.R(0, ctx.Height));
                    path.Close();
                    ctx.Canvas.DrawPath(path, paint);
                    break;
                }
            }
        }
    }
}

/// <summary>Pixel art: posterized noise mosaics or rows of symmetric sprites.</summary>
public sealed class PixelMosaicStyle : ArtStyle
{
    public override string Id => "pixel-mosaic";
    public override string Name => "Pixel Mosaic";
    public override StyleCategory Category => StyleCategory.Retro;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        if (ctx.Chance(0.6))
            Mosaic(ctx);
        else
            Sprites(ctx);
    }

    private static void Mosaic(RenderContext ctx)
    {
        var cols = ctx.Count(12, 90);
        var cell = ctx.Width / (float)cols;
        var rows = (int)MathF.Ceiling(ctx.Height / cell);
        var gap = ctx.Chance(0.5) ? cell * ctx.R(0.04f, 0.15f) : 0;
        var radius = gap > 0 && ctx.Chance(0.5) ? cell * 0.2f : 0;
        var scale = ctx.R(0.04f, 0.15f);
        var levels = ctx.Palette.Colors.Count;
        var bevel = ctx.Chance(0.3);
        using var paint = new SKPaint { IsAntialias = radius > 0 };
        using var highlight = new SKPaint { Color = SKColors.White.WithAlpha(45) };
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var v = ctx.Noise.Fbm(c * scale, r * scale, 3) * 0.5f + 0.5f;
                var index = Math.Clamp((int)(v * levels * 1.2f - levels * 0.1f), 0, levels - 1);
                paint.Color = ctx.Palette[index];
                var rect = new SKRect(c * cell + gap / 2, r * cell + gap / 2, (c + 1) * cell - gap / 2, (r + 1) * cell - gap / 2);
                if (radius > 0)
                    ctx.Canvas.DrawRoundRect(rect, radius, radius, paint);
                else
                    ctx.Canvas.DrawRect(rect, paint);
                if (bevel)
                    ctx.Canvas.DrawRect(rect.Left, rect.Top, rect.Width, rect.Height * 0.2f, highlight);
            }
        }
    }

    private static void Sprites(RenderContext ctx)
    {
        var size = ctx.Pick(new[] { 5, 7, 9 });
        var cols = ctx.Count(4, 14);
        var spriteSpan = ctx.Width / (float)cols;
        var px = spriteSpan / (size + 2);
        var rows = (int)(ctx.Height / spriteSpan) + 1;
        var offsetY = (ctx.Height - rows * spriteSpan) / 2;
        using var paint = new SKPaint();
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                paint.Color = ctx.Color();
                var ox = c * spriteSpan + px;
                var oy = offsetY + r * spriteSpan + px;
                var half = (size + 1) / 2;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        if (!ctx.Chance(0.5))
                            continue;
                        ctx.Canvas.DrawRect(ox + x * px, oy + y * px, px + 0.5f, px + 0.5f, paint);
                        ctx.Canvas.DrawRect(ox + (size - 1 - x) * px, oy + y * px, px + 0.5f, px + 0.5f, paint);
                    }
                }
            }
        }
    }
}

/// <summary>Glowing neon tubes over a dark backdrop.</summary>
public sealed class NeonGlowStyle : ArtStyle
{
    public override string Id => "neon-glow";
    public override string Name => "Neon Glow";
    public override StyleCategory Category => StyleCategory.Retro;

    public override void Render(RenderContext ctx)
    {
        var bg = ColorMath.FromOkLch(ctx.R(0.1f, 0.16f), 0.03f, ColorMath.ToOkLch(ctx.Palette.Background).H);
        using (var shader = SKShader.CreateRadialGradient(ctx.Center, ctx.Max * 0.8f, [ColorMath.Lighten(bg, 0.05f), ColorMath.Darken(bg, 0.06f)], null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = shader, IsDither = true })
        {
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }

        var tubes = ctx.Count(3, 12);
        var width = ctx.U * ctx.R(3, 7);
        using var wideMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, width * 4);
        using var nearMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, width * 1.2f);
        for (var i = 0; i < tubes; i++)
        {
            using var path = ctx.Chance(0.3) ? Shape(ctx) : Curve(ctx);
            var color = ColorMath.WithChroma(ctx.Color(), 1.4f);
            using var glow = ctx.Stroke(color.WithAlpha(150), width * 3);
            glow.MaskFilter = wideMask;
            glow.BlendMode = SKBlendMode.Plus;
            ctx.Canvas.DrawPath(path, glow);
            glow.MaskFilter = nearMask;
            glow.StrokeWidth = width * 1.6f;
            glow.Color = color.WithAlpha(200);
            ctx.Canvas.DrawPath(path, glow);
            using var core = ctx.Stroke(ColorMath.Lighten(color, 0.25f), width * 0.55f);
            ctx.Canvas.DrawPath(path, core);
        }
    }

    private static SKPath Curve(RenderContext ctx)
    {
        var points = new List<SKPoint>();
        var n = ctx.Ri(4, 8);
        var horizontal = ctx.Chance(0.5);
        for (var k = 0; k < n; k++)
        {
            var t = k / (float)(n - 1);
            points.Add(horizontal
                ? new SKPoint(-0.1f * ctx.Width + t * 1.2f * ctx.Width, ctx.R(0.1f, 0.9f) * ctx.Height)
                : new SKPoint(ctx.R(0.1f, 0.9f) * ctx.Width, -0.1f * ctx.Height + t * 1.2f * ctx.Height));
        }
        return SmoothOpenPath(points);
    }

    private static SKPath Shape(RenderContext ctx)
    {
        var path = new SKPath();
        var c = new SKPoint(ctx.R(0.2f, 0.8f) * ctx.Width, ctx.R(0.2f, 0.8f) * ctx.Height);
        var r = ctx.Min * ctx.R(0.08f, 0.3f);
        var sides = ctx.Pick(new[] { 0, 3, 4, 6 });
        if (sides == 0)
        {
            path.AddCircle(c.X, c.Y, r);
            return path;
        }
        var rotation = ctx.R(0, MathF.Tau);
        for (var k = 0; k < sides; k++)
        {
            var a = rotation + k * MathF.Tau / sides;
            var p = new SKPoint(c.X + MathF.Cos(a) * r, c.Y + MathF.Sin(a) * r);
            if (k == 0)
                path.MoveTo(p);
            else
                path.LineTo(p);
        }
        path.Close();
        return path;
    }
}
