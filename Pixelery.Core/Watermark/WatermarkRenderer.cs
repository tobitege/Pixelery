using System.Collections.Concurrent;
using System.Globalization;
using Pixelery.Core.Generation;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Pixelery.Core.Watermark;

/// <summary>
/// Draws watermark layers on top of an image. Text is shaped with HarfBuzz (right-to-left scripts and ligatures work);
/// each layer is recorded once as a picture and then placed once, in a corner or tiled across the image.
/// Thread-safe: typefaces and watermark images are cached and immutable.
/// </summary>
public static class WatermarkRenderer
{
    private const int RandomSalt = 0x3C6EF372;

    private static readonly ConcurrentDictionary<string, SKTypeface> Typefaces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, SKImage? Image)> Images = new(StringComparer.OrdinalIgnoreCase);

    private sealed record Item(SKPicture Picture, float Width, float Height) : IDisposable
    {
        public void Dispose() => Picture.Dispose();
    }

    /// <summary>Returns a new image with the watermark drawn on it, or <paramref name="image"/> itself when nothing is active.</summary>
    public static SKImage Apply(SKImage image, WatermarkSettings settings, NameValues? values, int seed)
    {
        if (!settings.IsActive)
            return image;
        using var surface = SKSurface.Create(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Could not allocate the watermark surface.");
        surface.Canvas.DrawImage(image, 0, 0);
        Draw(surface.Canvas, image.Width, image.Height, settings, values, seed);
        return surface.Snapshot();
    }

    public static void Draw(SKCanvas canvas, int width, int height, WatermarkSettings settings, NameValues? values, int seed)
    {
        if (!settings.IsActive)
            return;
        var rng = new Random(seed ^ RandomSalt);
        foreach (var layer in settings.Layers)
        {
            if (!layer.Enabled)
                continue;
            using var item = layer.Kind == WatermarkLayerKind.Text
                ? RecordText(layer, width, height, values, rng)
                : RecordImage(layer, width, height);
            if (item != null)
                Place(canvas, width, height, layer, item, rng);
        }
    }

    /// <summary>True when the file exists and can be decoded.</summary>
    public static bool CanLoadImage(string? path) => !string.IsNullOrWhiteSpace(path) && LoadImage(path) != null;

    public static SKColor ParseColor(string? hex, SKColor fallback) =>
        !string.IsNullOrWhiteSpace(hex) && SKColor.TryParse(hex, out var color) ? color : fallback;

    #region Text

    private static Item? RecordText(WatermarkLayer layer, int width, int height, NameValues? values, Random rng)
    {
        var text = values != null ? NameTemplate.FormatText(layer.Text ?? string.Empty, values, rng) : layer.Text ?? string.Empty;
        if (layer.Uppercase)
            text = text.ToUpper(CultureInfo.CurrentCulture);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.All(string.IsNullOrWhiteSpace))
            return null;

        var min = Math.Min(width, height);
        var fontSize = (float)Math.Max(4, min * layer.Size / 100);
        var typeface = GetTypeface(layer.FontFamily, layer.Bold, layer.Italic);
        using var font = new SKFont(typeface, fontSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
        using var shaper = new SKShaper(typeface);
        var letterSpacing = (float)(fontSize * layer.LetterSpacing / 100);

        var shaped = new List<(ushort[] Glyphs, SKPoint[] Points, float Width)>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                shaped.Add(([], [], 0));
                continue;
            }
            var result = shaper.Shape(line, font);
            var glyphs = result.Codepoints.Select(c => (ushort)c).ToArray();
            var points = result.Points.Select((p, i) => new SKPoint(p.X + i * letterSpacing, 0)).ToArray();
            shaped.Add((glyphs, points, result.Width + letterSpacing * Math.Max(0, glyphs.Length - 1)));
        }

        var lineHeight = font.Spacing;
        var ascent = -font.Metrics.Ascent;
        var blockWidth = shaped.Max(s => s.Width);
        var blockHeight = lineHeight * (lines.Length - 1) + ascent + font.Metrics.Descent;
        var padding = layer.Plate ? (float)(fontSize * layer.PlatePadding / 100) : 0;
        var totalWidth = blockWidth + padding * 2;
        var totalHeight = blockHeight + padding * 2;
        var align = Column(layer);

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, totalWidth, totalHeight));

        if (layer.Plate)
        {
            var radius = (float)(totalHeight * Math.Clamp(layer.PlateRadius, 0, 50) / 100);
            using var plate = new SKPaint { IsAntialias = true, Color = ParseColor(layer.PlateColor, SKColors.Black.WithAlpha(128)) };
            canvas.DrawRoundRect(new SKRect(0, 0, totalWidth, totalHeight), radius, radius, plate);
        }

        var blobs = new List<SKTextBlob>();
        for (var i = 0; i < shaped.Count; i++)
        {
            var (glyphs, points, lineWidth) = shaped[i];
            if (glyphs.Length == 0)
                continue;
            var x = padding + align switch
            {
                0 => 0,
                1 => (blockWidth - lineWidth) / 2,
                _ => blockWidth - lineWidth
            };
            var y = padding + ascent + i * lineHeight;
            using var builder = new SKTextBlobBuilder();
            builder.AddPositionedRun(glyphs, font, [.. points.Select(p => new SKPoint(p.X + x, y))]);
            if (builder.Build() is { } blob)
                blobs.Add(blob);
        }

        try
        {
            if (layer.Shadow)
            {
                var size = (float)(fontSize * layer.ShadowSize / 100);
                using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, size * 0.6f));
                using var shadow = new SKPaint { IsAntialias = true, Color = ParseColor(layer.ShadowColor, SKColors.Black.WithAlpha(150)), MaskFilter = blur };
                foreach (var blob in blobs)
                    canvas.DrawText(blob, size * 0.35f, size * 0.45f, shadow);
            }
            if (layer.Outline)
            {
                using var stroke = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeJoin = SKStrokeJoin.Round,
                    StrokeWidth = (float)(fontSize * layer.OutlineWidth / 100 * 2),
                    Color = ParseColor(layer.OutlineColor, SKColors.Black)
                };
                foreach (var blob in blobs)
                    canvas.DrawText(blob, 0, 0, stroke);
            }
            using var fill = new SKPaint { IsAntialias = true, Color = ParseColor(layer.Color, SKColors.White) };
            foreach (var blob in blobs)
                canvas.DrawText(blob, 0, 0, fill);
        }
        finally
        {
            foreach (var blob in blobs)
                blob.Dispose();
        }

        return new Item(recorder.EndRecording(), totalWidth, totalHeight);
    }

    private static SKTypeface GetTypeface(string? family, bool bold, bool italic)
    {
        var key = $"{family}|{bold}|{italic}";
        return Typefaces.GetOrAdd(key, _ => SKTypeface.FromFamilyName(
            string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family,
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default);
    }

    /// <summary>Installed font families, sorted, for font pickers.</summary>
    public static IReadOnlyList<string> FontFamilies() =>
        [.. SKFontManager.Default.GetFontFamilies().Where(f => !string.IsNullOrWhiteSpace(f) && !f.StartsWith('@')).Distinct().Order(StringComparer.CurrentCultureIgnoreCase)];

    #endregion

    #region Image

    private static Item? RecordImage(WatermarkLayer layer, int width, int height)
    {
        var image = LoadImage(layer.ImagePath);
        if (image == null)
            return null;
        var targetWidth = (float)Math.Max(4, width * layer.ImageWidth / 100);
        var targetHeight = targetWidth * image.Height / image.Width;

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, targetWidth, targetHeight));
        using var paint = new SKPaint { IsAntialias = true };
        if (layer.Tint)
        {
            paint.ColorFilter = SKColorFilter.CreateBlendMode(ParseColor(layer.TintColor, SKColors.White), SKBlendMode.SrcIn);
        }
        else if (layer.Grayscale)
        {
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(
            [
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0, 0, 0, 1, 0
            ]);
        }
        if (layer.Shadow)
        {
            var size = (float)(targetWidth * layer.ShadowSize / 100 * 0.25);
            paint.ImageFilter = SKImageFilter.CreateDropShadow(size * 0.35f, size * 0.45f, size * 0.6f, size * 0.6f,
                ParseColor(layer.ShadowColor, SKColors.Black.WithAlpha(150)));
        }
        canvas.DrawImage(image, new SKRect(0, 0, targetWidth, targetHeight), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        return new Item(recorder.EndRecording(), targetWidth, targetHeight);
    }

    /// <summary>Decodes the file once (reloaded when it changes on disk).</summary>
    private static SKImage? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Images.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Image;
            var encoded = SKImage.FromEncodedData(File.ReadAllBytes(path));
            var raster = encoded?.ToRasterImage();
            // ToRasterImage may return the same instance; only dispose a separate lazy image.
            if (encoded != null && !ReferenceEquals(encoded, raster))
                encoded.Dispose();
            Images[path] = (stamp, raster);
            return raster;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    #endregion

    #region Placement

    /// <summary>0 left, 1 center, 2 right (text lines align the same way).</summary>
    private static int Column(WatermarkLayer layer) => layer.Placement == WatermarkPlacement.Tiled ? 1 : (int)layer.Anchor % 3;

    private static void Place(SKCanvas canvas, int width, int height, WatermarkLayer layer, Item item, Random rng)
    {
        using var layerPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity / 100 * 255, 0, 255)),
            BlendMode = layer.Blend switch
            {
                WatermarkBlend.Multiply => SKBlendMode.Multiply,
                WatermarkBlend.Screen => SKBlendMode.Screen,
                WatermarkBlend.Overlay => SKBlendMode.Overlay,
                WatermarkBlend.SoftLight => SKBlendMode.SoftLight,
                WatermarkBlend.Difference => SKBlendMode.Difference,
                _ => SKBlendMode.SrcOver
            }
        };
        canvas.SaveLayer(layerPaint);
        var rotation = (float)layer.Rotation;

        if (layer.Placement == WatermarkPlacement.Tiled)
        {
            var gap = (float)(layer.TileSpacing / 100);
            var stepX = item.Width * (1 + gap);
            var stepY = Math.Max(item.Height, item.Width * 0.25f) * (1 + gap);
            var extent = MathF.Sqrt(width * width + height * height) / 2 + Math.Max(stepX, stepY);
            canvas.Save();
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees(rotation);
            var row = 0;
            for (var y = -extent; y <= extent; y += stepY, row++)
            {
                var shift = row % 2 == 0 ? 0 : stepX / 2;
                for (var x = -extent + shift; x <= extent; x += stepX)
                {
                    canvas.Save();
                    canvas.Translate(x - item.Width / 2, y - item.Height / 2);
                    canvas.DrawPicture(item.Picture);
                    canvas.Restore();
                }
            }
            canvas.Restore();
        }
        else
        {
            var anchor = layer.Placement == WatermarkPlacement.RandomCorner
                ? new[] { WatermarkAnchor.TopLeft, WatermarkAnchor.TopRight, WatermarkAnchor.BottomLeft, WatermarkAnchor.BottomRight }[rng.Next(4)]
                : layer.Anchor;
            var margin = (float)(Math.Min(width, height) * layer.Margin / 100);
            // Keep the rotated item inside the margins.
            var rad = rotation * MathF.PI / 180f;
            var boxWidth = MathF.Abs(item.Width * MathF.Cos(rad)) + MathF.Abs(item.Height * MathF.Sin(rad));
            var boxHeight = MathF.Abs(item.Width * MathF.Sin(rad)) + MathF.Abs(item.Height * MathF.Cos(rad));
            var cx = ((int)anchor % 3) switch
            {
                0 => margin + boxWidth / 2,
                1 => width / 2f,
                _ => width - margin - boxWidth / 2
            };
            var cy = ((int)anchor / 3) switch
            {
                0 => margin + boxHeight / 2,
                1 => height / 2f,
                _ => height - margin - boxHeight / 2
            };
            canvas.Save();
            canvas.Translate(cx, cy);
            canvas.RotateDegrees(rotation);
            canvas.Translate(-item.Width / 2, -item.Height / 2);
            canvas.DrawPicture(item.Picture);
            canvas.Restore();
        }
        canvas.Restore();
    }

    #endregion
}
