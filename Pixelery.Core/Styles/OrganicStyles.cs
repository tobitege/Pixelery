using Pixelery.Core.Palettes;
using Pixelery.Core.Rendering;
using SkiaSharp;

namespace Pixelery.Core.Styles;

/// <summary>Thousands of particle trails following a noise vector field.</summary>
public sealed class FlowFieldStyle : ArtStyle
{
    public override string Id => "flow-field";
    public override string Name => "Flow Field";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var particles = ctx.Count(1500, 5000);
        var steps = ctx.Count(80, 260);
        var stepLength = ctx.U * ctx.R(2f, 3.5f);
        var scale = ctx.R(0.6f, 3f) / ctx.Min;
        var turns = ctx.R(1f, 4f);
        var snap = ctx.Chance(0.2) ? ctx.Pick(new[] { 4, 6, 8 }) : 0;
        var widthBase = Math.Max(0.7f, ctx.U * ctx.R(0.8f, 3.2f));
        var alpha = (byte)ctx.Ri(70, 200);
        var colorByPosition = ctx.Chance(0.6);
        var offset = ctx.R(0, 100);
        using var paint = ctx.Stroke(SKColors.White, widthBase);

        var points = new SKPoint[steps];
        for (var p = 0; p < particles; p++)
        {
            var x = ctx.R(-0.05f, 1.05f) * ctx.Width;
            var y = ctx.R(-0.05f, 1.05f) * ctx.Height;
            var count = 0;
            for (var s = 0; s < steps; s++)
            {
                points[count++] = new SKPoint(x, y);
                var angle = ctx.Noise.Fbm(x * scale + offset, y * scale, 2) * MathF.PI * turns;
                if (snap > 0)
                    angle = MathF.Round(angle / (MathF.Tau / snap)) * (MathF.Tau / snap);
                x += MathF.Cos(angle) * stepLength;
                y += MathF.Sin(angle) * stepLength;
                if (x < -ctx.Width * 0.1f || x > ctx.Width * 1.1f || y < -ctx.Height * 0.1f || y > ctx.Height * 1.1f)
                    break;
            }
            if (count < 2)
                continue;
            var color = colorByPosition
                ? ctx.Palette.Sample(Math.Clamp(points[0].Y / ctx.Height + ctx.R(-0.08f, 0.08f), 0, 1))
                : ctx.Color();
            paint.Color = color.WithAlpha(alpha);
            paint.StrokeWidth = widthBase * ctx.R(0.4f, 1.6f);
            using var path = new SKPath();
            path.AddPoly(points.AsSpan(0, count).ToArray(), false);
            ctx.Canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Domain-warped fractal noise (after Inigo Quilez) that looks like marble or liquid paint.</summary>
public sealed class MarbleStyle : ArtStyle
{
    private const string Sksl = """
        uniform float scale;
        uniform float warp;
        uniform float veins;
        uniform float shift;

        half4 main(float2 c) {
            float2 p = uv(c) * scale;
            float2 q = float2(fbm4(p), fbm4(p + float2(5.2, 1.3)));
            float2 r = float2(fbm4(p + warp * q + float2(1.7, 9.2)), fbm4(p + warp * q + float2(8.3, 2.8)));
            float f = fbm4(p + warp * r);
            float t = f * 1.3 + length(q) * 0.4 + r.y * 0.3 + shift;
            float3 col = palc(t).rgb;
            float v = abs(sin((f + r.x) * veins));
            col = mix(col, col * 0.35, (1.0 - smoothstep(0.0, 0.12, v)) * step(1.0, veins));
            col *= 0.85 + 0.3 * f;
            return half4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    public override string Id => "marble";
    public override string Name => "Marble";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        RenderLowRes(ctx, 1800, small =>
        {
            var builder = Shaders.Create(Id, Sksl, small, cyclicRamp: true, rampWithBackground: true);
            builder.Uniforms["scale"] = small.R(1.2f, 3.5f);
            builder.Uniforms["warp"] = small.R(2f, 4.5f);
            builder.Uniforms["veins"] = small.Chance(0.4) ? small.R(8f, 30f) : 0f;
            builder.Uniforms["shift"] = small.R(0, 1);
            Shaders.Fill(small, builder);
        });
    }
}

/// <summary>Contour lines of a noise height map, with optional hypsometric tint.</summary>
public sealed class TopographicStyle : ArtStyle
{
    private const string Sksl = """
        uniform float scale;
        uniform float levels;
        uniform float lineWidth;
        uniform float tinted;
        uniform float4 bg;
        uniform float4 ink;

        float height(float2 p) { return fbm4(p * scale); }

        half4 main(float2 c) {
            float px = 1.0 / min(res.x, res.y);
            float2 p = uv(c);
            float h = height(p);
            float hx = height(p + float2(px, 0.0));
            float hy = height(p + float2(0.0, px));
            float grad = length(float2(hx - h, hy - h)) * levels + 1e-5;
            float v = h * levels;
            float d = abs(fract(v + 0.5) - 0.5) / grad;
            float major = fract(floor(v + 0.5) / 5.0 + 0.1) < 0.2 ? 1.0 : 0.0;
            float w = lineWidth * (1.0 + major * 1.2);
            float line = 1.0 - smoothstep(w * 0.5, w * 0.5 + 1.0, d);
            float3 base = tinted > 0.5 ? pal(floor(v) / levels * 1.4 - 0.1).rgb : bg.rgb;
            float3 col = mix(base, ink.rgb, line * (0.75 + major * 0.25));
            return half4(col, 1.0);
        }
        """;

    public override string Id => "topographic";
    public override string Name => "Topographic";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        var builder = Shaders.Create(Id, Sksl, ctx);
        builder.Uniforms["scale"] = ctx.R(1.2f, 3.5f);
        builder.Uniforms["levels"] = (float)ctx.Count(12, 40);
        builder.Uniforms["lineWidth"] = Math.Max(1f, ctx.U * ctx.R(1f, 2.5f));
        var tinted = ctx.Chance(0.5);
        builder.Uniforms["tinted"] = tinted ? 1f : 0f;
        var bg = ctx.Palette.Background;
        var ink = tinted ? (ctx.Palette.IsDark ? SKColors.White.WithAlpha(255) : ctx.Palette.Ink) : ctx.Color();
        if (!tinted && MathF.Abs(ColorMath.Luminance(ink) - ColorMath.Luminance(bg)) < 0.2f)
            ink = ctx.Palette.Ink;
        builder.Uniforms["bg"] = new[] { bg.Red / 255f, bg.Green / 255f, bg.Blue / 255f, 1f };
        builder.Uniforms["ink"] = new[] { ink.Red / 255f, ink.Green / 255f, ink.Blue / 255f, 1f };
        Shaders.Fill(ctx, builder);
    }
}

/// <summary>Watercolor washes from many translucent, recursively deformed polygons (after Tyler Hobbs).</summary>
public sealed class WatercolorStyle : ArtStyle
{
    public override string Id => "watercolor";
    public override string Name => "Watercolor";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        var paper = ctx.Palette.IsDark && ctx.Chance(0.7) ? ColorMath.FromOkLch(0.95f, 0.015f, 85) : ctx.Palette.Background;
        ctx.Clear(paper);
        var lightPaper = ColorMath.Luminance(paper) > 0.6f;
        var blobs = ctx.Count(3, 8);
        var layers = 45;
        for (var b = 0; b < blobs; b++)
        {
            var center = new SKPoint(ctx.R(0.05f, 0.95f) * ctx.Width, ctx.R(0.05f, 0.95f) * ctx.Height);
            var radius = ctx.Min * ctx.R(0.12f, 0.35f);
            var basePoly = new List<SKPoint>();
            var sides = 10;
            for (var i = 0; i < sides; i++)
            {
                var a = i * MathF.Tau / sides;
                basePoly.Add(new SKPoint(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius));
            }
            var variances = basePoly.Select(_ => ctx.R(0.3f, 1.4f)).ToList();
            var shape = Deform(ctx, basePoly, 3, radius * 0.25f, variances);
            var color = ctx.Color();
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = color.WithAlpha(lightPaper ? (byte)6 : (byte)8),
                BlendMode = lightPaper ? SKBlendMode.Multiply : SKBlendMode.Screen,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, ctx.U * 1.5f))
            };
            for (var l = 0; l < layers; l++)
            {
                var layer = Deform(ctx, shape, 4, radius * 0.22f, null);
                using var path = new SKPath();
                path.AddPoly([.. layer], true);
                ctx.Canvas.DrawPath(path, paint);
            }
        }
    }

    private static List<SKPoint> Deform(RenderContext ctx, List<SKPoint> poly, int depth, float amount, List<float>? variances)
    {
        var current = poly;
        var currentVariances = variances ?? [.. Enumerable.Repeat(1f, poly.Count)];
        for (var d = 0; d < depth; d++)
        {
            var next = new List<SKPoint>(current.Count * 2);
            var nextVariances = new List<float>(current.Count * 2);
            for (var i = 0; i < current.Count; i++)
            {
                var a = current[i];
                var b = current[(i + 1) % current.Count];
                var v = currentVariances[i];
                var mid = new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                var len = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                var shift = ctx.Gauss(0, amount * v * 0.5f) * Math.Min(1f, len / amount);
                var angle = ctx.Gauss(0, 0.6f) + MathF.Atan2(b.Y - a.Y, b.X - a.X) + MathF.PI / 2;
                mid = new SKPoint(mid.X + MathF.Cos(angle) * shift, mid.Y + MathF.Sin(angle) * shift);
                next.Add(a);
                next.Add(mid);
                nextVariances.Add(v);
                nextVariances.Add(v * ctx.R(0.7f, 1.2f));
            }
            current = next;
            currentVariances = nextVariances;
            amount *= 0.55f;
        }
        return current;
    }
}

/// <summary>Layered paper cut-outs with soft shadows: wave horizons or nested organic rings.</summary>
public sealed class PaperLayersStyle : ArtStyle
{
    public override string Id => "paper-layers";
    public override string Name => "Paper Cut";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        var layers = ctx.Count(4, 10);
        var concentric = ctx.Chance(0.4);
        var reverse = ctx.Chance(0.5);
        ctx.Clear(ctx.Palette.Sample(reverse ? 1 : 0));
        var shadow = ctx.U * ctx.R(6, 16);
        using var filter = SKImageFilter.CreateDropShadow(0, concentric ? shadow * 0.3f : -shadow * 0.25f, shadow, shadow, SKColors.Black.WithAlpha(110));
        var freq = ctx.R(1.5f, 4f);

        for (var l = 0; l < layers; l++)
        {
            var t = (l + 1) / (float)layers;
            var color = ctx.Palette.Sample(reverse ? 1 - t : t);
            using var paint = new SKPaint { IsAntialias = true, Color = color, ImageFilter = filter };
            using var path = concentric ? Ring(ctx, l, layers, freq) : Wave(ctx, l, layers, freq);
            ctx.Canvas.DrawPath(path, paint);
        }
    }

    private static SKPath Wave(RenderContext ctx, int layer, int layers, float freq)
    {
        var points = new List<SKPoint>();
        var baseY = ctx.Height * (0.15f + 0.8f * layer / layers);
        var amp = ctx.Height * ctx.R(0.03f, 0.1f);
        var segments = 24;
        var seed = ctx.R(0, 100);
        for (var i = 0; i <= segments; i++)
        {
            var x = ctx.Width * i / (float)segments;
            var y = baseY + ctx.Noise.Fbm(i / (float)segments * freq + seed, layer * 3.7f, 2) * amp * 2;
            points.Add(new SKPoint(x, y));
        }
        var path = SmoothOpenPath(points);
        path.LineTo(ctx.Width + 10, ctx.Height + 10);
        path.LineTo(-10, ctx.Height + 10);
        path.Close();
        return path;
    }

    private static SKPath Ring(RenderContext ctx, int layer, int layers, float freq)
    {
        var radius = ctx.Max * 0.8f * (1 - layer / (float)layers) + ctx.Min * 0.04f;
        var center = new SKPoint(ctx.Width / 2f + ctx.R(-0.02f, 0.02f) * ctx.Width, ctx.Height / 2f + ctx.R(-0.02f, 0.02f) * ctx.Height);
        var points = new List<SKPoint>();
        var segments = 18;
        var seed = ctx.R(0, 100);
        for (var i = 0; i < segments; i++)
        {
            var a = i * MathF.Tau / segments;
            var r = radius * (1 + ctx.Noise.Noise(MathF.Cos(a) * freq * 0.5f + seed, MathF.Sin(a) * freq * 0.5f + layer) * 0.12f);
            points.Add(new SKPoint(center.X + MathF.Cos(a) * r, center.Y + MathF.Sin(a) * r));
        }
        return SmoothClosedPath(points);
    }
}

/// <summary>Stacked ridgelines with occlusion, like the famous pulsar plot.</summary>
public sealed class RidgelinesStyle : ArtStyle
{
    public override string Id => "ridgelines";
    public override string Name => "Ridgelines";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var lines = ctx.Count(25, 90);
        var marginX = ctx.Width * ctx.R(0.08f, 0.2f);
        var marginTop = ctx.Height * ctx.R(0.12f, 0.25f);
        var marginBottom = ctx.Height * ctx.R(0.06f, 0.15f);
        var spacing = (ctx.Height - marginTop - marginBottom) / lines;
        var amp = spacing * ctx.R(4f, 9f);
        var colored = ctx.Chance(0.45);
        var samples = Math.Max(80, (int)(ctx.Width / (ctx.U * 4)));
        var freq = ctx.R(4f, 10f);
        var width = Math.Max(1f, ctx.U * ctx.R(1.2f, 2.5f));
        using var fill = ctx.Fill(ctx.Palette.Background);
        using var stroke = ctx.Stroke(ctx.Palette.Ink, width);

        for (var l = 0; l < lines; l++)
        {
            var baseY = marginTop + l * spacing;
            var points = new SKPoint[samples + 1];
            for (var i = 0; i <= samples; i++)
            {
                var u = i / (float)samples;
                var x = marginX + u * (ctx.Width - 2 * marginX);
                var envelope = MathF.Exp(-MathF.Pow((u - 0.5f) * 4.2f, 2));
                var n = MathF.Max(0, ctx.Noise.Fbm(u * freq, l * 0.35f, 3) + 0.15f);
                var y = baseY - (n * n * 3 * envelope + ctx.Noise.Noise(u * 40, l) * 0.02f) * amp;
                points[i] = new SKPoint(x, y);
            }
            using var path = new SKPath();
            path.AddPoly(points, false);
            using var area = new SKPath(path);
            area.LineTo(points[^1].X, baseY + spacing * 2);
            area.LineTo(points[0].X, baseY + spacing * 2);
            area.Close();
            ctx.Canvas.DrawPath(area, fill);
            if (colored)
                stroke.Color = ctx.Palette.Sample(l / (float)lines);
            ctx.Canvas.DrawPath(path, stroke);
        }
    }
}

/// <summary>Voronoi cells with lead lines, like stained glass.</summary>
public sealed class StainedGlassStyle : ArtStyle
{
    private const string Sksl = """
        uniform float cells;
        uniform float lead;
        uniform float4 leadColor;

        half4 main(float2 c) {
            float2 p = uv(c) * cells;
            float2 ip = floor(p);
            float2 fp = fract(p);
            float d1 = 8.0;
            float d2 = 8.0;
            float2 id = float2(0.0);
            for (int j = -1; j <= 1; j++) {
                for (int i = -1; i <= 1; i++) {
                    float2 g = float2(float(i), float(j));
                    float2 o = hash22(ip + g);
                    float2 r = g + o - fp;
                    float d = dot(r, r);
                    if (d < d1) {
                        d2 = d1;
                        d1 = d;
                        id = ip + g;
                    } else if (d < d2) {
                        d2 = d;
                    }
                }
            }
            float edge = sqrt(d2) - sqrt(d1);
            float px = cells / min(res.x, res.y);
            float line = 1.0 - smoothstep(lead, lead + px * 1.5, edge);
            float h = hash21(id * 1.37);
            float3 col = palc(h).rgb;
            col *= 0.8 + 0.35 * vnoise(p * 3.0 + id);
            col += 0.15 * (1.0 - sqrt(d1)) ;
            col = mix(col, leadColor.rgb, line);
            return half4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    public override string Id => "stained-glass";
    public override string Name => "Stained Glass";
    public override StyleCategory Category => StyleCategory.Organic;

    public override void Render(RenderContext ctx)
    {
        var builder = Shaders.Create(Id, Sksl, ctx);
        var cells = ctx.Count(4, 22);
        builder.Uniforms["cells"] = (float)cells;
        builder.Uniforms["lead"] = ctx.R(0.03f, 0.09f);
        var lead = ctx.Chance(0.7) ? SKColor.Parse("#16161A") : ctx.Palette.Background;
        builder.Uniforms["leadColor"] = new[] { lead.Red / 255f, lead.Green / 255f, lead.Blue / 255f, 1f };
        Shaders.Fill(ctx, builder);
    }
}
