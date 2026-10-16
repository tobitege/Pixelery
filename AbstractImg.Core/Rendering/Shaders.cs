using System.Collections.Concurrent;
using SkiaSharp;

namespace AbstractImg.Core.Rendering;

/// <summary>
/// Compiles and caches SkSL runtime shaders. Every shader gets a shared prelude with hash, value noise,
/// fractal noise and a palette ramp lookup. Coordinates are normalized with <c>uv()</c>: the shorter edge spans -0.5..0.5.
/// </summary>
public static class Shaders
{
    private const string Prelude = """
        uniform float2 res;
        uniform float seed;
        uniform shader ramp;

        float2 uv(float2 c) { return (c - 0.5 * res) / min(res.x, res.y); }

        half4 pal(float t) { return ramp.eval(float2(clamp(t, 0.0, 1.0) * 255.0 + 0.5, 0.5)); }

        half4 palc(float t) { return ramp.eval(float2(fract(t) * 255.0 + 0.5, 0.5)); }

        float hash21(float2 p) {
            p = fract(p * float2(123.34, 456.21) + seed * 0.1234);
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        float2 hash22(float2 p) {
            float3 a = fract(float3(p.x, p.y, p.x) * float3(123.34, 234.34, 345.65) + seed * 0.0731);
            a += dot(a, a + 34.45);
            return fract(float2(a.x * a.y, a.y * a.z));
        }

        float vnoise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = hash21(i);
            float b = hash21(i + float2(1.0, 0.0));
            float c = hash21(i + float2(0.0, 1.0));
            float d = hash21(i + float2(1.0, 1.0));
            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        float fbm4(float2 p) {
            float s = 0.0;
            float a = 0.5;
            for (int i = 0; i < 4; i++) {
                s += a * vnoise(p);
                p = float2x2(1.6, 1.2, -1.2, 1.6) * p;
                a *= 0.5;
            }
            return s / 0.9375;
        }

        float fbm6(float2 p) {
            float s = 0.0;
            float a = 0.5;
            for (int i = 0; i < 6; i++) {
                s += a * vnoise(p);
                p = float2x2(1.6, 1.2, -1.2, 1.6) * p;
                a *= 0.5;
            }
            return s / 0.984375;
        }

        """;

    private static readonly ConcurrentDictionary<string, SKRuntimeEffect> Cache = new();

    /// <summary>
    /// Returns a builder for the cached shader with the common uniforms (resolution, seed, palette ramp) already set.
    /// </summary>
    public static SKRuntimeShaderBuilder Create(string key, string body, RenderContext ctx, bool cyclicRamp = false, bool rampWithBackground = false)
    {
        var effect = Cache.GetOrAdd(key, static (k, source) =>
        {
            var compiled = SKRuntimeEffect.CreateShader(Prelude + source, out var errors);
            return compiled ?? throw new InvalidOperationException($"SkSL shader '{k}' failed to compile: {errors}");
        }, body);

        var builder = new SKRuntimeShaderBuilder(effect);
        builder.Uniforms["res"] = new[] { (float)ctx.Width, ctx.Height };
        builder.Uniforms["seed"] = ctx.R(0, 100);
        builder.Children["ramp"] = ctx.Palette.CreateRamp(cyclicRamp, rampWithBackground);
        return builder;
    }

    /// <summary>Fills the whole canvas with the built shader.</summary>
    public static void Fill(RenderContext ctx, SKRuntimeShaderBuilder builder)
    {
        using var shader = builder.Build();
        using var paint = new SKPaint { Shader = shader };
        ctx.Canvas.DrawRect(ctx.Bounds, paint);
    }
}
