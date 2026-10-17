using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Styles;
using Microsoft.Win32;

namespace Pixelery.Cli;

/// <summary>
/// Commands that produce exactly one image with the generator options: <c>render</c> (to a file),
/// <c>setbackground</c> (desktop wallpaper) and <c>setscreensaver</c> (Windows "Photos" screen saver folder).
/// Without size options the image matches the primary screen resolution.
/// </summary>
public static class SingleImageCommands
{
    private static string StateRoot => Path.GetDirectoryName(SettingsOptions.AppConfigPath)!;

    public static int Render(ArgReader args)
    {
        var file = args.Value("file") ?? (args.Positional.Count > 0 ? args.Positional[0] : null)
            ?? throw new UsageException("render needs a target file: render <file.jpg|png|webp> [options]");
        var format = Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".png" => OutputFormat.Png,
            ".webp" => OutputFormat.Webp,
            ".jpg" or ".jpeg" => OutputFormat.Jpeg,
            _ => throw new UsageException("The file name must end with .jpg, .png or .webp.")
        };
        var (settings, quiet) = Prepare(args, allowPositional: 1);
        var path = Path.GetFullPath(file);
        var (recipe, bytes) = RenderOne(settings, format);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        if (!quiet)
            Report(recipe, path);
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int SetBackground(ArgReader args)
    {
        var fit = args.Value("fit");
        var (settings, quiet) = Prepare(args);
        var folder = Path.Combine(StateRoot, "Wallpaper");
        Directory.CreateDirectory(folder);
        var (recipe, bytes) = RenderOne(settings, OutputFormat.Jpeg);
        // A new file name each time: Windows caches the wallpaper by path.
        var path = Path.Combine(folder, $"wallpaper-{DateTime.Now:yyyyMMdd-HHmmss}-{recipe.Style.Id}.jpg");
        File.WriteAllBytes(path, bytes);

        if (fit != null)
            SetWallpaperStyle(fit);
        if (!SystemParametersInfo(SpiSetDeskWallpaper, 0, path, SpifUpdateIniFile | SpifSendChange))
            throw new InvalidOperationException($"Windows refused the wallpaper (error {Marshal.GetLastWin32Error()}).");

        if (!quiet)
        {
            Report(recipe, path);
            Console.WriteLine("Desktop background updated.");
        }
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int SetScreensaver(ArgReader args)
    {
        var activate = args.Has("activate");
        var (settings, quiet) = Prepare(args);
        // The screen saver shows every picture in its folder: each call gets its own folder holding just the new image.
        var (recipe, bytes) = RenderOne(settings, OutputFormat.Jpeg);
        var folder = Path.Combine(StateRoot, "Screensaver", $"{DateTime.Now:yyyyMMdd-HHmmss}-{recipe.Style.Id}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"pixelery-{recipe.Style.Id}.jpg");
        File.WriteAllBytes(path, bytes);

        PhotoScreensaver.SetFolder(folder);
        // Read the value back to make sure Windows can resolve it.
        var stored = PhotoScreensaver.GetFolder();
        if (!string.Equals(stored?.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The screen saver folder could not be verified (stored: {stored ?? "nothing"}).");
        if (activate)
            PhotoScreensaver.Activate();

        if (!quiet)
        {
            Report(recipe, path);
            Console.WriteLine($"Photos screen saver now shows {folder}");
            if (!activate && !PhotoScreensaver.IsActive())
                Console.WriteLine("Note: the Photos screen saver is not the active screen saver. Add --activate to switch to it.");
        }
        return 0;
    }

    /// <summary>Builds the settings for one image: generator options, count 1, screen size unless sizes were given.</summary>
    private static (GeneratorSettings Settings, bool Quiet) Prepare(ArgReader args, int allowPositional = 0)
    {
        var sizeGiven = args.Has("ratios") || args.Has("edges") || args.Has("sizes") || args.Has("orientation");
        var settings = SettingsOptions.Build(args);
        var quiet = args.Has("quiet");
        args.EnsureAllConsumed();
        if (args.Positional.Count > allowPositional)
            throw new UsageException($"Unexpected argument '{args.Positional[allowPositional]}'.");

        settings.Count = 1;
        settings.SpreadDates = false;
        if (!sizeGiven && ScreenSize() is { } screen)
        {
            settings.AspectRatios = [];
            settings.LongEdges = [];
            settings.FixedSizes = [$"{screen.Width}x{screen.Height}"];
            settings.Orientations = [screen.Width >= screen.Height ? ImageOrientation.Landscape : ImageOrientation.Portrait];
            settings.MultipleSizes = false;
            settings.RandomSizes = false;
        }

        var problems = settings.Validate().Where(p => p is not ("Validation_Folder" or "Validation_Formats" or "Validation_Name")).ToList();
        if (problems.Contains("Validation_WatermarkImage"))
            throw new UsageException("A watermark image layer has no readable image file (--watermark-image).");
        if (problems.Count > 0)
            throw new UsageException("Nothing to render: check --styles, --palettes, --tones, --detail and the size options.");
        return (settings, quiet);
    }

    private static (ImageRecipe Recipe, byte[] Bytes) RenderOne(GeneratorSettings settings, OutputFormat format)
    {
        var styles = settings.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList();
        var sizes = settings.BuildSizePool();
        var seed = ImageGenerator.ImageSeed(ImageGenerator.ParseSeed(settings.Seed), 0);
        var now = settings.ReferenceTime();
        var recipe = ImageGenerator.CreateRecipe(settings, styles, sizes, seed, settings.StartIndex, now) with { Format = format };
        var (rendered, palette) = ImageGenerator.Render(recipe);
        var values = ImageGenerator.CreateNameValues(recipe, 1, ImageGenerator.PaletteSlug(palette), now);
        var image = Core.Watermark.WatermarkRenderer.Apply(rendered, settings.Watermark, values, recipe.Seed);
        if (!ReferenceEquals(image, rendered))
            rendered.Dispose();
        using (image)
        {
            var quality = format == OutputFormat.Png ? 100 : Math.Max(settings.Quality, 90);
            var exif = format == OutputFormat.Jpeg && settings.WriteExif
                ? new ExifInfo(DateTime.Now, "Pixelery", $"Pixelery {recipe.Style.Name}", "Pixelery")
                : (ExifInfo?)null;
            return (recipe, ImageEncoder.Encode(image, format, quality, exif));
        }
    }

    private static void Report(ImageRecipe recipe, string path) =>
        Console.WriteLine($"{recipe.Style.Name} · {recipe.Size.Width}×{recipe.Size.Height} · {recipe.Family.ToString().ToLowerInvariant()}/{recipe.Tone.ToString().ToLowerInvariant()} · seed {(uint)recipe.Seed:x8}{Environment.NewLine}{path}");

    /// <summary>Primary screen in physical pixels (independent of DPI scaling).</summary>
    private static (int Width, int Height)? ScreenSize()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var mode = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        return EnumDisplaySettings(null, EnumCurrentSettings, ref mode) && mode.dmPelsWidth > 0
            ? (mode.dmPelsWidth, mode.dmPelsHeight)
            : null;
    }

    [SupportedOSPlatform("windows")]
    private static void SetWallpaperStyle(string fit)
    {
        // WallpaperStyle / TileWallpaper values as written by the Settings app.
        var (style, tile) = fit.ToLowerInvariant() switch
        {
            "fill" => ("10", "0"),
            "fit" => ("6", "0"),
            "stretch" => ("2", "0"),
            "center" => ("0", "0"),
            "tile" => ("0", "1"),
            "span" => ("22", "0"),
            _ => throw new UsageException("--fit expects fill, fit, stretch, center, tile or span.")
        };
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new InvalidOperationException("Cannot open the desktop settings.");
        key.SetValue("WallpaperStyle", style);
        key.SetValue("TileWallpaper", tile);
    }

    private const int SpiSetDeskWallpaper = 0x0014;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendChange = 0x02;
    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int param, string value, int winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}

/// <summary>
/// Settings of the built-in Windows "Photos" screen saver (PhotoScreensaver.scr). Despite its name, the value
/// "EncryptedPIDL" holds the folder's shell item ID list (PIDL) only Base64 encoded, not encrypted.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PhotoScreensaver
{
    private const string SettingsKey = @"Software\Microsoft\Windows Photo Viewer\Slideshow\Screensaver";
    private const string ScreensaverExe = "PhotoScreensaver.scr";

    public static void SetFolder(string folder)
    {
        var pidl = FolderToPidl(folder);
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey, writable: true);
        key.SetValue("EncryptedPIDL", Convert.ToBase64String(pidl), RegistryValueKind.String);
    }

    /// <summary>The folder currently configured, or null (used to verify the stored format).</summary>
    public static string? GetFolder()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        if (key?.GetValue("EncryptedPIDL") is not string text || string.IsNullOrWhiteSpace(text))
            return null;
        return PidlToPath(Convert.FromBase64String(text));
    }

    public static bool IsActive()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("SCRNSAVE.EXE") is string exe && exe.EndsWith(ScreensaverExe, StringComparison.OrdinalIgnoreCase);
    }

