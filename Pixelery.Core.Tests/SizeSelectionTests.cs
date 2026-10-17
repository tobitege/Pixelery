using Pixelery.Core.Generation;
using Pixelery.Core.Styles;
using SkiaSharp;

namespace Pixelery.Core.Tests;

public sealed class SizeSelectionTests
{
    [Fact]
    public void DefaultsUseOneLandscapeSizeAndActualFileDates()
    {
        var settings = new GeneratorSettings();
        Assert.False(settings.MultipleSizes);
        Assert.False(settings.RandomSizes);
        Assert.False(settings.SpreadDates);
        var size = Assert.Single(settings.BuildSizePool());
        Assert.Equal((1920, 1080), (size.Width, size.Height));
        Assert.DoesNotContain("Validation_Sizes", settings.Validate());
    }

    [Fact]
    public void SingleSelectionCannotBeClearedAndSwitchesBetweenPresetsAndCombinations()
    {
        var settings = new GeneratorSettings();
        settings.SelectRatio("4:3", true);
        settings.SelectLongEdge(800, true);
        settings.SelectRatio("4:3", false);
        settings.SelectLongEdge(800, false);
        Assert.Equal("4:3", Assert.Single(settings.AspectRatios));
        Assert.Equal(800, Assert.Single(settings.LongEdges));
        Assert.Equal((800, 600), Dimensions(Assert.Single(settings.BuildSizePool())));

        settings.SelectFixedSize("720x1280", true);
        settings.SelectFixedSize("720x1280", false);
        Assert.Empty(settings.AspectRatios);
        Assert.Empty(settings.LongEdges);
        Assert.Equal((1280, 720), Dimensions(Assert.Single(settings.BuildSizePool())));
        settings.SelectOrientation(ImageOrientation.Portrait);
        Assert.Equal((720, 1280), Dimensions(Assert.Single(settings.BuildSizePool())));

        settings.SelectLongEdge(640, true);
        Assert.Empty(settings.FixedSizes);
        Assert.Equal((360, 640), Dimensions(Assert.Single(settings.BuildSizePool())));
    }

    [Fact]
    public void ModeSelectionsSurviveSwitchingAndASettingsRoundTrip()
    {
        var settings = new GeneratorSettings();
        settings.SelectFixedSize("800x600", true);
        settings.SelectOrientation(ImageOrientation.Portrait);
        settings.SetMultipleSizes(true);
        settings.SelectRatio("1:1", true);
        settings.SelectLongEdge(512, true);
        settings.SelectFixedSize("96x64", true);
        settings.RandomSizes = true;
        var multiple = settings.BuildSizePool();

        settings.SetMultipleSizes(false);
        Assert.False(settings.RandomSizes);
        Assert.Equal((600, 800), Dimensions(Assert.Single(settings.BuildSizePool())));
        var loaded = SettingsJson.Deserialize(SettingsJson.Serialize(settings));
        loaded.SetMultipleSizes(true);
        Assert.Equal(multiple, loaded.BuildSizePool());
        loaded.RandomSizes = true;
        loaded = SettingsJson.Deserialize(SettingsJson.Serialize(loaded));
        Assert.True(loaded.RandomSizes);
        loaded.SetMultipleSizes(false);
        Assert.Equal((600, 800), Dimensions(Assert.Single(loaded.BuildSizePool())));
    }

    [Fact]
    public void RemovingCustomChoicesAlsoRepairsTheRememberedSingleSelection()
    {
        var settings = new GeneratorSettings { CustomFixedSizes = ["777x555"] };
        settings.SelectFixedSize("777x555", true);
        settings.SetMultipleSizes(true);
        settings.SelectFixedSize("777x555", true);
        settings.RemoveCustomFixedSize("777x555");
        Assert.Empty(settings.CustomFixedSizes);
        Assert.Empty(settings.FixedSizes);
        settings.SetMultipleSizes(false);
        Assert.Equal((1920, 1080), Dimensions(Assert.Single(settings.BuildSizePool())));

        settings.CustomAspectRatios.Add("7:5");
        settings.CustomLongEdges.Add(700);
        settings.SelectRatio("7:5", true);
        settings.SelectLongEdge(700, true);
        settings.SetMultipleSizes(true);
        settings.RemoveCustomRatio("7:5");
        settings.RemoveCustomLongEdge(700);
        settings.SetMultipleSizes(false);
        Assert.Equal((1920, 1080), Dimensions(Assert.Single(settings.BuildSizePool())));
    }

