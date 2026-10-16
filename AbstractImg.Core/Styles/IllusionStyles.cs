using AbstractImg.Core.Palettes;
using AbstractImg.Core.Rendering;
using SkiaSharp;

namespace AbstractImg.Core.Styles;

/// <summary>Shared color choices for illusions, which only work with strong luminance contrast.</summary>
internal static class IllusionColors
{
    /// <summary>A dark and a light color with clear contrast: often black/white, otherwise from the palette.</summary>
    public static (SKColor Dark, SKColor Light) ContrastPair(RenderContext ctx, double classicChance = 0.4)
    {
        if (ctx.Chance(classicChance))
            return (SKColor.Parse("#111111"), SKColor.Parse("#F4F2EC"));
        var all = ctx.Palette.WithBackground().OrderBy(ColorMath.Luminance).ToList();
        var dark = ColorMath.Darken(all[0], 0.08f);
        var light = ColorMath.Lighten(all[^1], 0.05f);
        if (ColorMath.Luminance(light) - ColorMath.Luminance(dark) < 0.45f)
        {
            dark = ColorMath.FromOkLch(0.18f, 0.04f, ColorMath.ToOkLch(all[0]).H);
            light = ColorMath.FromOkLch(0.95f, 0.03f, ColorMath.ToOkLch(all[^1]).H);
        }
        return (dark, light);
    }

    /// <summary>A color between <paramref name="dark"/> and <paramref name="light"/>.</summary>
    public static SKColor Between(SKColor dark, SKColor light, float t = 0.5f) => ColorMath.Mix(dark, light, t);
}

/// <summary>Peripheral drift (after Akiyoshi Kitaoka): rings of graded segments that seem to rotate.</summary>
public sealed class RotatingSnakesStyle : ArtStyle
{
    public override string Id => "rotating-snakes";
    public override string Name => "Rotating Snakes";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        // The drift needs the luminance order black → dark → white → light.
        SKColor dark, light;
        if (ctx.Chance(0.4))
        {
            dark = SKColor.Parse("#2E4FB8");
            light = SKColor.Parse("#F2D43A");
        }
        else
        {
            var sorted = ctx.Palette.Colors.OrderBy(ColorMath.Luminance).ToList();
            dark = ColorMath.FromOkLch(0.42f, Math.Max(0.12f, ColorMath.ToOkLch(sorted[0]).C), ColorMath.ToOkLch(sorted[0]).H);
            light = ColorMath.FromOkLch(0.86f, Math.Max(0.12f, ColorMath.ToOkLch(sorted[^1]).C), ColorMath.ToOkLch(sorted[^1]).H);
        }
        var black = SKColor.Parse("#101010");
        var white = SKColor.Parse("#FAFAFA");
        SKColor[] sequence = [black, dark, white, light];
        float[] widths = [0.14f, 0.36f, 0.14f, 0.36f];

        var bg = ctx.Chance(0.5) ? ColorMath.Mix(dark, light, 0.5f) : ColorMath.FromOkLch(0.55f, 0.02f, 90);
        ctx.Clear(bg);

        var discRadius = ctx.Min / ctx.Count(2, 5) / 2f;
        var spacingX = discRadius * 2.02f;
        var spacingY = discRadius * 1.76f;
        var rings = ctx.Ri(4, 7);
        var segments = ctx.Pick(new[] { 24, 28, 32, 36 });
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        var row = 0;
        for (var cy = -discRadius; cy < ctx.Height + discRadius; cy += spacingY, row++)
        {
            var col = 0;
            for (var cx = (row % 2) * spacingX / 2 - discRadius; cx < ctx.Width + discRadius; cx += spacingX, col++)
            {
                // Neighbours turn in opposite directions.
                var clockwise = (row + col) % 2 == 0;
                for (var r = 0; r < rings; r++)
                {
                    var outer = discRadius * (1 - r / (float)rings);
                    var inner = discRadius * (1 - (r + 1) / (float)rings) + discRadius * 0.02f;
                    if (inner < discRadius * 0.12f)
                        inner = discRadius * 0.12f;
                    var step = 360f / segments * 4;
                    var ringOffset = r % 2 == 0 ? 0 : step / 2;
                    for (var s = 0; s < segments / 4; s++)
                    {
                        var start = s * step + ringOffset;
                        for (var k = 0; k < 4; k++)
                        {
                            var index = clockwise ? k : 3 - k;
                            var sweep = step * widths[index];
                            paint.Color = sequence[index];
                            using var path = Sector(cx, cy, inner, outer, start, sweep);
                            ctx.Canvas.DrawPath(path, paint);
                            start += sweep;
                        }
                    }
                }
                paint.Color = ColorMath.Mix(dark, black, 0.3f);
                ctx.Canvas.DrawCircle(cx, cy, discRadius * 0.12f, paint);
            }
        }
    }

    private static SKPath Sector(float cx, float cy, float inner, float outer, float startDeg, float sweepDeg)
    {
        var path = new SKPath();
        path.ArcTo(new SKRect(cx - outer, cy - outer, cx + outer, cy + outer), startDeg, sweepDeg, true);
        path.ArcTo(new SKRect(cx - inner, cy - inner, cx + inner, cy + inner), startDeg + sweepDeg, -sweepDeg, false);
        path.Close();
        return path;
    }
}