    public static void Activate()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), ScreensaverExe);
        if (!File.Exists(exe))
            throw new InvalidOperationException($"{ScreensaverExe} was not found in {Path.GetDirectoryName(exe)}.");
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new InvalidOperationException("Cannot open the desktop settings.");
        key.SetValue("SCRNSAVE.EXE", exe);
        key.SetValue("ScreenSaveActive", "1");
    }

    private static byte[] FolderToPidl(string folder)
    {
        var hr = SHParseDisplayName(Path.GetFullPath(folder), IntPtr.Zero, out var pidl, 0, out _);
        if (hr != 0 || pidl == IntPtr.Zero)
            throw new InvalidOperationException($"Cannot resolve the folder '{folder}' (HRESULT 0x{hr:X8}).");
        try
        {
            var size = (int)ILGetSize(pidl);
            var bytes = new byte[size];
            Marshal.Copy(pidl, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    private static string? PidlToPath(byte[] pidlBytes)
    {
        var pidl = Marshal.AllocCoTaskMem(pidlBytes.Length);
        try
        {
            Marshal.Copy(pidlBytes, 0, pidl, pidlBytes.Length);
            var buffer = new char[1024];
            return SHGetPathFromIDListEx(pidl, buffer, buffer.Length, 0) ? new string(buffer).TrimEnd('\0') : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll")]
    private static extern uint ILGetSize(IntPtr pidl);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDListEx(IntPtr pidl, [Out] char[] path, int length, int options);
}
