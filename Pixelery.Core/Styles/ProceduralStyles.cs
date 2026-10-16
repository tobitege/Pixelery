using Pixelery.Core.Palettes;
using Pixelery.Core.Rendering;
using SkiaSharp;

namespace Pixelery.Core.Styles;

/// <summary>Classic demo-scene plasma from interfering sine fields, smooth or posterized.</summary>
public sealed class PlasmaStyle : ArtStyle
{
    private const string Sksl = """
        uniform float4 k;
        uniform float2 c1;
        uniform float bands;
        uniform float scale;
        uniform float shift;
        uniform float swirl;

        half4 main(float2 c) {
            float2 p = uv(c);
            p += swirl * 0.08 * float2(sin(p.y * 7.0), sin(p.x * 6.0));
            float v = sin(p.x * k.x)
                + sin((p.y * k.y + p.x * k.z) * 0.5)
                + sin(length(p - c1) * k.w)
                + sin(length(p + c1.yx) * k.x * 0.7);
            float t = v * 0.125 * scale + shift;
            if (bands > 0.5) {
                t = floor(t * bands) / bands;
            }
            return half4(palc(t).rgb, 1.0);
        }
        """;

    public override string Id => "plasma";
    public override string Name => "Plasma";
    public override StyleCategory Category => StyleCategory.Procedural;

    public override void Render(RenderContext ctx)
    {
        var builder = Shaders.Create(Id, Sksl, ctx, cyclicRamp: true);
        var f = 6 + ctx.Detail * 14;
        builder.Uniforms["k"] = new[] { ctx.R(0.5f, 1.5f) * f, ctx.R(0.5f, 1.5f) * f, ctx.R(0.3f, 1.2f) * f, ctx.R(0.6f, 1.6f) * f };
        builder.Uniforms["c1"] = new[] { ctx.R(-0.4f, 0.4f), ctx.R(-0.4f, 0.4f) };
        builder.Uniforms["bands"] = ctx.Chance(0.35) ? (float)ctx.Ri(5, 14) : 0f;
        builder.Uniforms["scale"] = ctx.R(0.6f, 2f);
        builder.Uniforms["shift"] = ctx.R(0, 1);
        builder.Uniforms["swirl"] = ctx.Chance(0.5) ? ctx.R(0.5f, 2f) : 0f;
        Shaders.Fill(ctx, builder);
    }
}

/// <summary>Julia sets and Mandelbrot close-ups with smooth iteration coloring.</summary>
public sealed class FractalStyle : ArtStyle
{
    private const string Sksl = """
        uniform float mode;
        uniform float2 cpar;
        uniform float2 center;
        uniform float zoom;
        uniform float rot;
        uniform float cscale;
        uniform float coff;
        uniform float4 inside;

        half4 main(float2 c) {
            float2 p = uv(c);
            float cs = cos(rot);
            float sn = sin(rot);
            p = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) / zoom + center;
            float2 z = mode < 0.5 ? p : float2(0.0);
            float2 k = mode < 0.5 ? cpar : p;
            float it = 0.0;
            float escaped = 0.0;
            for (int i = 0; i < 220; i++) {
                z = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y) + k;
                if (dot(z, z) > 256.0) {
                    escaped = 1.0;
                    break;
                }
                it += 1.0;
            }
            if (escaped < 0.5) {
                return half4(inside);
            }
            float sm = it - log2(log2(dot(z, z))) + 4.0;
            float3 col = palc(sqrt(sm) * cscale + coff).rgb;
            return half4(col, 1.0);
        }
        """;

    private static readonly (float X, float Y)[] JuliaConstants =
    [
        (-0.8f, 0.156f), (-0.4f, 0.6f), (0.285f, 0.01f), (-0.70176f, -0.3842f), (-0.835f, -0.2321f),
        (-0.7269f, 0.1889f), (0.45f, 0.1428f), (-0.75f, 0.11f), (-0.1f, 0.651f), (0.355f, 0.355f),
        (-0.54f, 0.54f), (0.37f, 0.1f)
    ];

    private static readonly (float X, float Y, float MaxZoom)[] MandelbrotSpots =
    [
        (-0.743643887f, 0.131825904f, 300), (-0.7453f, 0.1127f, 150), (-0.16f, 1.0405f, 60),
        (-1.25066f, 0.02012f, 250), (-0.7746806f, -0.1374169f, 120), (-0.088f, 0.654f, 40),
        (0.2549870f, 0.0005f, 100), (-1.7686f, -0.0017f, 120)
    ];

