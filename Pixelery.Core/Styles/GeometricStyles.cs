using Pixelery.Core.Palettes;
using Pixelery.Core.Rendering;
using SkiaSharp;

namespace Pixelery.Core.Styles;

/// <summary>Bauhaus-inspired grid of quarter circles, half circles, triangles and stripes.</summary>
public sealed class BauhausStyle : ArtStyle
{
    public override string Id => "bauhaus";
    public override string Name => "Bauhaus";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var cols = ctx.Count(3, 10) + (ctx.Width > ctx.Height ? 1 : 0);
        var cell = ctx.Width / (float)cols;
        var rows = (int)MathF.Ceiling(ctx.Height / cell);
        var offsetY = (ctx.Height - rows * cell) / 2f;
        var gap = ctx.Chance(0.4) ? cell * ctx.R(0.03f, 0.08f) : 0;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = c * cell + gap / 2;
                var y = offsetY + r * cell + gap / 2;
                var s = cell - gap;
                var bg = ctx.Chance(0.3) ? ctx.Palette.Background : ctx.Color();
                var fg = ctx.Palette.PickOther(ctx.Rng, bg);
                using var bgPaint = ctx.Fill(bg);
                using var fgPaint = ctx.Fill(fg);
                ctx.Canvas.DrawRect(x, y, s, s, bgPaint);
                ctx.Canvas.Save();
                ctx.Canvas.ClipRect(new SKRect(x, y, x + s, y + s));
                ctx.Canvas.Translate(x + s / 2, y + s / 2);
                ctx.Canvas.RotateDegrees(90 * ctx.Ri(0, 4));
                DrawMotif(ctx, s, fgPaint, ctx.Ri(0, 9));
                ctx.Canvas.Restore();
            }
        }
    }

    private static void DrawMotif(RenderContext ctx, float s, SKPaint paint, int motif)
    {
        var h = s / 2;
        var canvas = ctx.Canvas;
        switch (motif)
        {
            case 0: // quarter circle from a corner
                canvas.DrawCircle(-h, -h, s, paint);
                break;
            case 1: // half circle on an edge
                canvas.DrawCircle(0, -h, h, paint);
                break;
            case 2: // full circle
                canvas.DrawCircle(0, 0, h * ctx.R(0.5f, 0.9f), paint);
                break;
            case 3: // triangle across the diagonal
            {
                using var path = new SKPath();
                path.MoveTo(-h, -h);
                path.LineTo(h, -h);
                path.LineTo(-h, h);
                path.Close();
                canvas.DrawPath(path, paint);
                break;
            }
            case 4: // stripes
            {
                var n = ctx.Ri(3, 6);
                var w = s / (n * 2f);
                for (var i = 0; i < n; i++)
                    canvas.DrawRect(-h + w * (i * 2 + 0.5f), -h, w, s, paint);
                break;
            }
            case 5: // two quarter circles (leaf)
            {
                canvas.Save();
                canvas.ClipRect(new SKRect(-h, -h, h, h));
                using var path = new SKPath();
                path.AddCircle(-h, -h, s);
                using var other = new SKPath();
                other.AddCircle(h, h, s);
                using var leaf = path.Op(other, SKPathOp.Intersect);
                if (leaf != null)
                    canvas.DrawPath(leaf, paint);
                canvas.Restore();
                break;
            }
            case 6: // ring
            {
                using var ring = new SKPaint { IsAntialias = true, Color = paint.Color, Style = SKPaintStyle.Stroke, StrokeWidth = s * 0.14f };
                canvas.DrawCircle(0, 0, h * 0.62f, ring);
                break;
            }
            case 7: // half square
                canvas.DrawRect(-h, -h, s, h, paint);
                break;
            default: // small dot pattern
            {
                var n = ctx.Ri(2, 4);
                var step = s / n;
                for (var i = 0; i < n; i++)
                    for (var j = 0; j < n; j++)
                        canvas.DrawCircle(-h + step * (i + 0.5f), -h + step * (j + 0.5f), step * 0.28f, paint);
                break;
            }
        }
    }
}

