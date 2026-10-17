using Pixelery.Core.Palettes;

namespace Pixelery.Core.Tests;

internal static class TestData
{
    public static PaletteSet Palettes() => new()
    {
        Entries = Enumerable.Range(0, PaletteSet.Size).Select(i => new PaletteSample(
            $"Palette {i}", PaletteFamily.Neon, BackgroundTone.Dark, "#FF102030",
            ["#80112233", "#FFAABBCC", "#FF778899"], i % 2 == 0)).ToList()
    };

    // The caller owns this directory. CI uses disposable runner storage; local runs use app data.
    public static string NewDirectory()
    {
        var root = Environment.GetEnvironmentVariable("PIXELERY_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pixelery", "Tests");
        var path = Path.Combine(root, "Core", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
