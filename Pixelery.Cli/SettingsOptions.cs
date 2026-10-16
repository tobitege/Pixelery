using Pixelery.Core.Generation;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Pixelery.Core.Watermark;

namespace Pixelery.Cli;

/// <summary>
/// Maps generator options to <see cref="GeneratorSettings"/>: start from defaults, a JSON file or the app's
/// saved configuration, then apply every option given on the command line.
/// </summary>
public static class SettingsOptions
{
    public static readonly Dictionary<string, string> Aliases = new()
    {
        ["o"] = "out",
        ["n"] = "count",
        ["j"] = "threads",
        ["s"] = "styles",
        ["c"] = "config",
        ["q"] = "quiet",
        ["h"] = "help"
    };

    public static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "app-config", "grain", "vignette", "exif", "overwrite", "spread-dates", "dry-run", "quiet", "json", "help", "watermark"
    };

    /// <summary>The app's generator.json (respects PIXELERY_STATE_ROOT like the app).</summary>
    public static string AppConfigPath => Path.Combine(Core.AppInfo.StateDirectory(), "generator.json");

    public static GeneratorSettings Build(ArgReader args)
    {
        var settings = LoadBase(args);

        if (args.Value("out") is { } outFolder)
            settings.OutputFolder = Path.GetFullPath(outFolder);
        if (args.Int("count", 1, 10_000_000) is { } count)
            settings.Count = count;
        if (args.Value("threads") is { } threads)
        {
            settings.Threads = threads.Equals("max", StringComparison.OrdinalIgnoreCase)
                ? Environment.ProcessorCount
                : args.Int("threads", 1, 1024)!.Value;
        }
        if (args.Value("seed") is { } seed)
            settings.Seed = seed;

        if (args.List("styles") is { } styles)
            settings.Styles = ResolveStyles(styles);
        if (args.List("exclude") is { } exclude)
        {
            var remove = ResolveStyles(exclude);
            settings.Styles = [.. settings.Styles.Where(s => !remove.Contains(s))];
        }

        if (args.List("palettes") is { } palettes)
            settings.Palettes = ParseEnums<PaletteFamily>(palettes, "palettes");
        if (args.List("tones") is { } tones)
            settings.Tones = ParseEnums<BackgroundTone>(tones, "tones");
        if (args.List("detail") is { } detail)
            settings.Details = ParseEnums<DetailLevel>(detail, "detail");
        if (args.Switch("grain") is { } grain)
            settings.Grain = grain;
        if (args.Switch("vignette") is { } vignette)
            settings.Vignette = vignette;

        // Size options replace the size configuration: --sizes alone drops the ratio × edge combinations,
        // --ratios or --edges alone drop the fixed sizes.
        var hasCombination = args.Has("ratios") || args.Has("edges");
        var hasFixed = args.Has("sizes");
        if (hasFixed && !hasCombination)
            settings.AspectRatios = [];
        if (hasCombination && !hasFixed)
            settings.FixedSizes = [];

        if (args.List("orientation") is { } orientation)
        {
            settings.Orientations = orientation.Count == 1 && orientation[0].Equals("both", StringComparison.OrdinalIgnoreCase)
                ? [ImageOrientation.Landscape, ImageOrientation.Portrait]
                : ParseEnums<ImageOrientation>(orientation, "orientation");
        }
        if (args.List("ratios") is { } ratios)
        {
            settings.AspectRatios = [];
            foreach (var text in IgnoreNone(ratios))
            {
                if (!SizeCatalog.TryParseRatio(text, out var w, out var h))
                    throw new UsageException($"--ratios: '{text}' is not an aspect ratio (examples: 16:9, 4:3, 1.85).");
                var value = SizeCatalog.NormalizeRatio(w, h);
                AddUnique(settings.AspectRatios, value);
                if (!SizeCatalog.AspectRatios.Contains(value))
                    AddUnique(settings.CustomAspectRatios, value);
            }
        }
        if (args.List("edges") is { } edges)
        {
            settings.LongEdges = [];
            foreach (var text in IgnoreNone(edges))
            {
                if (!SizeCatalog.TryParseEdge(text, out var edge))
                    throw new UsageException($"--edges: '{text}' is not a pixel length from {SizeCatalog.MinEdge} to {SizeCatalog.MaxEdge}.");
                AddUnique(settings.LongEdges, edge);
                if (!SizeCatalog.LongEdges.Contains(edge))
                    AddUnique(settings.CustomLongEdges, edge);
            }
        }
        if (args.List("sizes") is { } sizes)
        {
            settings.FixedSizes = [];
            foreach (var text in IgnoreNone(sizes))
            {
                var preset = SizeCatalog.FixedSizes.FirstOrDefault(p => p.Label.Replace(" ", string.Empty).Equals(text.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
                string value;
                if (preset.Label != null)
                    value = preset.Value;
                else if (SizeCatalog.TryParseSize(text, out var w, out var h))
                    value = $"{w}x{h}";
                else
                    throw new UsageException($"--sizes: '{text}' is neither WIDTHxHEIGHT nor a preset name (see 'list sizes').");
                AddUnique(settings.FixedSizes, value);
                if (SizeCatalog.FixedSizes.All(p => p.Value != value))
                    AddUnique(settings.CustomFixedSizes, value);
            }
        }

        if (args.List("formats") is { } formats)
            settings.Formats = ParseEnums<OutputFormat>(formats.Select(f => f.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpeg" : f), "formats");
        if (args.Int("quality", 1, 100) is { } quality)
            settings.Quality = quality;
        if (args.Value("name") is { } name)
            settings.NamePattern = name;
        if (args.Int("start", 0, int.MaxValue) is { } start)
            settings.StartIndex = start;
        if (args.Switch("overwrite") is { } overwrite)
            settings.Overwrite = overwrite;
        if (args.Switch("exif") is { } exif)
            settings.WriteExif = exif;
        if (args.Switch("spread-dates") is { } spread)
            settings.SpreadDates = spread;
        if (args.Int("spread-years", 1, 100) is { } years)
        {
            settings.SpreadYears = years;
            settings.SpreadDates = true;
        }

        ApplyWatermark(args, settings.Watermark);
        return settings;
    }

    /// <summary>
    /// Simple watermark options. --watermark-text and --watermark-image edit the first text or image layer (adding one if
    /// needed); the look options apply to those layers, or to all layers when neither is given. Everything else via --config.
    /// </summary>
    private static void ApplyWatermark(ArgReader args, WatermarkSettings watermark)
    {
        var targets = new List<WatermarkLayer>();
        if (args.Value("watermark-text") is { } text)
        {
            var layer = watermark.Layers.FirstOrDefault(l => l.Kind == WatermarkLayerKind.Text) ?? Add(WatermarkLayer.NewText());
            layer.Text = text.Replace("\\n", "\n");
            layer.Enabled = true;
            targets.Add(layer);
            watermark.Enabled = true;
        }
        if (args.Value("watermark-image") is { } image)
        {
            var path = Path.GetFullPath(image);
            if (!WatermarkRenderer.CanLoadImage(path))
                throw new UsageException($"--watermark-image: cannot read the image '{path}'.");
            var layer = watermark.Layers.FirstOrDefault(l => l.Kind == WatermarkLayerKind.Image) ?? Add(WatermarkLayer.NewImage());
            layer.ImagePath = path;
            layer.Enabled = true;
            targets.Add(layer);
            watermark.Enabled = true;
        }
        if (args.Switch("watermark") is { } enabled)
            watermark.Enabled = enabled;
        if (targets.Count == 0)
            targets = watermark.Layers;

        if (args.Value("watermark-position") is { } position)
        {
            var normalized = position.Replace("-", string.Empty).Replace("_", string.Empty);
            foreach (var layer in targets)
            {
                if (normalized.Equals("tiled", StringComparison.OrdinalIgnoreCase))
                    layer.Placement = WatermarkPlacement.Tiled;
                else if (normalized.Equals("random", StringComparison.OrdinalIgnoreCase) || normalized.Equals("randomcorner", StringComparison.OrdinalIgnoreCase))
                    layer.Placement = WatermarkPlacement.RandomCorner;
                else if (Enum.TryParse<WatermarkAnchor>(normalized, true, out var anchor) && Enum.IsDefined(anchor))
                {
                    layer.Placement = WatermarkPlacement.Single;
                    layer.Anchor = anchor;
                }
                else
                    throw new UsageException("--watermark-position expects top-left, top, top-right, left, center, right, bottom-left, bottom, bottom-right, tiled or random.");
            }
        }
        if (args.Value("watermark-size") is { } sizeText)
        {
            var size = ParseNumber(sizeText, "watermark-size", 0.5, 100);
            foreach (var layer in targets)
            {
                if (layer.Kind == WatermarkLayerKind.Text)
                    layer.Size = size;
                else
                    layer.ImageWidth = size;
            }
        }
        if (args.Value("watermark-opacity") is { } opacityText)
        {
            var opacity = ParseNumber(opacityText, "watermark-opacity", 0, 100);
            foreach (var layer in targets)
                layer.Opacity = opacity;
        }
        if (args.Value("watermark-rotation") is { } rotationText)
        {
            var rotation = ParseNumber(rotationText, "watermark-rotation", -180, 180);
            foreach (var layer in targets)
                layer.Rotation = rotation;
        }
        if (args.Value("watermark-color") is { } color)
        {
            if (!SkiaSharp.SKColor.TryParse(color, out var parsed))
                throw new UsageException($"--watermark-color: '{color}' is not a color (examples: #FFFFFF, #80FFFFFF).");
            foreach (var layer in targets.Where(l => l.Kind == WatermarkLayerKind.Text))
                layer.Color = $"#{parsed.Alpha:X2}{parsed.Red:X2}{parsed.Green:X2}{parsed.Blue:X2}";
        }
        if (args.Value("watermark-font") is { } font)
        {
            foreach (var layer in targets.Where(l => l.Kind == WatermarkLayerKind.Text))
                layer.FontFamily = font;
        }

        WatermarkLayer Add(WatermarkLayer layer)
        {
            watermark.Layers.Add(layer);
            return layer;
        }
    }

    private static double ParseNumber(string text, string option, double min, double max)
    {
        if (!double.TryParse(text.TrimEnd('%').Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            || value < min || value > max)
            throw new UsageException($"--{option} expects a number from {min} to {max}, got '{text}'.");
        return value;
    }

    private static GeneratorSettings LoadBase(ArgReader args)
    {
        var file = args.Value("config");
        var fromApp = args.Has("app-config");
        if (file != null && fromApp)
            throw new UsageException("Use either --config or --app-config, not both.");
        if (fromApp)
        {
            file = AppConfigPath;
            if (!File.Exists(file))
                throw new UsageException($"The app has not saved a configuration yet ({file}).");
        }
        if (file == null)
            return new GeneratorSettings();
        try
        {
            return SettingsJson.Load(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            throw new UsageException($"Cannot read settings from '{file}': {ex.Message}");
        }
    }

    /// <summary>Style ids, category names (all styles of that category) or "all".</summary>
    public static List<string> ResolveStyles(IEnumerable<string> items)
    {
        var result = new List<string>();
        foreach (var item in IgnoreNone(items))
        {
            if (item.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(StyleCatalog.All.Select(s => s.Id));
                continue;
            }
            if (Enum.TryParse<StyleCategory>(item, true, out var category))
            {
                result.AddRange(StyleCatalog.All.Where(s => s.Category == category).Select(s => s.Id));
                continue;
            }
            var style = StyleCatalog.Find(item) ?? StyleCatalog.All.FirstOrDefault(s => s.Name.Replace(" ", string.Empty).Equals(item.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
                ?? throw new UsageException($"Unknown style '{item}'. See 'list styles'.{Suggest(item, StyleCatalog.All.Select(s => s.Id))}");
            result.Add(style.Id);
        }
        return [.. result.Distinct()];
    }

    private static List<T> ParseEnums<T>(IEnumerable<string> items, string option) where T : struct, Enum
    {
        var result = new List<T>();
        foreach (var item in IgnoreNone(items))
        {
            if (item.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(Enum.GetValues<T>());
                continue;
            }
            if (!Enum.TryParse<T>(item, true, out var value) || !Enum.IsDefined(value))
                throw new UsageException($"--{option}: unknown value '{item}'. Allowed: {string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))}, all.");
            result.Add(value);
        }
        return [.. result.Distinct()];
    }

    /// <summary>"none" clears a list (e.g. --sizes none).</summary>
    private static IEnumerable<string> IgnoreNone(IEnumerable<string> items) =>
        items.Where(i => !i.Equals("none", StringComparison.OrdinalIgnoreCase));

    private static void AddUnique<T>(List<T> list, T value)
    {
        if (!list.Contains(value))
            list.Add(value);
    }

    private static string Suggest(string input, IEnumerable<string> candidates)
    {
        var best = candidates
            .Select(c => (Candidate: c, Score: c.Contains(input, StringComparison.OrdinalIgnoreCase) || input.Contains(c, StringComparison.OrdinalIgnoreCase) ? 0 : Distance(input.ToLowerInvariant(), c)))
            .OrderBy(x => x.Score)
            .FirstOrDefault();
        return best.Candidate != null && best.Score <= 3 ? $" Did you mean '{best.Candidate}'?" : string.Empty;
    }

    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
            d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++)
            d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