    /// <summary>Share of probe points that never escape (same mapping as the shader).</summary>
    private static float InteriorShare(RenderContext ctx, bool julia, float cx, float cy, float centerX, float centerY, float zoom, float rot)
    {
        const int grid = 24;
        var inside = 0;
        var cs = MathF.Cos(rot);
        var sn = MathF.Sin(rot);
        var min = ctx.Min;
        for (var gy = 0; gy < grid; gy++)
        {
            for (var gx = 0; gx < grid; gx++)
            {
                var px = ((gx + 0.5f) / grid * ctx.Width - ctx.Width / 2f) / min;
                var py = ((gy + 0.5f) / grid * ctx.Height - ctx.Height / 2f) / min;
                var x = (px * cs - py * sn) / zoom + centerX;
                var y = (px * sn + py * cs) / zoom + centerY;
                float zx = julia ? x : 0, zy = julia ? y : 0;
                float kx = julia ? cx : x, ky = julia ? cy : y;
                var escaped = false;
                for (var i = 0; i < 220; i++)
                {
                    var t = zx * zx - zy * zy + kx;
                    zy = 2 * zx * zy + ky;
                    zx = t;
                    if (zx * zx + zy * zy > 256)
                    {
                        escaped = true;
                        break;
                    }
                }
                if (!escaped)
                    inside++;
            }
        }
        return inside / (float)(grid * grid);
    }

    public override string Id => "fractal";
    public override string Name => "Fractal";
    public override StyleCategory Category => StyleCategory.Procedural;

    public override void Render(RenderContext ctx)
    {
        var builder = Shaders.Create(Id, Sksl, ctx, cyclicRamp: true);
        // Some parameter sets show almost nothing (all inside or all escaped); probe a coarse grid and retry.
        var julia = false;
        float cx = 0, cy = 0, centerX = 0, centerY = 0, zoom = 1, rot = 0;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            julia = ctx.Chance(0.65);
            if (julia)
            {
                var (x, y) = ctx.Pick(JuliaConstants);
                cx = x + ctx.R(-0.01f, 0.01f);
                cy = y + ctx.R(-0.01f, 0.01f);
                centerX = ctx.R(-0.3f, 0.3f);
                centerY = ctx.R(-0.3f, 0.3f);
                zoom = ctx.R(0.35f, 1.4f);
            }
            else
            {
                var (x, y, maxZoom) = ctx.Pick(MandelbrotSpots);
                cx = cy = 0;
                centerX = x;
                centerY = y;
                zoom = ctx.R(maxZoom * 0.15f, maxZoom);
            }
            rot = ctx.R(0, MathF.Tau);
            var interior = InteriorShare(ctx, julia, cx, cy, centerX, centerY, zoom, rot);
            if (interior is > 0.02f and < 0.55f)
                break;
        }
        builder.Uniforms["mode"] = julia ? 0f : 1f;
        builder.Uniforms["cpar"] = new[] { cx, cy };
        builder.Uniforms["center"] = new[] { centerX, centerY };
        builder.Uniforms["zoom"] = zoom;
        builder.Uniforms["rot"] = rot;
        builder.Uniforms["cscale"] = ctx.R(0.15f, 0.5f);
        builder.Uniforms["coff"] = ctx.R(0, 1);
        var inside = ctx.Chance(0.6) ? SKColor.Parse("#0A0A0F") : ctx.Palette.Background;
        builder.Uniforms["inside"] = new[] { inside.Red / 255f, inside.Green / 255f, inside.Blue / 255f, 1f };
        Shaders.Fill(ctx, builder);
    }
}

/// <summary>Damped pendulum curves (harmonograph) and spirographs drawn as glowing threads.</summary>
public sealed class HarmonographStyle : ArtStyle
{
    public override string Id => "harmonograph";
    public override string Name => "Harmonograph";
    public override StyleCategory Category => StyleCategory.Procedural;

