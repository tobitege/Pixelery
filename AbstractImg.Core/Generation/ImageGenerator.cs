using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AbstractImg.Core.Output;
using AbstractImg.Core.Palettes;
using AbstractImg.Core.Rendering;
using AbstractImg.Core.Styles;
using SkiaSharp;

namespace AbstractImg.Core.Generation;

/// <summary>All random decisions for one image, derived from its seed.</summary>
public sealed record ImageRecipe(
    int Index,
    int Seed,
    ArtStyle Style,
    PaletteFamily Family,
    BackgroundTone Tone,
    DetailLevel Detail,
    SizeChoice Size,
    OutputFormat Format,
    DateTime Date,
    bool Grain,
    bool Vignette);

/// <summary>The most recent preview published by a run.</summary>
public sealed record GenerationPreview(byte[] Jpeg, string FilePath, int Width, int Height, string Style);

/// <summary>Live state of a generation run. Counters are updated from worker threads; read them from a UI timer.</summary>
public sealed class GenerationRun
{
    private int _completed;
    private int _failed;
    private long _bytes;
    private long _lastPreviewTicks;
    private GenerationPreview? _preview;
    private string? _firstError;

    public int Total { get; init; }

    public int Completed => Volatile.Read(ref _completed);

    public int Failed => Volatile.Read(ref _failed);

    public long BytesWritten => Interlocked.Read(ref _bytes);

    public string? FirstError => Volatile.Read(ref _firstError);

    public GenerationPreview? Preview => Volatile.Read(ref _preview);

    /// <summary>Minimum time between previews; zero disables previews.</summary>
    public TimeSpan PreviewInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    public int PreviewEdge { get; set; } = 640;

    public Stopwatch Watch { get; } = new();

    public bool WasCancelled { get; internal set; }

    public string OutputFolder { get; init; } = string.Empty;

    internal void ReportSuccess(long bytes)
    {
        Interlocked.Add(ref _bytes, bytes);
        Interlocked.Increment(ref _completed);
    }

    internal void ReportFailure(Exception ex)
    {
        Interlocked.CompareExchange(ref _firstError, ex.Message, null);
        Interlocked.Increment(ref _failed);
        Interlocked.Increment(ref _completed);
    }

    /// <summary>Lets exactly one worker create the next preview once the interval elapsed.</summary>
    internal bool TryClaimPreview()
    {
        if (PreviewInterval <= TimeSpan.Zero)
            return false;
        var now = Watch.Elapsed.Ticks;
        var last = Interlocked.Read(ref _lastPreviewTicks);
        if (last != 0 && now - last < PreviewInterval.Ticks)
            return false;
        return Interlocked.CompareExchange(ref _lastPreviewTicks, now, last) == last;
    }

    internal void PublishPreview(GenerationPreview preview) => Volatile.Write(ref _preview, preview);
}

/// <summary>
/// Plans, renders, encodes and writes images in parallel.
/// </summary>
public static class ImageGenerator
{
    private const int RenderSalt = 0x5BD1E995;