/// <summary>Truchet tilings: arcs, mazes and triangles.</summary>
public sealed class TruchetStyle : ArtStyle
{
    public override string Id => "truchet";
    public override string Name => "Truchet";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var tile = ctx.Min / ctx.Count(6, 26);
        var cols = (int)MathF.Ceiling(ctx.Width / tile) + 1;
        var rows = (int)MathF.Ceiling(ctx.Height / tile) + 1;
        var variant = ctx.Ri(0, 4);
        var multiColor = ctx.Chance(0.5);
        var ink = ctx.Chance(0.5) ? ctx.Palette.Ink : ctx.Color();
        var width = tile * ctx.R(0.12f, 0.3f);
        var rings = variant == 3 ? ctx.Ri(2, 4) : 1;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = c * tile;
                var y = r * tile;
                var color = multiColor ? ctx.Color() : ink;
                var flip = ctx.Chance(0.5);
                switch (variant)
                {
                    case 0:
                    case 3:
                    {
                        using var paint = ctx.Stroke(color, width / rings);
                        paint.StrokeCap = SKStrokeCap.Butt;
                        for (var k = 1; k <= rings; k++)
                        {
                            var radius = tile / 2 * (rings == 1 ? 1 : k / (float)rings * 1.6f - 0.3f);
                            if (radius <= 0)
                                continue;
                            if (flip)
                            {
                                ctx.Canvas.DrawArc(new SKRect(x - radius, y - radius, x + radius, y + radius), 0, 90, false, paint);
                                ctx.Canvas.DrawArc(new SKRect(x + tile - radius, y + tile - radius, x + tile + radius, y + tile + radius), 180, 90, false, paint);
                            }
                            else
                            {
                                ctx.Canvas.DrawArc(new SKRect(x + tile - radius, y - radius, x + tile + radius, y + radius), 90, 90, false, paint);
                                ctx.Canvas.DrawArc(new SKRect(x - radius, y + tile - radius, x + radius, y + tile + radius), 270, 90, false, paint);
                            }
                        }
                        break;
                    }
                    case 1:
                    {
                        using var paint = ctx.Stroke(color, width * 0.7f);
                        if (flip)
                            ctx.Canvas.DrawLine(x, y, x + tile, y + tile, paint);
                        else
                            ctx.Canvas.DrawLine(x + tile, y, x, y + tile, paint);
                        break;
                    }
                    default:
                    {
                        using var paint = ctx.Fill(color);
                        using var path = new SKPath();
                        var corner = ctx.Ri(0, 4);
                        SKPoint[] corners = [new(x, y), new(x + tile, y), new(x + tile, y + tile), new(x, y + tile)];
                        path.MoveTo(corners[corner]);
                        path.LineTo(corners[(corner + 1) % 4]);
                        path.LineTo(corners[(corner + 3) % 4]);
                        path.Close();
                        ctx.Canvas.DrawPath(path, paint);
                        break;
                    }
                }
            }
        }
    }
}

/// <summary>Low-poly triangle mesh shaded along a gradient.</summary>
public sealed class LowPolyStyle : ArtStyle
{
    public override string Id => "low-poly";
    public override string Name => "Low Poly";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        var cols = ctx.Count(6, 28);
        var cellW = ctx.Width / (float)cols;
        var rows = Math.Max(2, (int)MathF.Round(ctx.Height / cellW));
        var cellH = ctx.Height / (float)rows;
        var points = new SKPoint[cols + 1, rows + 1];
        for (var r = 0; r <= rows; r++)
        {
            for (var c = 0; c <= cols; c++)
            {
                var jx = c == 0 || c == cols ? 0 : ctx.R(-0.45f, 0.45f) * cellW;
                var jy = r == 0 || r == rows ? 0 : ctx.R(-0.45f, 0.45f) * cellH;
                points[c, r] = new SKPoint(c * cellW + jx, r * cellH + jy);
            }
        }

