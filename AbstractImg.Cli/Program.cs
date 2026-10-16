using System.Diagnostics;
using AbstractImg.Core.Generation;
using AbstractImg.Core.Palettes;
using AbstractImg.Core.Styles;
using SkiaSharp;

// Headless helper: contact sheets for visual checks, timing per style, and command line generation.
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "sheet":
        Sheet(args.Length > 1 ? args[1] : "sheets", args.Length > 2 ? int.Parse(args[2]) : 1, args.Length > 3 ? args[3] : null);
        break;
    case "bench":
        Bench(args.Length > 1 ? int.Parse(args[1]) : 1920, args.Length > 2 ? int.Parse(args[2]) : 1080, args.Length > 3 ? int.Parse(args[3]) : 3);
        break;
    case "gen":
        await Generate(args.Length > 1 ? args[1] : "out", args.Length > 2 ? int.Parse(args[2]) : 20);
        break;
    default:
        Console.WriteLine("Usage: sheet <folder> [seed] [styleId] | bench [w] [h] [n] | gen <folder> [count]");
        break;
}

static void Sheet(string folder, int seed, string? only)
{
    Directory.CreateDirectory(folder);
    const int w = 420, h = 263, columns = 4, label = 26;
    var rng = new Random(seed);
    foreach (var group in StyleCatalog.All.Where(s => only == null || s.Id == only).GroupBy(s => s.Category))
    {
        var styles = group.ToList();
        using var surface = SKSurface.Create(new SKImageInfo(columns * w, styles.Count * (h + label)));
        surface.Canvas.Clear(new SKColor(30, 30, 34));
        using var font = new SKFont(SKTypeface.Default, 18);
        using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
        for (var row = 0; row < styles.Count; row++)
        {
            surface.Canvas.DrawText(styles[row].Name, 8, row * (h + label) + 19, SKTextAlign.Left, font, text);
            for (var col = 0; col < columns; col++)
            {
                var family = (PaletteFamily)rng.Next(7);
                var tone = (BackgroundTone)rng.Next(3);
                var detail = (DetailLevel)(col % 3);
                var sw = Stopwatch.StartNew();
                var (image, _) = ImageGenerator.Render(styles[row], rng.Next(), family, tone, detail, col == 3 ? h : w, col == 3 ? w : h, true, false);
                using (image)
                {
                    var dest = col == 3
                        ? SKRect.Create(col * w + (w - h * h / (float)w) / 2, row * (h + label) + label, h * h / (float)w, h)
                        : SKRect.Create(col * w, row * (h + label) + label, w, h);
                    surface.Canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear));
                }
                surface.Canvas.DrawText($"{family}/{tone}/{detail} {sw.ElapsedMilliseconds}ms", col * w + 8, row * (h + label) + label + h - 8, SKTextAlign.Left, new SKFont(SKTypeface.Default, 13), text);
            }
        }
        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(folder, $"{group.Key}.png");
        File.WriteAllBytes(path, data.ToArray());
        Console.WriteLine(path);
    }
}

static void Bench(int width, int height, int n)
{
    var rng = new Random(42);
    long total = 0;
    foreach (var style in StyleCatalog.All)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
        {
            var (image, _) = ImageGenerator.Render(style, rng.Next(), (PaletteFamily)rng.Next(7), (BackgroundTone)rng.Next(3), DetailLevel.High, width, height, true, true);
            image.Dispose();
        }
        var ms = sw.ElapsedMilliseconds / n;
        total += ms;
        Console.WriteLine($"{style.Id,-16} {ms,6} ms");
    }
    Console.WriteLine($"{"average",-16} {total / StyleCatalog.All.Count,6} ms");
}

static async Task Generate(string folder, int count)
{
    var settings = new GeneratorSettings { OutputFolder = Path.GetFullPath(folder), Count = count, Threads = Environment.ProcessorCount };
    var run = ImageGenerator.Start(settings, CancellationToken.None, out var completion);
    while (!completion.IsCompleted)
    {
        await Task.WhenAny(completion, Task.Delay(500));
        Console.Write($"\r{run.Completed}/{run.Total}  {run.Completed / Math.Max(0.001, run.Watch.Elapsed.TotalSeconds):0.0} img/s");
    }
    Console.WriteLine();
    if (run.FirstError != null)
        Console.WriteLine($"{run.Failed} failed: {run.FirstError}");
}