    /// <summary>Numeric seeds are used as-is, any other text is hashed (FNV-1a) so words work as seeds too.</summary>
    public static int ParseSeed(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Random.Shared.Next();
        if (int.TryParse(text.Trim(), out var value))
            return value;
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text.Trim()))
            hash = (hash ^ b) * 16777619u;
        return (int)hash;
    }

    /// <summary>Per-image seed from the master seed and the image index (SplitMix64 finalizer).</summary>
    public static int ImageSeed(int masterSeed, int index)
    {
        var z = ((ulong)(uint)masterSeed << 32) | (uint)index;
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return (int)(z ^ (z >> 31));
    }

    public static ImageRecipe CreateRecipe(GeneratorSettings settings, IReadOnlyList<ArtStyle> styles, IReadOnlyList<SizeChoice> sizes, int seed, int index, DateTime now)
    {
        var rng = new Random(seed);
        T Pick<T>(IReadOnlyList<T> list) => list[rng.Next(list.Count)];

        var date = now;
        if (settings.SpreadDates && settings.SpreadYears > 0)
        {
            var days = rng.NextDouble() * settings.SpreadYears * 365.25;
            date = now.AddDays(-days);
            // Plausible daytime instead of the time of the run.
            date = date.Date.AddHours(7 + rng.NextDouble() * 15);
            if (date > now)
                date = now;
        }

        return new ImageRecipe(
            index,
            seed,
            Pick(styles),
            Pick(settings.Palettes),
            Pick(settings.Tones),
            Pick(settings.Details),
            Pick(sizes),
            Pick(settings.Formats),
            date,
            settings.Grain,
            settings.Vignette);
    }

    /// <summary>The palette <see cref="Render(ImageRecipe)"/> will use, without rendering (for name previews).</summary>
    public static Palette CreatePalette(ImageRecipe recipe) =>
        PaletteFactory.Create(new Random(recipe.Seed ^ RenderSalt), recipe.Family, recipe.Tone);

    /// <summary>Renders the recipe into a new image. The caller disposes the image.</summary>
    public static (SKImage Image, Palette Palette) Render(ImageRecipe recipe) =>
        Render(recipe.Style, recipe.Seed, recipe.Family, recipe.Tone, recipe.Detail, recipe.Size.Width, recipe.Size.Height, recipe.Grain, recipe.Vignette);

    public static (SKImage Image, Palette Palette) Render(ArtStyle style, int seed, PaletteFamily family, BackgroundTone tone, DetailLevel detail,
        int width, int height, bool grain, bool vignette)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException($"Could not allocate a {width}×{height} image.");
        var rng = new Random(seed ^ RenderSalt);
        var palette = PaletteFactory.Create(rng, family, tone);
        var level = detail switch
        {
            DetailLevel.Low => 0.2f,
            DetailLevel.High => 0.85f,
            _ => 0.5f
        };
        level = Math.Clamp(level + (float)(rng.NextDouble() - 0.5) * 0.16f, 0, 1);
        var ctx = new RenderContext(surface.Canvas, width, height, rng, palette, level);
        surface.Canvas.Clear(palette.Background);
        style.Render(ctx);
        if (grain)
            PostEffects.Grain(ctx, ctx.R(0.06f, 0.18f));
        if (vignette)
            PostEffects.Vignette(ctx, ctx.R(0.25f, 0.5f));
        surface.Canvas.Flush();
        return (surface.Snapshot(), palette);
    }

    public static string PaletteSlug(Palette palette)
    {
        var sb = new StringBuilder();
        foreach (var c in palette.Name.ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    public static NameValues CreateNameValues(ImageRecipe recipe, int total, string paletteSlug, DateTime now) => new(
        recipe.Index,
        total,
        recipe.Style.Id,
        recipe.Style.Category.ToString().ToLowerInvariant(),
        recipe.Size.Width,
        recipe.Size.Height,
        recipe.Size.Ratio,
        recipe.Size.Orientation,
        paletteSlug,
        recipe.Seed,
        recipe.Date,
        now,
        ImageEncoder.Extension(recipe.Format).TrimStart('.'));

    /// <summary>Runs a full generation. Returns when all images are written or the token is cancelled.</summary>
    public static GenerationRun Start(GeneratorSettings settings, CancellationToken token, out Task completion)
    {
        var snapshot = settings.Clone();
        var styles = snapshot.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
        var sizes = snapshot.BuildSizePool();
        if (!NameTemplate.TryParse(snapshot.NamePattern, out var template, out var error) || template == null)
            throw new ArgumentException($"Invalid name pattern: {error}");
        if (styles.Count == 0 || sizes.Count == 0)
            throw new ArgumentException("Nothing to generate.");

        var run = new GenerationRun { Total = snapshot.Count, OutputFolder = snapshot.OutputFolder };
        var masterSeed = ParseSeed(snapshot.Seed);
        var now = DateTime.Now;
        var reserved = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var createdFolders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(snapshot.OutputFolder);

        run.Watch.Start();
        completion = Task.Factory.StartNew(() =>
        {
            try
            {
                Parallel.For(0, snapshot.Count, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, snapshot.Threads),
                    CancellationToken = token
                }, i =>
                {
                    try
                    {
                        GenerateOne(snapshot, styles, sizes, template, run, masterSeed, i, now, reserved, createdFolders, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        run.ReportFailure(ex);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                run.WasCancelled = true;
            }
            finally
            {
                run.Watch.Stop();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return run;
    }

    private static void GenerateOne(GeneratorSettings settings, IReadOnlyList<ArtStyle> styles, IReadOnlyList<SizeChoice> sizes, NameTemplate template,
        GenerationRun run, int masterSeed, int i, DateTime now, ConcurrentDictionary<string, byte> reserved,
        ConcurrentDictionary<string, byte> createdFolders, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var seed = ImageSeed(masterSeed, i);
        var recipe = CreateRecipe(settings, styles, sizes, seed, settings.StartIndex + i, now);
        var (image, palette) = Render(recipe);
        using (image)
        {
            token.ThrowIfCancellationRequested();
            var exif = settings.WriteExif
                ? new ExifInfo(recipe.Date, "AbstractImg", $"AbstractImg {recipe.Style.Name}", "AbstractImg")
                : (ExifInfo?)null;
            var bytes = ImageEncoder.Encode(image, recipe.Format, settings.Quality, exif);

            var values = CreateNameValues(recipe, settings.Count, PaletteSlug(palette), now);
            var relative = template.Format(values, new Random(seed ^ 0x2545F491));
            var path = ReservePath(settings.OutputFolder, relative, ImageEncoder.Extension(recipe.Format), settings.Overwrite, reserved);
            var folder = Path.GetDirectoryName(path)!;
            if (createdFolders.TryAdd(folder, 0))
                Directory.CreateDirectory(folder);

            File.WriteAllBytes(path, bytes);
            if (settings.SpreadDates)
            {
                File.SetCreationTime(path, recipe.Date);
                File.SetLastWriteTime(path, recipe.Date);
            }
            run.ReportSuccess(bytes.LongLength);

            if (run.TryClaimPreview())
                run.PublishPreview(new GenerationPreview(ImageEncoder.Thumbnail(image, run.PreviewEdge), path, recipe.Size.Width, recipe.Size.Height, recipe.Style.Name));
        }
    }

    /// <summary>Finds a free file name (unless overwriting) that no other worker of this run uses.</summary>
    private static string ReservePath(string root, string relative, string extension, bool overwrite, ConcurrentDictionary<string, byte> reserved)
    {
        var basePath = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!basePath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            basePath = Path.Combine(rootFull, Path.GetFileName(relative));

        for (var attempt = 1; attempt < 100000; attempt++)
        {
            var candidate = attempt == 1 ? basePath + extension : $"{basePath}-{attempt}{extension}";
            if (!reserved.TryAdd(candidate, 0))
                continue;
            if (overwrite || !File.Exists(candidate))
                return candidate;
        }
        throw new IOException("No free file name found.");
    }
}