        var angle = ctx.R(0, MathF.Tau);
        var dir = new SKPoint(MathF.Cos(angle), MathF.Sin(angle));
        var span = Math.Abs(dir.X) * ctx.Width + Math.Abs(dir.Y) * ctx.Height;
        var noiseScale = ctx.R(1.5f, 4f) / ctx.Max;
        var light = ctx.R(0.04f, 0.12f);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 1 };
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var a = points[c, r];
                var b = points[c + 1, r];
                var d = points[c + 1, r + 1];
                var e = points[c, r + 1];
                if (ctx.Chance(0.5))
                {
                    DrawTriangle(a, b, d);
                    DrawTriangle(a, d, e);
                }
                else
                {
                    DrawTriangle(a, b, e);
                    DrawTriangle(b, d, e);
                }
            }
        }

        void DrawTriangle(SKPoint p1, SKPoint p2, SKPoint p3)
        {
            var cx = (p1.X + p2.X + p3.X) / 3;
            var cy = (p1.Y + p2.Y + p3.Y) / 3;
            var projected = ((cx - ctx.Width / 2f) * dir.X + (cy - ctx.Height / 2f) * dir.Y) / span + 0.5f;
            var t = projected + ctx.Noise.Noise(cx * noiseScale, cy * noiseScale) * 0.15f;
            var color = ctx.Palette.Sample(Math.Clamp(t, 0, 1));
            color = ColorMath.Lighten(color, ctx.R(-light, light));
            paint.Color = color;
            using var path = new SKPath();
            path.MoveTo(p1);
            path.LineTo(p2);
            path.LineTo(p3);
            path.Close();
            ctx.Canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Recursive rectangle subdivision: classic Mondrian or modern rounded tiles.</summary>
public sealed class SubdivisionStyle : ArtStyle
{
    public override string Id => "subdivision";
    public override string Name => "Subdivision";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        var mondrian = ctx.Chance(0.35);
        var rects = new List<SKRect>();
        Split(ctx, new SKRect(0, 0, ctx.Width, ctx.Height), 0, ctx.Count(3, 7), rects);

        if (mondrian)
        {
            var white = ColorMath.FromOkLch(0.96f, 0.01f, 90);
            SKColor[] primaries = ctx.Chance(0.7)
                ? [SKColor.Parse("#D0312D"), SKColor.Parse("#F2C230"), SKColor.Parse("#1F4E9C"), SKColor.Parse("#111111")]
                : [.. ctx.Palette.Colors];
            ctx.Clear(white);
            var line = ctx.U * ctx.R(6, 14);
            using var stroke = new SKPaint { Color = SKColor.Parse("#111111"), Style = SKPaintStyle.Stroke, StrokeWidth = line };
            foreach (var rect in rects)
            {
                if (ctx.Chance(0.38))
                {
                    using var fill = ctx.Fill(ctx.Pick(primaries));
                    ctx.Canvas.DrawRect(rect, fill);
                }
                ctx.Canvas.DrawRect(rect, stroke);
            }
            return;
        }

        ctx.Clear();
        var gap = ctx.U * ctx.R(4, 16);
        var radius = ctx.U * ctx.R(0, 40);
        foreach (var rect in rects)
        {
            var inner = SKRect.Inflate(rect, -gap / 2, -gap / 2);
            if (inner.Width <= 1 || inner.Height <= 1)
                continue;
            var color = ctx.Color();
            using var fill = ctx.Fill(color);
            ctx.Canvas.DrawRoundRect(inner, radius, radius, fill);
            if (ctx.Chance(0.3))
            {
                using var accent = ctx.Fill(ctx.Palette.PickOther(ctx.Rng, color));
                var r = Math.Min(inner.Width, inner.Height) * ctx.R(0.2f, 0.45f);
                switch (ctx.Ri(0, 3))
                {
                    case 0:
                        ctx.Canvas.DrawCircle(inner.MidX, inner.MidY, r, accent);
                        break;
                    case 1:
                        ctx.Canvas.Save();
                        ctx.Canvas.ClipRoundRect(new SKRoundRect(inner, radius, radius), antialias: true);
                        ctx.Canvas.DrawCircle(inner.Left, inner.Bottom, r * 2, accent);
                        ctx.Canvas.Restore();
                        break;
                    default:
                        ctx.Canvas.DrawRoundRect(SKRect.Inflate(inner, -inner.Width * 0.3f, -inner.Height * 0.3f), radius / 2, radius / 2, accent);
                        break;
                }
            }
        }
    }

    private static void Split(RenderContext ctx, SKRect rect, int depth, int maxDepth, List<SKRect> output)
    {
        var minSize = ctx.Min * 0.06f;
        var canSplit = rect.Width > minSize * 2 || rect.Height > minSize * 2;
        if (depth >= maxDepth || !canSplit || (depth > 1 && ctx.Chance(0.18)))
        {
            output.Add(rect);
            return;
        }
        var vertical = rect.Width > rect.Height ? ctx.Chance(0.75) : ctx.Chance(0.25);
        if (vertical && rect.Width <= minSize * 2)
            vertical = false;
        if (!vertical && rect.Height <= minSize * 2)
            vertical = true;
        var t = ctx.R(0.25f, 0.75f);
        if (vertical)
        {
            var x = rect.Left + rect.Width * t;
            Split(ctx, new SKRect(rect.Left, rect.Top, x, rect.Bottom), depth + 1, maxDepth, output);
            Split(ctx, new SKRect(x, rect.Top, rect.Right, rect.Bottom), depth + 1, maxDepth, output);
        }
        else
        {
            var y = rect.Top + rect.Height * t;
            Split(ctx, new SKRect(rect.Left, rect.Top, rect.Right, y), depth + 1, maxDepth, output);
            Split(ctx, new SKRect(rect.Left, y, rect.Right, rect.Bottom), depth + 1, maxDepth, output);
        }
    }
}

