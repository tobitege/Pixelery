using System.Diagnostics;
using System.Reflection;
using AbstractImg.Cli;
using AbstractImg.Core.Generation;
using AbstractImg.Core.Palettes;
using AbstractImg.Core.Styles;
using SkiaSharp;

// abstractimg: command line front end for the AbstractImg generator (same engine and settings as the app).
// Progress bar and typographic characters need UTF-8 in classic consoles.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var rest = args.Skip(1).ToArray();
try
{
    if (rest.Any(a => a is "--help" or "-h" or "/?"))
    {
        Help.Print(command);
        return 0;
    }

    switch (command)
    {
        case "generate" or "gen":
            return await GenerateCommand.RunAsync(Reader(rest));
        case "render":
            return SingleImageCommands.Render(Reader(rest));
        case "setbackground" or "set-background" or "wallpaper":
            return OperatingSystem.IsWindows() ? SingleImageCommands.SetBackground(Reader(rest)) : WindowsOnly();
        case "setscreensaver" or "set-screensaver" or "screensaver":
            return OperatingSystem.IsWindows() ? SingleImageCommands.SetScreensaver(Reader(rest, "activate")) : WindowsOnly();
        case "list" or "ls":
            return ListCommand.Run(rest);
        case "sheet":
            DevCommands.Sheet(rest.Length > 0 ? rest[0] : "sheets", rest.Length > 1 ? int.Parse(rest[1]) : 1, rest.Length > 2 ? rest[2] : null);
            return 0;
        case "bench":
            DevCommands.Bench(rest.Length > 0 ? int.Parse(rest[0]) : 1920, rest.Length > 1 ? int.Parse(rest[1]) : 1080, rest.Length > 2 ? int.Parse(rest[2]) : 3);
            return 0;
        case "version" or "--version" or "-v":
            Console.WriteLine($"abstractimg {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}");
            return 0;
        case "help" or "--help" or "-h" or "/?":
            Help.Print(rest.Length > 0 ? rest[0].ToLowerInvariant() : null);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            Help.Print(null);
            return 2;
    }
}
catch (UsageException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static ArgReader Reader(string[] rest, string? extraFlag = null)
{
    var flags = new HashSet<string>(SettingsOptions.Flags, StringComparer.OrdinalIgnoreCase);
    if (extraFlag != null)
        flags.Add(extraFlag);
    return new ArgReader(rest, SettingsOptions.Aliases, flags);
}

static int WindowsOnly()
{
    Console.Error.WriteLine("error: this command is only available on Windows.");
    return 1;
}

internal static class Help
{
    private const string GeneratorOptions = """
          Source (applied first, then every option below overrides it)
            -c, --config <file>      Load settings from a JSON file (format of the app's generator.json)
                --app-config         Load the settings last used in the AbstractImg app
                --save-config <file> Write the effective settings to a JSON file (also usable in the app)

          Styles and colors (comma separated lists; "all" selects everything)
            -s, --styles <list>      Style ids, names or categories, e.g. marble,flow-field or geometric (see: list styles)
                --exclude <list>     Remove styles or categories from the selection
                --palettes <list>    curated, harmony, cosine, pastel, neon, earth, monochrome
                --tones <list>       dark, light, vivid
                --detail <list>      low, medium, high
                --grain / --no-grain         Film grain
                --vignette / --no-vignette   Darker edges

          Sizes (all combinations form the pool; each image picks one at random; size options replace the
          sizes of a loaded configuration: --sizes alone uses only those sizes, --ratios/--edges alone no fixed sizes)
                --orientation <list> landscape, portrait or both
                --ratios <list>      Aspect ratios, e.g. 16:9,4:3,1.85 ("none" to clear)
                --edges <list>       Long edge in pixels, e.g. 1920,3840 ("none" to clear)
                --sizes <list>       Fixed sizes WIDTHxHEIGHT or preset names, e.g. 1920x1080,"4K UHD" (see: list sizes)

        """;

    public static void Print(string? command)
    {
        switch (command)
        {
            case "generate" or "gen":
                Console.WriteLine("""
                    abstractimg generate [options]

                    Generates many images with the same engine and settings as the app.

                    """ + GeneratorOptions + """
                      Output
                        -o, --out <folder>       Target folder (default: Pictures\AbstractImg)
                        -n, --count <n>          Number of images (default 100)
                        -j, --threads <n|max>    Parallel threads (default: half the logical processors)
                            --seed <text>        Master seed (number or any text) for reproducible runs
                            --formats <list>     jpeg, png, webp
                            --quality <1-100>    JPEG/WebP quality (default 90)
                            --name <pattern>     File name schema, e.g. "{style}_{n:00000}_{w}x{h}" (see: list placeholders)
                            --start <n>          First value of {n}
                            --overwrite / --no-overwrite        Replace existing files instead of adding a suffix
                            --spread-dates / --no-spread-dates  Random file dates in the past
                            --spread-years <n>   How many years into the past (implies --spread-dates)
                            --exif / --no-exif   Write date taken and camera into JPEG files

                      Run
                            --dry-run            Show the plan and example file names, generate nothing
                        -q, --quiet              No progress output
                            --json               Print a JSON summary at the end (for scripts)

                    Exit codes: 0 done, 1 failures, 2 invalid options, 130 cancelled (Ctrl+C).

                    Examples:
                      abstractimg generate -o D:\TestImages -n 10000 -j max
                      abstractimg generate --app-config -n 500
                      abstractimg generate -s marble,aurora --palettes neon --tones dark --sizes "4K UHD" --formats png
                      abstractimg generate -n 2000 --name "{date:yyyy}\{date:MM}\{style}_{n:00000}" --spread-years 10
                    """);
                break;
            case "render":
                Console.WriteLine("""
                    abstractimg render <file.jpg|png|webp> [options]

                    Renders one image into a file. Without size options it matches the primary screen.
                    Accepts all style, color and size options of 'generate' plus --seed and --quality.

                    """ + GeneratorOptions + """
                    Example:
                      abstractimg render marble.png -s marble --palettes curated --sizes 2560x1440 --seed 42
                    """);
                break;
            case "setbackground" or "set-background" or "wallpaper":
                Console.WriteLine("""
                    abstractimg setbackground [options]

                    Renders one image at screen resolution and sets it as the desktop background.
                    Images are saved in %LOCALAPPDATA%\AbstractImg\Wallpaper.

                      --fit <mode>   fill, fit, stretch, center, tile or span (default: keep the current mode)

                    """ + GeneratorOptions + """
                    Example:
                      abstractimg setbackground -s aurora,nebula,synthwave --tones dark
                    """);
                break;
            case "setscreensaver" or "set-screensaver" or "screensaver":
                Console.WriteLine("""
                    abstractimg setscreensaver [options]

                    Renders one image at screen resolution and makes it the picture of the Windows "Photos"
                    screen saver. Each call creates its own folder under %LOCALAPPDATA%\AbstractImg\Screensaver.

                      --activate     Also make the Photos screen saver the active screen saver

                    """ + GeneratorOptions + """
                    Example:
                      abstractimg setscreensaver -s geometric --activate
                    """);
                break;
            case "list" or "ls":
                Console.WriteLine("""
                    abstractimg list <styles|palettes|sizes|placeholders>

                      styles         All styles with id, category and name
                      palettes       Palette families, tones, detail levels and the curated palettes
                      sizes          Aspect ratio, long edge and fixed size presets
                      placeholders   File name placeholders for --name
                    """);
                break;
            default:
                Console.WriteLine("""
                    abstractimg - mass generator for abstract images

                    Usage: abstractimg <command> [options]

                    Commands:
                      generate         Generate many images (same settings as the app)
                      render           Render one image into a file
                      setbackground    Render one image and set it as the desktop background
                      setscreensaver   Render one image and show it in the Windows Photos screen saver
                      list             List styles, palettes, sizes or file name placeholders
                      sheet            Developer: contact sheets per category (sheet <folder> [seed] [styleId])
                      bench            Developer: render time per style (bench [width] [height] [n])
                      version          Show the version

                    Run 'abstractimg <command> --help' for the options of a command.
                    """);
                break;
        }
    }
}

internal static class ListCommand
{
    public static int Run(string[] rest)
    {
        var what = rest.Length > 0 ? rest[0].ToLowerInvariant() : string.Empty;
        switch (what)
        {
            case "styles":
                foreach (var group in StyleCatalog.All.GroupBy(s => s.Category))
                {
                    Console.WriteLine($"{group.Key.ToString().ToLowerInvariant()}");
                    foreach (var style in group)
                        Console.WriteLine($"  {style.Id,-16} {style.Name}");
                }
                break;
            case "palettes":
                Console.WriteLine($"families  {string.Join(", ", Enum.GetNames<PaletteFamily>().Select(n => n.ToLowerInvariant()))}");
                Console.WriteLine($"tones     {string.Join(", ", Enum.GetNames<BackgroundTone>().Select(n => n.ToLowerInvariant()))}");
                Console.WriteLine($"detail    {string.Join(", ", Enum.GetNames<DetailLevel>().Select(n => n.ToLowerInvariant()))}");
                Console.WriteLine($"curated   {string.Join(", ", PaletteFactory.CuratedNames)}");
                break;
            case "sizes":
                Console.WriteLine($"ratios    {string.Join(", ", SizeCatalog.AspectRatios)}");
                Console.WriteLine($"edges     {string.Join(", ", SizeCatalog.LongEdges)}");
                Console.WriteLine("fixed");
                foreach (var preset in SizeCatalog.FixedSizes)
                    Console.WriteLine($"  {preset.Label,-14} {preset.Width}x{preset.Height}");
                break;
            case "placeholders":
                foreach (var (token, example) in NameTemplate.Placeholders)
                    Console.WriteLine($"  {token,-11} {example}");
                Console.WriteLine();
                Console.WriteLine("""
                      Formats: {n:00000} pads numbers, {date:yyyy-MM-dd} uses .NET date formats,
                      {style:upper|lower|title} changes case, {seed:d} prints the seed as a number, {rand:8} sets the length.
                      Use \ or / for subfolders. The extension is added automatically.
                    """);
                break;
            default:
                throw new UsageException("list expects one of: styles, palettes, sizes, placeholders.");
        }
        return 0;
    }
}

/// <summary>Developer helpers used to check styles visually and measure them.</summary>
internal static class DevCommands
{
    public static void Sheet(string folder, int seed, string? only)
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
            using var small = new SKFont(SKTypeface.Default, 13);
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
                    surface.Canvas.DrawText($"{family}/{tone}/{detail} {sw.ElapsedMilliseconds}ms", col * w + 8, row * (h + label) + label + h - 8, SKTextAlign.Left, small, text);
                }
            }
            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            var path = Path.Combine(folder, $"{group.Key}.png");
            File.WriteAllBytes(path, data.ToArray());
            Console.WriteLine(path);
        }
    }

    public static void Bench(int width, int height, int n)
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
}