    public override void Render(RenderContext ctx)
    {
        var bg = ctx.Chance(0.75) ? ColorMath.FromOkLch(ctx.R(0.1f, 0.18f), 0.03f, ColorMath.ToOkLch(ctx.Color()).H) : ColorMath.FromOkLch(0.95f, 0.015f, 85);
        ctx.Clear(bg);
        var dark = ColorMath.Luminance(bg) < 0.5f;
        var points = ctx.Chance(0.65) ? Harmonograph(ctx) : Spirograph(ctx);
        var segment = 400;
        using var paint = ctx.Stroke(SKColors.White, Math.Max(0.8f, ctx.U * ctx.R(0.8f, 1.8f)));
        paint.BlendMode = dark ? SKBlendMode.Screen : SKBlendMode.Multiply;
        var alpha = (byte)ctx.Ri(120, 220);
        for (var start = 0; start < points.Length - 1; start += segment)
        {
            var count = Math.Min(segment + 1, points.Length - start);
            paint.Color = ctx.Palette.Sample(start / (float)points.Length).WithAlpha(alpha);
            using var path = new SKPath();
            path.AddPoly(points.AsSpan(start, count).ToArray(), false);
            ctx.Canvas.DrawPath(path, paint);
        }
    }

    private static SKPoint[] Harmonograph(RenderContext ctx)
    {
        float[] ratios = [1, 2, 3, 1.5f, 4];
        float F() => ctx.Pick(ratios) + ctx.R(-0.015f, 0.015f);
        var f = new[] { F(), F(), F(), F() };
        var p = Enumerable.Range(0, 4).Select(_ => ctx.R(0, MathF.Tau)).ToArray();
        var d = Enumerable.Range(0, 4).Select(_ => ctx.R(0.0015f, 0.005f)).ToArray();
        var a = new[] { ctx.R(0.4f, 1f), ctx.R(0.2f, 0.6f), ctx.R(0.4f, 1f), ctx.R(0.2f, 0.6f) };
        var n = ctx.Count(20000, 70000);
        var scale = ctx.Min * 0.45f / Math.Max(a[0] + a[1], a[2] + a[3]);
        var c = ctx.Center;
        var pts = new SKPoint[n];
        for (var i = 0; i < n; i++)
        {
            var t = i * 0.02f;
            var x = a[0] * MathF.Sin(f[0] * t + p[0]) * MathF.Exp(-d[0] * t) + a[1] * MathF.Sin(f[1] * t + p[1]) * MathF.Exp(-d[1] * t);
            var y = a[2] * MathF.Sin(f[2] * t + p[2]) * MathF.Exp(-d[2] * t) + a[3] * MathF.Sin(f[3] * t + p[3]) * MathF.Exp(-d[3] * t);
            pts[i] = new SKPoint(c.X + x * scale, c.Y + y * scale);
        }
        return pts;
    }

    private static SKPoint[] Spirograph(RenderContext ctx)
    {
        var bigR = ctx.Ri(5, 15);
        var smallR = ctx.Ri(2, bigR);
        var arm = ctx.R(0.4f, 1.4f) * smallR;
        var gcd = Gcd(bigR, smallR);
        var turns = smallR / gcd;
        // Several slightly rotated and shrunk copies give the curve the density of a real spirograph drawing.
        var copies = ctx.Count(4, 14);
        var perCopy = Math.Min(80000 / copies, turns * 1600);
        var scale = ctx.Min * 0.45f / (bigR - smallR + arm);
        var spin = ctx.R(2, 12) * MathF.PI / 180f;
        var c = ctx.Center;
        var pts = new SKPoint[perCopy * copies];
        for (var k = 0; k < copies; k++)
        {
            var shrink = 1 - k * 0.025f;
            var ca = MathF.Cos(k * spin);
            var sa = MathF.Sin(k * spin);
            for (var i = 0; i < perCopy; i++)
            {
                var t = i / (float)(perCopy - 1) * MathF.Tau * turns;
                var x = (bigR - smallR) * MathF.Cos(t) + arm * MathF.Cos((bigR - smallR) / (float)smallR * t);
                var y = (bigR - smallR) * MathF.Sin(t) - arm * MathF.Sin((bigR - smallR) / (float)smallR * t);
                pts[k * perCopy + i] = new SKPoint(c.X + (x * ca - y * sa) * scale * shrink, c.Y + (x * sa + y * ca) * scale * shrink);
            }
        }
        return pts;
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