/// <summary>Non-overlapping circles of many sizes, filled, ringed or outlined.</summary>
public sealed class CirclePackingStyle : ArtStyle
{
    public override string Id => "circle-packing";
    public override string Name => "Circle Packing";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var maxR = ctx.Min * ctx.R(0.12f, 0.3f);
        var minR = ctx.Min * (0.004f + (1 - ctx.Detail) * 0.01f);
        var padding = ctx.U * ctx.R(1, 6);
        var mode = ctx.Ri(0, 3);
        var cellSize = Math.Max(minR * 4, ctx.Min / 40f);
        var gridW = (int)(ctx.Width / cellSize) + 1;
        var gridH = (int)(ctx.Height / cellSize) + 1;
        var grid = new List<int>[gridW * gridH];
        var circles = new List<(float X, float Y, float R)>();
        var attempts = ctx.AreaCount(9000, 2000, 60000);

        for (var i = 0; i < attempts; i++)
        {
            // Large circles first, then progressively smaller ones fill the gaps.
            var progress = i / (float)attempts;
            var r = maxR * MathF.Pow(1 - progress, 2.2f) + minR;
            var x = ctx.R(0, ctx.Width);
            var y = ctx.R(0, ctx.Height);
            if (!Fits(x, y, r))
                continue;
            circles.Add((x, y, r));
            Insert(circles.Count - 1, x, y, r);
        }

        foreach (var (x, y, r) in circles)
        {
            var color = ctx.Color();
            switch (mode)
            {
                case 0:
                {
                    using var fill = ctx.Fill(color);
                    ctx.Canvas.DrawCircle(x, y, r, fill);
                    break;
                }
                case 1:
                {
                    var rings = Math.Max(1, (int)(r / (ctx.U * 14)));
                    for (var k = rings; k >= 1; k--)
                    {
                        using var fill = ctx.Fill(k % 2 == 0 ? ctx.Palette.Background : color);
                        ctx.Canvas.DrawCircle(x, y, r * k / rings, fill);
                    }
                    break;
                }
                default:
                {
                    using var stroke = ctx.Stroke(color, Math.Max(1, r * 0.08f));
                    ctx.Canvas.DrawCircle(x, y, r - stroke.StrokeWidth / 2, stroke);
                    break;
                }
            }
        }

