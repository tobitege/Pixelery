using AbstractImg.Core.Palettes;
using AbstractImg.Core.Rendering;
using SkiaSharp;

namespace AbstractImg.Core.Styles;

/// <summary>Soft "mesh gradient" wallpaper: large blurred color blobs.</summary>
public sealed class MeshGradientStyle : ArtStyle
{
    public override string Id => "mesh-gradient";
    public override string Name => "Mesh Gradient";
    public override StyleCategory Category => StyleCategory.Gradients;

    public override void Render(RenderContext ctx)
    {
        RenderLowRes(ctx, 320, small =>
        {
            small.Clear(small.Chance(0.5) ? small.Palette.Background : small.Color());
            var blobs = small.Count(4, 9);
            using var blur = SKImageFilter.CreateBlur(small.Max * 0.09f, small.Max * 0.09f);
            using var layer = new SKPaint { ImageFilter = blur };
            small.Canvas.SaveLayer(layer);
            for (var i = 0; i < blobs; i++)
            {
                using var paint = small.Fill(small.Palette[i].WithAlpha((byte)small.Ri(170, 256)));
                var cx = small.R(-0.1f, 1.1f) * small.Width;
                var cy = small.R(-0.1f, 1.1f) * small.Height;
                var rx = small.R(0.2f, 0.55f) * small.Max;
                var ry = rx * small.R(0.5f, 1.3f);
                small.Canvas.Save();
                small.Canvas.RotateDegrees(small.R(0, 180), cx, cy);
                small.Canvas.DrawOval(cx, cy, rx, ry, paint);
                small.Canvas.Restore();
            }
            small.Canvas.Restore();
        });
    }
}