/// <summary>Café wall: rows of shifted tiles separated by gray mortar look wedge-shaped although all lines are parallel.</summary>
public sealed class CafeWallStyle : ArtStyle
{
    public override string Id => "cafe-wall";
    public override string Name => "Café Wall";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var (dark, light) = IllusionColors.ContrastPair(ctx);
        var mortar = IllusionColors.Between(dark, light, ctx.R(0.4f, 0.6f));
        ctx.Clear(mortar);

        var rows = ctx.Count(8, 20);
        var rowHeight = ctx.Height / (float)rows;
        var line = Math.Max(1f, rowHeight * ctx.R(0.05f, 0.1f));
        var tile = rowHeight * ctx.R(1.4f, 2.2f);
        float[] shifts = [0, 1 / 3f, 2 / 3f, 1 / 3f];
        using var darkPaint = ctx.Fill(dark);
        using var lightPaint = ctx.Fill(light);
        darkPaint.IsAntialias = false;
        lightPaint.IsAntialias = false;

        for (var r = 0; r < rows; r++)
        {
            var top = r * rowHeight + line / 2;
            var height = rowHeight - line;
            var offset = shifts[r % shifts.Length] * tile - tile * 2;
            var index = 0;
            for (var x = offset; x < ctx.Width; x += tile, index++)
                ctx.Canvas.DrawRect(x, top, tile, height, index % 2 == 0 ? darkPaint : lightPaint);
        }
    }
}

/// <summary>Scintillating grid: white dots at the crossings of gray bars flash dark; or the classic Hermann grid.</summary>
public sealed class ScintillatingGridStyle : ArtStyle
{
    public override string Id => "scintillating-grid";
    public override string Name => "Scintillating Grid";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var hermann = ctx.Chance(0.3);
        var cells = ctx.Count(8, 22);
        var cell = ctx.Min / cells;
        var bar = cell * ctx.R(0.12f, 0.18f);
        var hue = ColorMath.ToOkLch(ctx.Color()).H;
        var background = hermann ? SKColor.Parse("#0D0D0D") : ColorMath.FromOkLch(ctx.R(0.08f, 0.16f), ctx.R(0f, 0.06f), hue);
        var barColor = hermann ? SKColor.Parse("#F2F2F2") : ColorMath.FromOkLch(ctx.R(0.5f, 0.62f), ctx.R(0f, 0.08f), hue);
        ctx.Clear(background);

        var offsetX = (ctx.Width % cell) / 2;
        var offsetY = (ctx.Height % cell) / 2;
        using var barPaint = ctx.Fill(barColor);
        for (var x = offsetX; x <= ctx.Width; x += cell)
            ctx.Canvas.DrawRect(x - bar / 2, 0, bar, ctx.Height, barPaint);
        for (var y = offsetY; y <= ctx.Height; y += cell)
            ctx.Canvas.DrawRect(0, y - bar / 2, ctx.Width, bar, barPaint);

        if (hermann)
            return;
        using var dot = ctx.Fill(SKColors.White);
        var radius = bar * ctx.R(0.6f, 0.75f);
        for (var x = offsetX; x <= ctx.Width; x += cell)
            for (var y = offsetY; y <= ctx.Height; y += cell)
                ctx.Canvas.DrawCircle(x, y, radius, dot);
    }
}