        bool Fits(float x, float y, float r)
        {
            var reach = r + maxR + padding;
            var x0 = Math.Max(0, (int)((x - reach) / cellSize));
            var x1 = Math.Min(gridW - 1, (int)((x + reach) / cellSize));
            var y0 = Math.Max(0, (int)((y - reach) / cellSize));
            var y1 = Math.Min(gridH - 1, (int)((y + reach) / cellSize));
            for (var gy = y0; gy <= y1; gy++)
            {
                for (var gx = x0; gx <= x1; gx++)
                {
                    var bucket = grid[gy * gridW + gx];
                    if (bucket == null)
                        continue;
                    foreach (var index in bucket)
                    {
                        var c = circles[index];
                        var dx = c.X - x;
                        var dy = c.Y - y;
                        var min = c.R + r + padding;
                        if (dx * dx + dy * dy < min * min)
                            return false;
                    }
                }
            }
            return true;
        }

        void Insert(int index, float x, float y, float r)
        {
            // Register the circle at its center cell only; Fits() searches a radius that covers the largest circle.
            var gx = Math.Clamp((int)(x / cellSize), 0, gridW - 1);
            var gy = Math.Clamp((int)(y / cellSize), 0, gridH - 1);
            (grid[gy * gridW + gx] ??= []).Add(index);
        }
    }
}

/// <summary>80s Memphis design: squiggles, confetti, zigzags and bold shapes with offset shadows.</summary>
public sealed class MemphisStyle : ArtStyle
{
    public override string Id => "memphis";
    public override string Name => "Memphis";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var count = ctx.Count(25, 70);
        var shadow = ctx.Chance(0.6);
        var shadowOffset = ctx.U * ctx.R(4, 10);
        var ink = ctx.Palette.Ink;
        for (var i = 0; i < count; i++)
        {
            var x = ctx.R(0, ctx.Width);
            var y = ctx.R(0, ctx.Height);
            var size = ctx.U * ctx.R(30, 120);
            var color = ctx.Color();
            ctx.Canvas.Save();
            ctx.Canvas.Translate(x, y);
            ctx.Canvas.RotateDegrees(ctx.R(0, 360));
            var kind = ctx.Ri(0, 8);
            if (shadow)
            {
                ctx.Canvas.Save();
                ctx.Canvas.Translate(shadowOffset, shadowOffset);
                DrawShape(ctx, kind, size, ink);
                ctx.Canvas.Restore();
            }
            DrawShape(ctx, kind, size, color);
            ctx.Canvas.Restore();
        }
    }

    private static void DrawShape(RenderContext ctx, int kind, float size, SKColor color)
    {
        var canvas = ctx.Canvas;
        var lineWidth = size * 0.12f;
        // Shapes are deterministic per call, so shadow and shape share geometry: reseed from size.
        var local = new Random((int)(size * 1000));
        switch (kind)
        {
            case 0: // squiggle
            {
                using var path = new SKPath();
                var waves = 3 + local.Next(3);
                path.MoveTo(-size, 0);
                for (var k = 0; k < waves; k++)
                {
                    var x0 = -size + k * 2 * size / waves;
                    var dx = 2 * size / waves;
                    path.CubicTo(x0 + dx * 0.25f, k % 2 == 0 ? -size * 0.4f : size * 0.4f, x0 + dx * 0.75f, k % 2 == 0 ? -size * 0.4f : size * 0.4f, x0 + dx, 0);
                }
                using var paint = ctx.Stroke(color, lineWidth);
                canvas.DrawPath(path, paint);
                break;
            }
            case 1: // triangle
            {
                using var path = new SKPath();
                path.MoveTo(0, -size * 0.6f);
                path.LineTo(size * 0.55f, size * 0.4f);
                path.LineTo(-size * 0.55f, size * 0.4f);
                path.Close();
                using var paint = local.Next(2) == 0 ? ctx.Fill(color) : ctx.Stroke(color, lineWidth * 0.7f);
                canvas.DrawPath(path, paint);
                break;
            }
            case 2: // zigzag
            {
                using var path = new SKPath();
                var n = 5 + local.Next(4);
                for (var k = 0; k <= n; k++)
                {
                    var p = new SKPoint(-size + k * 2 * size / n, k % 2 == 0 ? -size * 0.2f : size * 0.2f);
                    if (k == 0)
                        path.MoveTo(p);
                    else
                        path.LineTo(p);
                }
                using var paint = ctx.Stroke(color, lineWidth * 0.8f);
                paint.StrokeJoin = SKStrokeJoin.Miter;
                canvas.DrawPath(path, paint);
                break;
            }
            case 3: // dots patch
            {
                using var paint = ctx.Fill(color);
                for (var gx = 0; gx < 4; gx++)
                    for (var gy = 0; gy < 4; gy++)
                        canvas.DrawCircle(-size * 0.5f + gx * size / 3, -size * 0.5f + gy * size / 3, size * 0.07f, paint);
                break;
            }
            case 4: // half circle
            {
                using var paint = ctx.Fill(color);
                canvas.DrawArc(new SKRect(-size * 0.5f, -size * 0.5f, size * 0.5f, size * 0.5f), 0, 180, true, paint);
                break;
            }
            case 5: // plus
            {
                using var paint = ctx.Stroke(color, lineWidth);
                paint.StrokeCap = SKStrokeCap.Square;
                canvas.DrawLine(-size * 0.3f, 0, size * 0.3f, 0, paint);
                canvas.DrawLine(0, -size * 0.3f, 0, size * 0.3f, paint);
                break;
            }
            case 6: // striped rectangle
            {
                using var paint = ctx.Fill(color);
                for (var k = 0; k < 5; k++)
                    canvas.DrawRect(-size * 0.5f, -size * 0.35f + k * size * 0.16f, size, size * 0.07f, paint);
                break;
            }
            default: // ring or disc
            {
                using var paint = local.Next(2) == 0 ? ctx.Fill(color) : ctx.Stroke(color, lineWidth);
                canvas.DrawCircle(0, 0, size * 0.35f, paint);
                break;
            }
        }
    }
}

