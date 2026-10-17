using System.Globalization;
using System.Text.Json;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Styles;

namespace Pixelery.Cli;

/// <summary><c>generate</c>: the same generation the app runs, driven by options or a saved configuration.</summary>
public static class GenerateCommand
{
    private static readonly Dictionary<string, string> ValidationText = new()
    {
        ["Validation_Folder"] = "Choose an output folder with --out (full path).",
        ["Validation_Styles"] = "Select at least one style (--styles).",
        ["Validation_Palettes"] = "Select at least one palette family (--palettes).",
        ["Validation_Tones"] = "Select at least one background tone (--tones).",
        ["Validation_Details"] = "Select at least one level of detail (--detail).",
        ["Validation_Sizes"] = "No sizes: combine --ratios, --orientation and --edges, or give --sizes.",
        ["Validation_Formats"] = "Select at least one format (--formats).",
        ["Validation_Name"] = "The file name schema (--name) is invalid.",
        ["Validation_Count"] = "Generate at least one image (--count).",
        ["Validation_WatermarkImage"] = "A watermark image layer has no readable image file (--watermark-image)."
    };

    public static async Task<int> RunAsync(ArgReader args)
    {
        var settings = SettingsOptions.Build(args);
        var saveConfig = args.Value("save-config");
        var dryRun = args.Has("dry-run");
        var quiet = args.Has("quiet");
        var json = args.Has("json");
        args.EnsureAllConsumed();
        if (args.Positional.Count > 0)
            throw new UsageException($"Unexpected argument '{args.Positional[0]}'. Options start with --.");

        if (!NameTemplate.TryParse(settings.NamePattern, out _, out var nameError))
            throw new UsageException($"--name: invalid placeholder or character: {nameError}. See 'list placeholders'.");
        var problems = settings.Validate();
        if (problems.Count > 0)
            throw new UsageException(string.Join(Environment.NewLine, problems.Select(p => ValidationText.GetValueOrDefault(p, p))));

        if (saveConfig != null)
        {
            SettingsJson.Save(saveConfig, settings);
            if (!quiet && !json)
                Console.WriteLine($"Settings saved to {Path.GetFullPath(saveConfig)}");
        }

        if (!quiet && !json)
            PrintPlan(settings);
        if (dryRun)
            return 0;

        using var cts = new CancellationTokenSource();
        var cancelRequests = 0;
        Console.CancelKeyPress += (_, e) =>
        {
            // First Ctrl+C stops cleanly after the images in progress; a second one ends the process.
            if (Interlocked.Increment(ref cancelRequests) == 1)
            {
                e.Cancel = true;
                cts.Cancel();
                if (!quiet)
                    Console.Error.WriteLine(Environment.NewLine + "Cancelling… (press Ctrl+C again to quit immediately)");
            }
        };

        GenerationRun run;
        Task completion;
        try
        {
            run = ImageGenerator.Start(settings, cts.Token, out completion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        run.PreviewInterval = TimeSpan.Zero;

        var progress = new ProgressPrinter(run, quiet || json);
        while (!completion.IsCompleted)
        {
            await Task.WhenAny(completion, Task.Delay(Console.IsOutputRedirected ? 5000 : 250));
            progress.Print();
        }
        await completion;
        progress.Finish();

        var written = run.Completed - run.Failed;
        if (json)
        {
            var result = new Dictionary<string, object?>
            {
                ["folder"] = run.OutputFolder,
                ["requested"] = run.Total,
                ["written"] = written,
                ["failed"] = run.Failed,
                ["cancelled"] = run.WasCancelled,
                ["seconds"] = Math.Round(run.Watch.Elapsed.TotalSeconds, 2),
                ["bytes"] = run.BytesWritten,
                ["imagesPerSecond"] = Math.Round(written / Math.Max(0.001, run.Watch.Elapsed.TotalSeconds), 2),
                ["firstError"] = run.FirstError
            };
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.DictionaryStringObject));
        }
        else if (!quiet)
        {
            Console.WriteLine(run.WasCancelled
                ? $"Cancelled: {written:N0} of {run.Total:N0} images written to {run.OutputFolder}"
                : $"Done: {written:N0} images ({Format.Bytes(run.BytesWritten)}) in {Format.Duration(run.Watch.Elapsed)} → {run.OutputFolder}");
        }
        if (run.Failed > 0)
            Console.Error.WriteLine($"{run.Failed:N0} images failed. First error: {run.FirstError}");

        return run.WasCancelled ? 130 : run.Failed > 0 ? 1 : 0;
    }