/// <summary>Fraser spiral: concentric circles of twisted cord on a checkered ground appear to spiral inward.</summary>
public sealed class FraserSpiralStyle : ArtStyle
{
    public override string Id => "fraser-spiral";
    public override string Name => "Fraser Spiral";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var groundA = ctx.Palette.Colors.Count > 1 ? ctx.Palette[0] : SKColor.Parse("#4A7FC1");
        var groundB = ctx.Palette.PickOther(ctx.Rng, groundA);
        if (ctx.Chance(0.35))
        {
            groundA = SKColor.Parse("#3D7BC6");
            groundB = SKColor.Parse("#5BB55B");
        }
        // Keep the ground in the middle of the luminance range so the black/white cord dominates.
        groundA = ColorMath.FromOkLch(0.58f, ColorMath.ToOkLch(groundA).C, ColorMath.ToOkLch(groundA).H);
        groundB = ColorMath.FromOkLch(0.66f, ColorMath.ToOkLch(groundB).C, ColorMath.ToOkLch(groundB).H);

        var center = new SKPoint(ctx.Width / 2f + ctx.R(-0.1f, 0.1f) * ctx.Width, ctx.Height / 2f + ctx.R(-0.1f, 0.1f) * ctx.Height);
        var maxRadius = MathF.Sqrt(ctx.Width * ctx.Width + ctx.Height * ctx.Height) * 0.6f;
        var ringStep = ctx.Min / ctx.Count(8, 18);
        var sectors = ctx.Pick(new[] { 24, 32, 40 });

        // Radial checkerboard ground.
        using (var paint = new SKPaint { IsAntialias = true })
        {
            ctx.Clear(groundA);
            paint.Color = groundB;
            for (var r = 0; r * ringStep < maxRadius; r++)
            {
                for (var s = 0; s < sectors; s++)
                {
                    if ((r + s) % 2 == 0)
                        continue;
                    using var path = new SKPath();
                    var outer = (r + 1) * ringStep;
                    var inner = r * ringStep;
                    var start = s * 360f / sectors;
                    path.ArcTo(new SKRect(center.X - outer, center.Y - outer, center.X + outer, center.Y + outer), start, 360f / sectors, true);
                    if (inner > 0)
                        path.ArcTo(new SKRect(center.X - inner, center.Y - inner, center.X + inner, center.Y + inner), start + 360f / sectors, -360f / sectors, false);
                    else
                        path.LineTo(center);
                    path.Close();
                    ctx.Canvas.DrawPath(path, paint);
                }
            }
        }

        // Twisted cords: tilted dashes alternating black and white along each circle.
        var tilt = ctx.R(18, 28) * (ctx.Chance(0.5) ? 1 : -1);
        var cordWidth = ringStep * 0.28f;
        using var black = ctx.Fill(SKColor.Parse("#111111"));
        using var white = ctx.Fill(SKColor.Parse("#F7F7F7"));
        for (var radius = ringStep * 1.5f; radius < maxRadius; radius += ringStep)
        {
            var dashLength = cordWidth * 1.6f;
            var count = Math.Max(12, (int)(MathF.Tau * radius / dashLength));
            if (count % 2 == 1)
                count++;
            for (var k = 0; k < count; k++)
            {
                var angle = k * 360f / count;
                var rad = angle * MathF.PI / 180f;
                var x = center.X + MathF.Cos(rad) * radius;
                var y = center.Y + MathF.Sin(rad) * radius;
                ctx.Canvas.Save();
                ctx.Canvas.Translate(x, y);
                ctx.Canvas.RotateDegrees(angle + 90 + tilt);
                ctx.Canvas.DrawRect(-dashLength * 0.56f, -cordWidth / 2, dashLength * 1.12f, cordWidth, k % 2 == 0 ? black : white);
                ctx.Canvas.Restore();
            }
        }
    }
}

/// <summary>Zöllner illusion: parallel lines with alternating short hatches look tilted against each other.</summary>
public sealed class ZoellnerStyle : ArtStyle
{
    public override string Id => "zoellner";
    public override string Name => "Zöllner";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var (dark, light) = IllusionColors.ContrastPair(ctx, 0.5);
        var inverted = ctx.Chance(0.3);
        var background = inverted ? dark : light;
        var ink = inverted ? light : dark;
        ctx.Clear(background);