/// <summary>Northern lights: glowing curtains above a dark sky.</summary>
public sealed class AuroraStyle : ArtStyle
{
    private const string Sksl = """
        uniform float amp;
        uniform float freq;
        uniform float streaks;
        uniform float4 sky;

        half4 main(float2 c) {
            float2 p = uv(c);
            float aspect = res.x / min(res.x, res.y);
            float3 col = sky.rgb * (0.55 + 0.6 * (0.5 - p.y));
            for (int i = 0; i < 4; i++) {
                float fi = float(i);
                float base = -0.05 + fi * 0.09 - 0.12;
                float curve = base + (fbm4(float2(p.x * freq + fi * 3.1, fi * 7.3)) - 0.5) * amp;
                float d = curve - p.y;
                float glow = d > 0.0 ? exp(-d * (7.0 + fi * 2.0)) : exp(d * 30.0);
                float streak = 0.55 + 0.45 * vnoise(float2(p.x * streaks + fi * 13.0, fi));
                float edge = smoothstep(-0.01, 0.02, d);
                float4 tint = pal(fract(fi * 0.27 + p.x / aspect * 0.35 + 0.5));
                col += tint.rgb * glow * streak * (0.35 + 0.65 * edge) * 0.55;
            }
            return half4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    public override string Id => "aurora";
    public override string Name => "Aurora";
    public override StyleCategory Category => StyleCategory.Gradients;

    public override void Render(RenderContext ctx)
    {
        // Soft field: rendering above ~1600 px only costs time.
        RenderLowRes(ctx, 1600, small =>
        {
            var builder = Shaders.Create(Id, Sksl, small);
            builder.Uniforms["amp"] = small.R(0.25f, 0.6f);
            builder.Uniforms["freq"] = small.R(0.8f, 2.4f);
            builder.Uniforms["streaks"] = small.R(40f, 140f);
            var sky = ColorMath.FromOkLch(small.R(0.1f, 0.18f), 0.04f, ColorMath.ToOkLch(small.Palette.Background).H);
            builder.Uniforms["sky"] = new[] { sky.Red / 255f, sky.Green / 255f, sky.Blue / 255f, 1f };
            Shaders.Fill(small, builder);
        });
        // Stars only in the upper sky, fading out towards the curtains.
        Starfield.Draw(ctx, ctx.R(60, 140), y => Math.Clamp(1f - y / 0.55f, 0f, 1f));
    }
}

/// <summary>Linear, radial, conic and two-point gradients, optionally layered or posterized into bands.</summary>
public sealed class GradientFieldStyle : ArtStyle
{
    public override string Id => "gradient-field";
    public override string Name => "Gradient Field";
    public override StyleCategory Category => StyleCategory.Gradients;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var layers = ctx.Chance(0.45) ? 2 : 1;
        var banded = ctx.Chance(0.3);
        for (var i = 0; i < layers; i++)
        {
            using var shader = CreateGradient(ctx, banded);
            using var paint = new SKPaint { Shader = shader, IsDither = true };
            if (i > 0)
            {
                paint.BlendMode = ctx.Pick(new[] { SKBlendMode.Screen, SKBlendMode.Multiply, SKBlendMode.Overlay, SKBlendMode.SoftLight, SKBlendMode.Hue });
                paint.Color = SKColors.White.WithAlpha((byte)ctx.Ri(140, 256));
            }
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }
    }

    private static SKShader CreateGradient(RenderContext ctx, bool banded)
    {
        var count = ctx.Ri(3, Math.Min(6, ctx.Palette.Colors.Count + 1) + 1);
        var colors = Enumerable.Range(0, count).Select(i => i == 0 && ctx.Chance(0.5) ? ctx.Palette.Background : ctx.Palette[ctx.Ri(0, 99)]).ToArray();
        // Low contrast gradients look flat: stretch the lightness range.
        var lums = colors.Select(ColorMath.Luminance).ToArray();
        if (lums.Max() - lums.Min() < 0.25f)
        {
            colors[0] = ColorMath.Darken(colors[0], 0.25f);
            colors[^1] = ColorMath.Lighten(colors[^1], 0.15f);
        }
        float[]? positions = null;
        if (banded)
        {
            // Duplicate stops so each color becomes a hard-edged band.
            var bands = ctx.Ri(4, 12);
            var bandColors = new SKColor[bands * 2];
            positions = new float[bands * 2];
            for (var b = 0; b < bands; b++)
            {
                var c = ColorMath.Mix(colors[b * (colors.Length - 1) / Math.Max(1, bands - 1) % colors.Length], colors[(b + 1) % colors.Length], b / (float)bands);
                bandColors[b * 2] = c;
                bandColors[b * 2 + 1] = c;
                positions[b * 2] = b / (float)bands;
                positions[b * 2 + 1] = (b + 1) / (float)bands;
            }
            colors = bandColors;
        }

        var w = ctx.Width;
        var h = ctx.Height;
        switch (ctx.Ri(0, 4))
        {
            case 0:
            {
                var angle = ctx.R(0, MathF.Tau);
                var r = ctx.Max * 0.6f;
                var c = ctx.Center;
                return SKShader.CreateLinearGradient(
                    new SKPoint(c.X - MathF.Cos(angle) * r, c.Y - MathF.Sin(angle) * r),
                    new SKPoint(c.X + MathF.Cos(angle) * r, c.Y + MathF.Sin(angle) * r),
                    colors, positions, SKShaderTileMode.Clamp);
            }
            case 1:
                return SKShader.CreateRadialGradient(
                    new SKPoint(ctx.R(0, w), ctx.R(0, h)), ctx.Max * ctx.R(0.5f, 1.2f), colors, positions,
                    ctx.Chance(0.3) ? SKShaderTileMode.Mirror : SKShaderTileMode.Clamp);
            case 2:
            {
                var center = new SKPoint(ctx.R(0.2f, 0.8f) * w, ctx.R(0.2f, 0.8f) * h);
                var start = ctx.R(0, 360);
                // Close the loop so the seam of the conic gradient disappears.
                var loop = banded ? colors : [.. colors, colors[0]];
                return SKShader.CreateSweepGradient(center, loop, banded ? positions : null, SKShaderTileMode.Clamp, start, start + 360);
            }
            default:
                return SKShader.CreateTwoPointConicalGradient(
                    new SKPoint(ctx.R(0, w), ctx.R(0, h)), ctx.Min * ctx.R(0.02f, 0.2f),
                    new SKPoint(ctx.R(0, w), ctx.R(0, h)), ctx.Max * ctx.R(0.6f, 1.1f),
                    colors, positions, SKShaderTileMode.Clamp);
        }
    }
}

/// <summary>Out-of-focus light circles on a soft gradient.</summary>
public sealed class BokehStyle : ArtStyle
{
    public override string Id => "bokeh";
    public override string Name => "Bokeh";
    public override StyleCategory Category => StyleCategory.Gradients;

    public override void Render(RenderContext ctx)
    {
        var bg = ctx.Palette.IsDark ? ctx.Palette.Background : ColorMath.Darken(ctx.Color(), 0.35f);
        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(ctx.R(0, ctx.Width), ctx.Height),
                   [ColorMath.Darken(bg, 0.05f), ColorMath.Mix(bg, ctx.Color(), 0.35f), ColorMath.Darken(bg, 0.1f)], null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = shader, IsDither = true })
        {
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }

        var hexagonal = ctx.Chance(0.35);
        var rotation = ctx.R(0, 60);
        // Far, mid and near layers: small and very soft first, large with bright rims last.
        for (var layer = 0; layer < 3; layer++)
        {
            var count = ctx.Count(10, 40) / (layer + 1) + 4;
            for (var i = 0; i < count; i++)
            {
                var r = ctx.U * ctx.R(15, 50) * (1 + layer * 1.6f);
                var x = ctx.R(-0.05f, 1.05f) * ctx.Width;
                var y = ctx.R(-0.05f, 1.05f) * ctx.Height;
                var color = ctx.Color();
                var sigma = r * (layer == 2 ? 0.08f : 0.35f - layer * 0.12f);
                using var mask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, sigma));
                using var fill = new SKPaint
                {
                    IsAntialias = true,
                    Color = color.WithAlpha((byte)ctx.Ri(40, 110)),
                    BlendMode = SKBlendMode.Screen,
                    MaskFilter = mask
                };
                using var shape = Shape(x, y, r, hexagonal, rotation);
                ctx.Canvas.DrawPath(shape, fill);
                if (layer == 2)
                {
                    using var rim = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = r * 0.06f,
                        Color = ColorMath.Lighten(color, 0.2f).WithAlpha(70),
                        BlendMode = SKBlendMode.Screen,
                        MaskFilter = mask
                    };
                    ctx.Canvas.DrawPath(shape, rim);
                }
            }
        }
    }

    private static SKPath Shape(float x, float y, float r, bool hexagonal, float rotation)
    {
        var path = new SKPath();
        if (!hexagonal)
        {
            path.AddCircle(x, y, r);
            return path;
        }
        for (var k = 0; k < 6; k++)
        {
            var a = (rotation + k * 60) * MathF.PI / 180f;
            var p = new SKPoint(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r);
            if (k == 0)
                path.MoveTo(p);
            else
                path.LineTo(p);
        }
        path.Close();
        return path;
    }
}

/// <summary>Colorful gas clouds with a sprinkle of stars.</summary>
public sealed class NebulaStyle : ArtStyle
{
    private const string Sksl = """
        uniform float scale;
        uniform float warp;
        uniform float density;
        uniform float4 space;