    private static void PrintPlan(GeneratorSettings s)
    {
        var sizes = s.BuildSizePool();
        var styles = s.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
        string Join<T>(IEnumerable<T> items) => string.Join(", ", items.Select(i => i!.ToString()!.ToLowerInvariant()));

        Console.WriteLine($"Output     {s.OutputFolder}");
        Console.WriteLine($"Images     {s.Count:N0} with {s.Threads} thread{(s.Threads == 1 ? string.Empty : "s")}, seed {(string.IsNullOrWhiteSpace(s.Seed) ? "random" : s.Seed)}");
        Console.WriteLine($"Styles     {styles.Count} of {StyleCatalog.All.Count}: {(styles.Count == StyleCatalog.All.Count ? "all" : string.Join(", ", styles.Select(x => x.Id)))}");
        Console.WriteLine($"Colors     palettes {Join(s.Palettes)}; tones {Join(s.Tones)}; detail {Join(s.Details)}{(s.Grain ? "; grain" : string.Empty)}{(s.Vignette ? "; vignette" : string.Empty)}");
        Console.WriteLine($"Sizes      {sizes.Count} sizes, {sizes.Min(x => x.Megapixels):0.##}–{sizes.Max(x => x.Megapixels):0.##} MP: {string.Join(", ", sizes.Take(6).Select(x => $"{x.Width}x{x.Height}"))}{(sizes.Count > 6 ? ", …" : string.Empty)}");
        Console.WriteLine($"Files      {Join(s.Formats)}, quality {s.Quality}{(s.SpreadDates ? $", dates spread over {s.SpreadYears} years" : string.Empty)}{(s.WriteExif ? ", EXIF" : string.Empty)}{(s.Overwrite ? ", overwrite" : string.Empty)}");
        Console.WriteLine($"Names      {s.NamePattern}  (start {s.StartIndex})");
        if (s.Watermark.IsActive)
        {
            var layers = s.Watermark.Layers.Where(l => l.Enabled)
                .Select(l => l.Kind == Core.Watermark.WatermarkLayerKind.Text ? $"text \"{l.Text.Replace('\n', ' ')}\"" : $"image {Path.GetFileName(l.ImagePath)}");
            Console.WriteLine($"Watermark  {string.Join(", ", layers)}");
        }
        foreach (var example in ExampleNames(s, styles, sizes))
            Console.WriteLine($"             {example}");
        Console.WriteLine();
    }

    private static IEnumerable<string> ExampleNames(GeneratorSettings s, IReadOnlyList<ArtStyle> styles, IReadOnlyList<SizeChoice> sizes)
    {
        NameTemplate.TryParse(s.NamePattern, out var template, out _);
        var now = s.ReferenceTime();
        var master = ImageGenerator.ParseSeed(string.IsNullOrWhiteSpace(s.Seed) ? "preview" : s.Seed);
        for (var i = 0; i < Math.Min(3, s.Count); i++)
        {
            var seed = ImageGenerator.ImageSeed(master, i);
            var recipe = ImageGenerator.CreateRecipe(s, styles, sizes, seed, s.StartIndex + i, now);
            var values = ImageGenerator.CreateNameValues(recipe, s.Count, ImageGenerator.PaletteSlug(ImageGenerator.CreatePalette(recipe)), now);
            yield return template!.Format(values, new Random(seed)) + ImageEncoder.Extension(recipe.Format);
        }
    }

    /// <summary>Single updating progress line on a console, periodic lines when output is redirected.</summary>
    private sealed class ProgressPrinter(GenerationRun run, bool silent)
    {
        private int _lastLength;
        private int _lastPrinted = -1;

        public void Print()
        {
            if (silent)
                return;
            var done = run.Completed;
            if (Console.IsOutputRedirected && done == _lastPrinted)
                return;
            _lastPrinted = done;
            var elapsed = run.Watch.Elapsed;
            var rate = elapsed.TotalSeconds > 0.2 ? done / elapsed.TotalSeconds : 0;
            var eta = rate > 0 && done < run.Total ? Format.Duration(TimeSpan.FromSeconds((run.Total - done) / rate)) : "–";
            var percent = run.Percent;
            var bar = new string('█', (int)(percent / 5)).PadRight(20, '·');
            var line = string.Format(CultureInfo.CurrentCulture, "{0} {1,5:0.0}%  {2:N0}/{3:N0}  {4:0.0} img/s  ETA {5}  {6}{7}",
                bar, percent, done, run.Total, rate, eta, Format.Bytes(run.BytesWritten), run.Failed > 0 ? $"  {run.Failed} failed" : string.Empty);
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(line);
                return;
            }
            Console.Write("\r" + line.PadRight(_lastLength));
            _lastLength = line.Length;
        }

        public void Finish()
        {
            if (silent)
                return;
            Print();
            if (!Console.IsOutputRedirected)
                Console.WriteLine();
        }
    }
}

internal static class Format
{
    public static string Bytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return string.Format(CultureInfo.CurrentCulture, unit == 0 ? "{0:0} {1}" : "{0:0.0} {1}", bytes, units[unit]);
    }

    public static string Duration(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
[System.Text.Json.Serialization.JsonSerializable(typeof(int))]
[System.Text.Json.Serialization.JsonSerializable(typeof(long))]
[System.Text.Json.Serialization.JsonSerializable(typeof(double))]
[System.Text.Json.Serialization.JsonSerializable(typeof(bool))]
internal partial class CliJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