        var lineAngle = ctx.Pick(new[] { 45f, -45f, 0f, 90f, 30f });
        var spacing = ctx.Min / ctx.Count(6, 14);
        var hatchSpacing = spacing * ctx.R(0.22f, 0.32f);
        var hatchLength = spacing * ctx.R(0.45f, 0.6f);
        var hatchAngle = ctx.R(30, 45);
        var width = Math.Max(1.5f, ctx.U * ctx.R(3, 6));
        var extent = MathF.Sqrt(ctx.Width * ctx.Width + ctx.Height * ctx.Height);
        using var line = ctx.Stroke(ink, width);
        using var hatch = ctx.Stroke(ink, width * 0.75f);

        ctx.Canvas.Save();
        ctx.Canvas.Translate(ctx.Width / 2f, ctx.Height / 2f);
        ctx.Canvas.RotateDegrees(lineAngle);
        var index = 0;
        for (var y = -extent / 2; y <= extent / 2; y += spacing, index++)
        {
            ctx.Canvas.DrawLine(-extent / 2, y, extent / 2, y, line);
            var a = (index % 2 == 0 ? hatchAngle : -hatchAngle) * MathF.PI / 180f;
            var dx = MathF.Cos(a) * hatchLength / 2;
            var dy = MathF.Sin(a) * hatchLength / 2;
            for (var x = -extent / 2; x <= extent / 2; x += hatchSpacing)
                ctx.Canvas.DrawLine(x - dx, y - dy, x + dx, y + dy, hatch);
        }
        ctx.Canvas.Restore();
    }
}

/// <summary>Ouchi illusion: discs of vertical checks inside a field of horizontal checks seem to float and slide.</summary>
public sealed class OuchiStyle : ArtStyle
{
    public override string Id => "ouchi";
    public override string Name => "Ouchi";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var (dark, light) = IllusionColors.ContrastPair(ctx, 0.5);
        var cellShort = ctx.Min / ctx.Count(40, 90);
        var cellLong = cellShort * ctx.R(3.5f, 4.5f);
        var angle = ctx.Pick(new[] { 0f, 45f, 30f });

        DrawChecks(ctx, angle, cellLong, cellShort, dark, light);

        var discs = ctx.Chance(0.5) ? 1 : ctx.Ri(2, 6);
        for (var i = 0; i < discs; i++)
        {
            var radius = discs == 1 ? ctx.Min * ctx.R(0.2f, 0.32f) : ctx.Min * ctx.R(0.1f, 0.2f);
            var x = discs == 1 ? ctx.Width / 2f : ctx.R(radius, ctx.Width - radius);
            var y = discs == 1 ? ctx.Height / 2f : ctx.R(radius, ctx.Height - radius);
            using var clip = new SKPath();
            clip.AddCircle(x, y, radius);
            ctx.Canvas.Save();
            ctx.Canvas.ClipPath(clip, antialias: true);
            DrawChecks(ctx, angle + 90, cellLong, cellShort, dark, light);
            ctx.Canvas.Restore();
        }
    }

    private static void DrawChecks(RenderContext ctx, float angle, float cellLong, float cellShort, SKColor dark, SKColor light)
    {
        var extent = MathF.Sqrt(ctx.Width * ctx.Width + ctx.Height * ctx.Height);
        using var lightPaint = ctx.Fill(light);
        using var darkPaint = ctx.Fill(dark);
        ctx.Canvas.Save();
        ctx.Canvas.Translate(ctx.Width / 2f, ctx.Height / 2f);
        ctx.Canvas.RotateDegrees(angle);
        ctx.Canvas.DrawRect(-extent, -extent, extent * 2, extent * 2, lightPaint);
        var rows = (int)(extent / cellShort) + 2;
        var cols = (int)(extent / cellLong) + 2;
        for (var r = -rows; r <= rows; r++)
            for (var c = -cols; c <= cols; c++)
                if (((r + c) & 1) == 0)
                    ctx.Canvas.DrawRect(c * cellLong, r * cellShort, cellLong, cellShort, darkPaint);
        ctx.Canvas.Restore();
    }
}