        half4 main(float2 c) {
            float2 p = uv(c) * scale;
            float2 q = float2(fbm4(p + float2(0.0, 0.0)), fbm4(p + float2(5.2, 1.3)));
            float2 r = float2(fbm4(p + warp * q + float2(1.7, 9.2)), fbm4(p + warp * q + float2(8.3, 2.8)));
            float f = fbm6(p + warp * r);
            float cloud = smoothstep(density, 1.0, f);
            float3 gas = pal(f * 1.1 + length(q) * 0.35 - 0.2).rgb;
            float3 col = mix(space.rgb, gas, cloud);
            col += gas * pow(cloud, 3.0) * 0.6;
            col = mix(col, col * 0.4, smoothstep(0.55, 0.85, r.x) * 0.5);
            return half4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    public override string Id => "nebula";
    public override string Name => "Nebula";
    public override StyleCategory Category => StyleCategory.Gradients;

    public override void Render(RenderContext ctx)
    {
        RenderLowRes(ctx, 1600, small =>
        {
            var builder = Shaders.Create(Id, Sksl, small);
            builder.Uniforms["scale"] = small.R(1.5f, 3.5f);
            builder.Uniforms["warp"] = small.R(2f, 5f);
            builder.Uniforms["density"] = small.R(0.18f, 0.38f);
            var space = ColorMath.FromOkLch(small.R(0.06f, 0.14f), small.R(0.01f, 0.05f), ColorMath.ToOkLch(small.Color()).H);
            builder.Uniforms["space"] = new[] { space.Red / 255f, space.Green / 255f, space.Blue / 255f, 1f };
            Shaders.Fill(small, builder);
        });
        Starfield.Draw(ctx, ctx.R(90, 220) * (0.6f + ctx.Detail * 0.6f));
    }
}