/// <summary>Isometric cubes (tumbling blocks) with shaded faces.</summary>
public sealed class IsometricStyle : ArtStyle
{
    public override string Id => "isometric";
    public override string Name => "Isometric Cubes";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var size = ctx.Min / ctx.Count(5, 22);
        var w = size * MathF.Sqrt(3);
        var rowH = size * 1.5f;
        var missing = ctx.Chance(0.5) ? ctx.R(0.05f, 0.35f) : 0;
        var uniform = ctx.Chance(0.3);
        var baseColor = ctx.Color();
        var outline = ctx.Chance(0.35);
        using var stroke = ctx.Stroke(ctx.Palette.Background, Math.Max(1, size * 0.04f));

        var rows = (int)(ctx.Height / rowH) + 3;
        var cols = (int)(ctx.Width / w) + 3;
        for (var r = -1; r < rows; r++)
        {
            for (var c = -1; c < cols; c++)
            {
                if (ctx.Chance(missing))
                    continue;
                var cx = c * w + (r % 2 == 0 ? 0 : w / 2);
                var cy = r * rowH;
                var color = uniform ? baseColor : ctx.Color();
                var top = ColorMath.Lighten(color, 0.12f);
                var left = color;
                var right = ColorMath.Darken(color, 0.15f);
                SKPoint center = new(cx, cy);
                SKPoint up = new(cx, cy - size);
                SKPoint ul = new(cx - w / 2, cy - size / 2);
                SKPoint ur = new(cx + w / 2, cy - size / 2);
                SKPoint dl = new(cx - w / 2, cy + size / 2);
                SKPoint dr = new(cx + w / 2, cy + size / 2);
                SKPoint down = new(cx, cy + size);
                Face([center, ul, up, ur], top);
                Face([center, ul, dl, down], left);
                Face([center, ur, dr, down], right);
            }
        }

        void Face(SKPoint[] pts, SKColor color)
        {
            using var path = new SKPath();
            path.AddPoly(pts, true);
            using var fill = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 0.75f };
            ctx.Canvas.DrawPath(path, fill);
            if (outline)
                ctx.Canvas.DrawPath(path, stroke);
        }
    }
}