/// <summary>Kanizsa figures: "pac-man" inducers make triangles and polygons appear that are never drawn.</summary>
public sealed class KanizsaStyle : ArtStyle
{
    public override string Id => "kanizsa";
    public override string Name => "Kanizsa";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var (dark, light) = IllusionColors.ContrastPair(ctx, 0.5);
        var inverted = ctx.Chance(0.3);
        var background = inverted ? dark : light;
        ctx.Clear(background);

        var single = ctx.Chance(0.4);
        var cols = single ? 1 : ctx.Ri(2, 4);
        var rows = single ? 1 : Math.Max(1, (int)MathF.Round(cols * ctx.Height / (float)ctx.Width));
        var cellW = ctx.Width / (float)cols;
        var cellH = ctx.Height / (float)rows;
        var colorful = !inverted && ctx.Chance(0.4);

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var center = new SKPoint((c + 0.5f) * cellW, (r + 0.5f) * cellH);
                var radius = Math.Min(cellW, cellH) * ctx.R(0.26f, 0.32f);
                var sides = ctx.Pick(new[] { 3, 3, 4, 5, 6 });
                var rotation = ctx.R(0, 360);
                var inducer = colorful ? ctx.Color() : inverted ? light : dark;
                DrawFigure(ctx, center, radius, sides, rotation, inducer, background, ctx.Chance(0.5) && sides == 3);
            }
        }
    }

    private static void DrawFigure(RenderContext ctx, SKPoint center, float radius, int sides, float rotation, SKColor inducer, SKColor background, bool outline)
    {
        var vertices = Polygon(center, radius, sides, rotation);
        var inducerRadius = radius * MathF.Sin(MathF.PI / sides) * 0.62f;

        if (outline)
        {
            // The classic second triangle, drawn as an outline and interrupted by the illusory one.
            var outlinePoints = Polygon(center, radius * 1.05f, sides, rotation + 180f / sides);
            using var stroke = ctx.Stroke(inducer, Math.Max(1.5f, radius * 0.025f));
            stroke.StrokeJoin = SKStrokeJoin.Miter;
            using var path = new SKPath();
            path.AddPoly(outlinePoints, true);
            ctx.Canvas.DrawPath(path, stroke);
        }

        using var fill = ctx.Fill(inducer);
        foreach (var v in vertices)
            ctx.Canvas.DrawCircle(v, inducerRadius, fill);

        // Covering the polygon with the background turns the discs into pac-men and cuts the outline.
        using var cover = ctx.Fill(background);
        using var polygon = new SKPath();
        polygon.AddPoly(vertices, true);
        ctx.Canvas.DrawPath(polygon, cover);
    }

    private static SKPoint[] Polygon(SKPoint center, float radius, int sides, float rotation)
    {
        var points = new SKPoint[sides];
        for (var i = 0; i < sides; i++)
        {
            var a = (rotation + i * 360f / sides) * MathF.PI / 180f;
            points[i] = new SKPoint(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius);
        }
        return points;
    }
}

/// <summary>Impossible figures: Penrose tribars built from isometric cubes whose last joint is drawn behind the first.</summary>
public sealed class ImpossibleFiguresStyle : ArtStyle
{
    private static readonly float Cos30 = MathF.Sqrt(3) / 2;

    public override string Id => "impossible-figures";
    public override string Name => "Impossible Figures";
    public override StyleCategory Category => StyleCategory.Illusions;