    [Fact]
    public void FixedSizesFollowOrientationAndSquareSizesAreNotDuplicated()
    {
        var settings = new GeneratorSettings
        {
            MultipleSizes = true, AspectRatios = [], LongEdges = [],
            FixedSizes = ["600x800", "800x600", "512x512"],
            Orientations = [ImageOrientation.Landscape, ImageOrientation.Portrait]
        };
        Assert.Equal([(800, 600), (600, 800), (512, 512)], settings.BuildSizePool().Select(Dimensions));
        settings.SelectOrientation(ImageOrientation.Landscape);
        settings.SelectOrientation(ImageOrientation.Portrait);
        Assert.Equal(ImageOrientation.Portrait, Assert.Single(settings.Orientations));
    }

    [Fact]
    public void ClonesOwnBothSizeSelectionsAndSavedPatterns()
    {
        var settings = new GeneratorSettings();
        var clone = settings.Clone();
        clone.AspectRatios.Clear();
        clone.InactiveSizeSelection.LongEdges.Clear();
        clone.TryAddNamePattern("copy_{n}");
        Assert.Single(settings.AspectRatios);
        Assert.Single(settings.InactiveSizeSelection.LongEdges);
        Assert.Single(settings.NamePatterns);
    }

    [Fact]
    public void RandomSizesAreReproducibleAndDifferFromOrderedCycling()
    {
        var settings = MultipleSettings();
        var sizes = settings.BuildSizePool();
        SizeChoice[] Recipes() => Enumerable.Range(0, 40).Select(i => ImageGenerator.CreateRecipe(
            settings, [new TruchetStyle()], sizes, ImageGenerator.ImageSeed(42, i), settings.StartIndex + i,
            new DateTime(2026, 1, 1)).Size).ToArray();
        var ordered = Recipes();
        Assert.Equal(Enumerable.Range(0, 40).Select(i => sizes[i % sizes.Count]), ordered);
        settings.RandomSizes = true;
        var random = Recipes();
        Assert.Equal(random, Recipes());
        Assert.False(random.SequenceEqual(ordered));
        Assert.All(random, size => Assert.Contains(size, sizes));
    }

    [Fact]
    public async Task ParallelGenerationCyclesSizesByImageNumberAndKeepsActualFileTimes()
    {
        var settings = MultipleSettings();
        settings.OutputFolder = TestData.NewDirectory();
        settings.ReferenceDate = new DateOnly(2000, 1, 1);
        settings.NamePattern = "{n:000}";
        settings.Count = 9;
        settings.Threads = 3;
        settings.Formats = [OutputFormat.Png];
        var before = DateTime.UtcNow.AddSeconds(-2);
        var run = ImageGenerator.Start(settings, CancellationToken.None, out var completion);
        await completion;
        var after = DateTime.UtcNow.AddSeconds(2);
        Assert.Equal(0, run.Failed);
        Assert.Equal(9, run.Completed);
        var sizes = settings.BuildSizePool();
        for (var i = 0; i < settings.Count; i++)
        {
            var path = Path.Combine(settings.OutputFolder, $"{settings.StartIndex + i:000}.png");
            using var image = SKBitmap.Decode(path);
            Assert.NotNull(image);
            Assert.Equal(Dimensions(sizes[i % sizes.Count]), (image.Width, image.Height));
            Assert.InRange(File.GetCreationTimeUtc(path), before, after);
            Assert.InRange(File.GetLastWriteTimeUtc(path), before, after);
        }
    }

    private static GeneratorSettings MultipleSettings() => new()
    {
        MultipleSizes = true, StartIndex = 17, AspectRatios = [], LongEdges = [],
        FixedSizes = ["64x32", "80x40", "96x48"], Styles = ["truchet"], Seed = "42"
    };

    private static (int Width, int Height) Dimensions(SizeChoice size) => (size.Width, size.Height);
}