/// <summary>Rotated dot screens modulated by a noise or wave field, layered like print separations.</summary>
public sealed class HalftoneStyle : ArtStyle
{
    public override string Id => "halftone";
    public override string Name => "Halftone";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        ctx.Clear();
        var layers = ctx.Ri(1, 4);
        var spacing = ctx.Min / ctx.Count(30, 110);
        var field = ctx.Ri(0, 3);
        var blend = ctx.Palette.IsDark ? SKBlendMode.Screen : SKBlendMode.Multiply;
        var cx = ctx.R(0.2f, 0.8f) * ctx.Width;
        var cy = ctx.R(0.2f, 0.8f) * ctx.Height;
        var freq = ctx.R(1.5f, 4f) / ctx.Max;

        for (var l = 0; l < layers; l++)
        {
            var angle = (15 + l * 30 + ctx.R(-5, 5)) * MathF.PI / 180f;
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var offset = new SKPoint(ctx.R(0, 1000), ctx.R(0, 1000));
            using var paint = ctx.Fill(ctx.Palette[l + ctx.Ri(0, 5)]);
            paint.BlendMode = layers > 1 ? blend : SKBlendMode.SrcOver;
            var extent = ctx.Max * 0.75f;
            for (var gy = -extent; gy <= extent; gy += spacing)
            {
                for (var gx = -extent; gx <= extent; gx += spacing)
                {
                    var x = ctx.Width / 2f + gx * cos - gy * sin;
                    var y = ctx.Height / 2f + gx * sin + gy * cos;
                    if (x < -spacing || y < -spacing || x > ctx.Width + spacing || y > ctx.Height + spacing)
                        continue;
                    var v = field switch
                    {
                        0 => ctx.Noise.Fbm(x * freq + offset.X, y * freq + offset.Y, 3) * 0.5f + 0.5f,
                        1 => 1 - MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / ctx.Max,
                        _ => MathF.Sin((x * cos + y * sin) * freq * 8 + l) * 0.5f + 0.5f
                    };
                    var r = spacing * 0.55f * Math.Clamp(v, 0, 1);
                    if (r > 0.3f)
                        ctx.Canvas.DrawCircle(x, y, r, paint);
                }
            }
        }
    }
}

/// <summary>Mirrored wedge symmetry around the center.</summary>
public sealed class KaleidoscopeStyle : ArtStyle
{
    public override string Id => "kaleidoscope";
    public override string Name => "Kaleidoscope";
    public override StyleCategory Category => StyleCategory.Geometric;

    public override void Render(RenderContext ctx)
    {
        using (var bg = SKShader.CreateRadialGradient(ctx.Center, ctx.Max * 0.75f,
                   [ColorMath.Lighten(ctx.Palette.Background, 0.08f), ctx.Palette.Background], null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = bg })
        {
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }

        var segments = ctx.Pick(new[] { 6, 8, 10, 12, 16 });
        var wedge = 360f / segments;
        var radius = ctx.Max * 0.75f;
        var shapes = ctx.Count(25, 70);
        // Shapes in polar coordinates inside the first wedge.
        var items = Enumerable.Range(0, shapes).Select(_ => (
            Kind: ctx.Ri(0, 4),
            Dist: MathF.Pow(ctx.R01(), 0.8f) * radius,
            Angle: ctx.R(0, wedge),
            Size: ctx.U * ctx.R(30, 170),
            Rotation: ctx.R(0, 360),
            Color: ctx.Color().WithAlpha((byte)ctx.Ri(160, 256)))).ToList();

        using var clip = new SKPath();
        clip.MoveTo(0, 0);
        clip.ArcTo(new SKRect(-radius * 1.5f, -radius * 1.5f, radius * 1.5f, radius * 1.5f), 0, wedge, false);
        clip.Close();

        for (var k = 0; k < segments; k++)
        {
            ctx.Canvas.Save();
            ctx.Canvas.Translate(ctx.Center.X, ctx.Center.Y);
            if (k % 2 == 0)
            {
                ctx.Canvas.RotateDegrees(k * wedge);
            }
            else
            {
                ctx.Canvas.RotateDegrees((k + 1) * wedge);
                ctx.Canvas.Scale(1, -1);
            }
            ctx.Canvas.ClipPath(clip, antialias: true);
            foreach (var item in items)
            {
                var a = item.Angle * MathF.PI / 180f;
                var x = MathF.Cos(a) * item.Dist;
                var y = MathF.Sin(a) * item.Dist;
                ctx.Canvas.Save();
                ctx.Canvas.Translate(x, y);
                ctx.Canvas.RotateDegrees(item.Rotation);
                using var paint = ctx.Fill(item.Color);
                switch (item.Kind)
                {
                    case 0:
                        ctx.Canvas.DrawCircle(0, 0, item.Size / 2, paint);
                        break;
                    case 1:
                        ctx.Canvas.DrawRect(-item.Size / 2, -item.Size / 6, item.Size, item.Size / 3, paint);
                        break;
                    case 2:
                    {
                        using var path = new SKPath();
                        path.MoveTo(0, -item.Size / 2);
                        path.LineTo(item.Size / 2, item.Size / 2);
                        path.LineTo(-item.Size / 2, item.Size / 2);
                        path.Close();
                        ctx.Canvas.DrawPath(path, paint);
                        break;
                    }
                    default:
                    {
                        using var stroke = ctx.Stroke(item.Color, item.Size * 0.08f);
                        ctx.Canvas.DrawCircle(0, 0, item.Size / 2, stroke);
                        break;
                    }
                }
                ctx.Canvas.Restore();
            }
            ctx.Canvas.Restore();
        }
    }
}