    public override void Render(RenderContext ctx)
    {
        var bg = ctx.Palette.Background;
        using (var shader = SKShader.CreateRadialGradient(ctx.Center, ctx.Max * 0.75f, [ColorMath.Lighten(bg, 0.05f), ColorMath.Darken(bg, 0.05f)], null, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = shader, IsDither = true })
        {
            ctx.Canvas.DrawRect(ctx.Bounds, paint);
        }

        var single = ctx.Chance(0.4);
        var cols = single ? 1 : ctx.Ri(2, 4);
        var rows = single ? 1 : Math.Max(1, (int)MathF.Round(cols * ctx.Height / (float)ctx.Width));
        var cellW = ctx.Width / (float)cols;
        var cellH = ctx.Height / (float)rows;
        var outline = ctx.Chance(0.35);

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var n = ctx.Ri(3, 7);
                // The tribar spans about 1.5 n units wide and 1.7 n units high.
                var unit = Math.Min(cellW, cellH) * 0.75f / (n * 1.75f);
                var center = new SKPoint((c + 0.5f) * cellW, (r + 0.5f) * cellH);
                var color = ctx.Color();
                DrawTribar(ctx, center, unit, n, color, outline, ctx.R(-8, 8) + (ctx.Chance(0.5) ? 0 : 60));
            }
        }
    }

    private static void DrawTribar(RenderContext ctx, SKPoint center, float unit, int n, SKColor color, bool outline, float rotation)
    {
        // Path of cubes along +x, +y, +z; in isometric view n steps of each sum to zero, so the end meets the start.
        var cubes = new List<(int X, int Y, int Z)>();
        for (var i = 0; i < n; i++)
            cubes.Add((i, 0, 0));
        for (var j = 0; j < n; j++)
            cubes.Add((n, j, 0));
        for (var k = 0; k < n; k++)
            cubes.Add((n, n, k));

        var points = cubes.Select(p => Project(p.X, p.Y, p.Z, unit)).ToList();
        var mid = new SKPoint(points.Average(p => p.X), points.Average(p => p.Y));

        var top = ColorMath.Lighten(color, 0.14f);
        var left = color;
        var right = ColorMath.Darken(color, 0.16f);
        var edge = ColorMath.Darken(color, 0.35f);

        ctx.Canvas.Save();
        ctx.Canvas.Translate(center.X, center.Y);
        ctx.Canvas.RotateDegrees(rotation);
        ctx.Canvas.Translate(-mid.X, -mid.Y);
        for (var i = 0; i < points.Count; i++)
            DrawCube(ctx, points[i], unit, top, left, right, edge, outline);

        // The impossible joint: the first cube covers the last one where they overlap.
        using var clip = Hexagon(points[^1], unit);
        ctx.Canvas.Save();
        ctx.Canvas.ClipPath(clip, antialias: true);
        DrawCube(ctx, points[0], unit, top, left, right, edge, outline);
        ctx.Canvas.Restore();
        ctx.Canvas.Restore();
    }

    private static SKPoint Project(int x, int y, int z, float unit) =>
        new((x - y) * Cos30 * unit, ((x + y) * 0.5f - z) * unit);

    private static SKPath Hexagon(SKPoint p, float u)
    {
        var path = new SKPath();
        path.AddPoly(
        [
            new SKPoint(p.X, p.Y - u),
            new SKPoint(p.X + Cos30 * u, p.Y - u / 2),
            new SKPoint(p.X + Cos30 * u, p.Y + u / 2),
            new SKPoint(p.X, p.Y + u),
            new SKPoint(p.X - Cos30 * u, p.Y + u / 2),
            new SKPoint(p.X - Cos30 * u, p.Y - u / 2)
        ], true);
        return path;
    }

    private static void DrawCube(RenderContext ctx, SKPoint p, float u, SKColor top, SKColor left, SKColor right, SKColor edge, bool outline)
    {
        SKPoint c = p;
        SKPoint up = new(p.X, p.Y - u);
        SKPoint ul = new(p.X - Cos30 * u, p.Y - u / 2);
        SKPoint ur = new(p.X + Cos30 * u, p.Y - u / 2);
        SKPoint dl = new(p.X - Cos30 * u, p.Y + u / 2);
        SKPoint dr = new(p.X + Cos30 * u, p.Y + u / 2);
        SKPoint down = new(p.X, p.Y + u);
        Face([c, ul, up, ur], top);
        Face([c, ul, dl, down], left);
        Face([c, ur, dr, down], right);

        void Face(SKPoint[] pts, SKColor color)
        {
            using var path = new SKPath();
            path.AddPoly(pts, true);
            // A hairline in the face color hides seams between neighbouring cubes.
            using var fill = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 1f };
            ctx.Canvas.DrawPath(path, fill);
            if (outline)
            {
                using var stroke = ctx.Stroke(edge, Math.Max(1f, u * 0.04f));
                ctx.Canvas.DrawPath(path, stroke);
            }
        }
    }
}
