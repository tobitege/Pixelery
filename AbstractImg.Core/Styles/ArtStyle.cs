using AbstractImg.Core.Rendering;
using SkiaSharp;

namespace AbstractImg.Core.Styles;

public enum StyleCategory
{
    Gradients,
    Geometric,
    Organic,
    Procedural,
    Retro,
    Illusions
}

/// <summary>
/// One kind of abstract image. Implementations derive every decision from <see cref="RenderContext.Rng"/>
/// so an image can be reproduced from its seed.
/// </summary>
public abstract class ArtStyle
{
    /// <summary>Stable identifier (kebab-case), used in settings and file names.</summary>
    public abstract string Id { get; }

    /// <summary>English display name (style names are not translated).</summary>
    public abstract string Name { get; }

    public abstract StyleCategory Category { get; }

    public abstract void Render(RenderContext ctx);

    public override string ToString() => Name;

    /// <summary>
    /// Paints into a smaller offscreen surface and scales the result up. Used for soft, blurred styles where
    /// full resolution would only cost time.
    /// </summary>
    protected static void RenderLowRes(RenderContext ctx, int maxEdge, Action<RenderContext> paint)
    {
        var scale = Math.Min(1f, maxEdge / ctx.Max);
        var w = Math.Max(8, (int)MathF.Ceiling(ctx.Width * scale));
        var h = Math.Max(8, (int)MathF.Ceiling(ctx.Height * scale));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var small = new RenderContext(surface.Canvas, w, h, ctx.Rng, ctx.Palette, ctx.Detail);
        paint(small);
        using var image = surface.Snapshot();
        using var paintUp = new SKPaint { IsDither = true };
        ctx.Canvas.DrawImage(image, ctx.Bounds, new SKSamplingOptions(SKCubicResampler.Mitchell), paintUp);
    }

    /// <summary>Smooth closed path through the points (Catmull-Rom converted to cubic Béziers).</summary>
    protected static SKPath SmoothClosedPath(IReadOnlyList<SKPoint> pts)
    {
        var path = new SKPath();
        var n = pts.Count;
        if (n < 3)
            return path;
        path.MoveTo(pts[0]);
        for (var i = 0; i < n; i++)
        {
            var p0 = pts[(i - 1 + n) % n];
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];
            var p3 = pts[(i + 2) % n];
            path.CubicTo(
                p1.X + (p2.X - p0.X) / 6f, p1.Y + (p2.Y - p0.Y) / 6f,
                p2.X - (p3.X - p1.X) / 6f, p2.Y - (p3.Y - p1.Y) / 6f,
                p2.X, p2.Y);
        }
        path.Close();
        return path;
    }

    /// <summary>Smooth open path through the points.</summary>
    protected static SKPath SmoothOpenPath(IReadOnlyList<SKPoint> pts)
    {
        var path = new SKPath();
        var n = pts.Count;
        if (n == 0)
            return path;
        path.MoveTo(pts[0]);
        for (var i = 0; i < n - 1; i++)
        {
            var p0 = pts[Math.Max(i - 1, 0)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(i + 2, n - 1)];
            path.CubicTo(
                p1.X + (p2.X - p0.X) / 6f, p1.Y + (p2.Y - p0.Y) / 6f,
                p2.X - (p3.X - p1.X) / 6f, p2.Y - (p3.Y - p1.Y) / 6f,
                p2.X, p2.Y);
        }
        return path;
    }
}