/// <summary>Op art: moiré rings, warped stripes and bulging checkerboards.</summary>
public sealed class OpArtStyle : ArtStyle
{
    private const string Sksl = """
        uniform float variant;
        uniform float freq;
        uniform float2 c1;
        uniform float2 c2;
        uniform float warp;
        uniform float4 colA;
        uniform float4 colB;

        half4 main(float2 c) {
            float2 p = uv(c);
            float aa = freq * 1.5 / min(res.x, res.y);
            float v;
            if (variant < 0.5) {
                float s1 = sin(length(p - c1) * freq);
                float s2 = sin(length(p - c2) * freq);
                v = s1 * s2;
                aa *= 2.0;
            } else if (variant < 1.5) {
                float y = p.y + warp * sin(p.x * 6.0 + p.y * 2.0) * 0.08 + warp * 0.05 * sin(p.x * 17.0);
                v = sin(y * freq);
            } else {
                float r = length(p - c1);
                float2 q = p * (1.0 - warp * 0.3 * exp(-r * r * 6.0));
                float tx = smoothstep(-aa, aa, sin(q.x * freq * 0.5));
                float ty = smoothstep(-aa, aa, sin(q.y * freq * 0.5));
                v = (tx * ty + (1.0 - tx) * (1.0 - ty)) * 2.0 - 1.0;
            }
            float t = smoothstep(-aa, aa, v);
            return half4(mix(colA, colB, t));
        }
        """;

    public override string Id => "op-art";
    public override string Name => "Op Art";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var builder = Shaders.Create(Id, Sksl, ctx);
        var variant = ctx.Ri(0, 3);
        builder.Uniforms["variant"] = (float)variant;
        builder.Uniforms["freq"] = ctx.R(60, 140) * (0.6f + ctx.Detail * 0.8f);
        builder.Uniforms["c1"] = new[] { ctx.R(-0.3f, 0.3f), ctx.R(-0.3f, 0.3f) };
        builder.Uniforms["c2"] = new[] { ctx.R(-0.3f, 0.3f), ctx.R(-0.3f, 0.3f) };
        builder.Uniforms["warp"] = ctx.R(0.5f, 1.5f);
        SKColor a, b;
        if (ctx.Chance(0.4))
        {
            a = SKColor.Parse("#111111");
            b = SKColor.Parse("#F2F0EA");
        }
        else
        {
            a = ctx.Palette.Background;
            b = ctx.Palette.Colors.OrderByDescending(x => MathF.Abs(ColorMath.Luminance(x) - ColorMath.Luminance(a))).First();
        }
        builder.Uniforms["colA"] = new[] { a.Red / 255f, a.Green / 255f, a.Blue / 255f, 1f };
        builder.Uniforms["colB"] = new[] { b.Red / 255f, b.Green / 255f, b.Blue / 255f, 1f };
        Shaders.Fill(ctx, builder);
    }
}
