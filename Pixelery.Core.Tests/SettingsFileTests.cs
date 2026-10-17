using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pixelery.Core.Generation;

namespace Pixelery.Core.Tests;

public sealed class SettingsFileTests
{
    [Fact]
    public void ExportLeavesOutTheMachineSettingsAndRecordsTheVersion()
    {
        var settings = new GeneratorSettings { OutputFolder = @"C:\Gallery", Threads = 7, Seed = "4711", NamePattern = "{style}_{n:000}" };

        var json = JsonNode.Parse(SettingsJson.Export(settings, "9.8.7"))!.AsObject();

        Assert.False(json.ContainsKey(nameof(GeneratorSettings.OutputFolder)));
        Assert.False(json.ContainsKey(nameof(GeneratorSettings.Threads)));
        Assert.False(json.ContainsKey(nameof(GeneratorSettings.NamePatterns)));
        Assert.Equal(GeneratorSettings.CurrentFormatVersion, (int?)json[nameof(GeneratorSettings.FormatVersion)]);
        Assert.Equal("9.8.7", (string?)json[nameof(GeneratorSettings.AppVersion)]);
        Assert.Equal("4711", (string?)json[nameof(GeneratorSettings.Seed)]);
        Assert.Equal("{style}_{n:000}", (string?)json[nameof(GeneratorSettings.NamePattern)]);
        Assert.Null(settings.AppVersion);
    }

    [Fact]
    public void ImportKeepsTheLocalFolderAndThreadsAndTheStylesAsWritten()
    {
        // A file from a version that knew only this style: importing it must not add the styles that came later.
        var node = JsonNode.Parse(SettingsJson.Export(new GeneratorSettings { Styles = ["marble"] }, "0.1.0"))!.AsObject();
        node[nameof(GeneratorSettings.KnownStyles)] = new JsonArray("marble");
        var json = node.ToJsonString();
        var local = new GeneratorSettings { OutputFolder = @"D:\Gallery", Threads = 3 };

        var imported = SettingsJson.Import(json, local);

        Assert.Equal("marble", Assert.Single(imported.Styles));
        Assert.Equal(@"D:\Gallery", imported.OutputFolder);
        Assert.Equal(3, imported.Threads);
        Assert.Equal("0.1.0", imported.AppVersion);
        // The app's own settings file still picks up new styles.
        Assert.True(SettingsJson.Deserialize(json).Styles.Count > 1);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Entries\":[]}")]
    [InlineData("[]")]
    public void ImportRejectsJsonThatIsNoSettingsFile(string json) =>
        Assert.Throws<JsonException>(() => SettingsJson.Import(json, new GeneratorSettings()));

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("\"1\"")]
    [InlineData("null")]
    public void OnlyTheCurrentExplicitFormatVersionIsAccepted(string? version)
    {
        var node = JsonNode.Parse(SettingsJson.Export(new GeneratorSettings(), "0.3.0"))!.AsObject();
        if (version == null) node.Remove(nameof(GeneratorSettings.FormatVersion));
        else node[nameof(GeneratorSettings.FormatVersion)] = JsonNode.Parse(version);
        Assert.Throws<JsonException>(() => SettingsJson.Import(node.ToJsonString(), new GeneratorSettings()));
        Assert.Throws<JsonException>(() => SettingsJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void ImportAddsOnlyTheSelectedPatternAndDoesNotDuplicateOrMutateTheLocalCollection()
    {
        var local = new GeneratorSettings();
        local.TryAddNamePattern("local_{n}");
        var source = new GeneratorSettings();
        source.TryAddNamePattern("private_{n}");
        source.TryAddNamePattern("shared_{n}");
        var json = SettingsJson.Export(source, "0.3.0");
        var imported = SettingsJson.Import(json, local);
        Assert.Equal("shared_{n}", imported.NamePattern);
        Assert.Equal([GeneratorSettings.DefaultNamePattern, "local_{n}", "shared_{n}"], imported.NamePatterns);
        Assert.DoesNotContain("private_{n}", imported.NamePatterns);
        Assert.DoesNotContain("shared_{n}", local.NamePatterns);
        Assert.Equal(imported.NamePatterns, SettingsJson.Import(json, imported).NamePatterns);
    }

    [Fact]
    public void PatternCollectionTrimsDuplicatesPreservesFormatCaseAndRetainsOneEntry()
    {
        var settings = new GeneratorSettings();
        Assert.True(settings.TryAddNamePattern("  {date:MM}_{n}  "));
        Assert.True(settings.TryAddNamePattern("{date:MM}_{n}"));
        Assert.True(settings.TryAddNamePattern("{date:mm}_{n}"));
        var loaded = SettingsJson.Deserialize(SettingsJson.Serialize(settings));
        Assert.Equal(3, loaded.NamePatterns.Count);
        Assert.Equal("{date:mm}_{n}", loaded.NamePattern);
        loaded.RemoveNamePattern();
        Assert.Equal("{date:MM}_{n}", loaded.NamePattern);
        loaded.RemoveNamePattern();
        loaded.RemoveNamePattern();
        Assert.Equal(GeneratorSettings.DefaultNamePattern, Assert.Single(loaded.NamePatterns));
        Assert.Equal(GeneratorSettings.DefaultNamePattern, loaded.NamePattern);
    }

    [Theory]
    [InlineData("../outside/{n}")]
    [InlineData("C:\\outside\\{n}")]
    [InlineData("\\\\server\\share\\{n}")]
    [InlineData("CON.txt/{n}")]
    [InlineData("bad\nname_{n}")]
    [InlineData("{n:D999999999}")]
    public void ImportRejectsUnsafePatternsWithoutChangingLocalSettings(string pattern)
    {
        var local = new GeneratorSettings();
        var node = JsonNode.Parse(SettingsJson.Export(local, "0.3.0"))!.AsObject();
        node[nameof(GeneratorSettings.NamePattern)] = pattern;
        Assert.Throws<JsonException>(() => SettingsJson.Import(node.ToJsonString(), local));
        Assert.Equal(GeneratorSettings.DefaultNamePattern, local.NamePattern);
        Assert.Single(local.NamePatterns);
    }

    [Fact]
    public void ImportRejectsInvalidSizeModesAndNullSelections()
    {
        var node = JsonNode.Parse(SettingsJson.Export(new GeneratorSettings(), "0.3.0"))!.AsObject();
        node[nameof(GeneratorSettings.RandomSizes)] = true;
        Assert.Throws<JsonException>(() => SettingsJson.Import(node.ToJsonString(), new GeneratorSettings()));
        node[nameof(GeneratorSettings.RandomSizes)] = false;
        node[nameof(GeneratorSettings.FixedSizes)] = new JsonArray("800x600");
        Assert.Throws<JsonException>(() => SettingsJson.Import(node.ToJsonString(), new GeneratorSettings()));
        node[nameof(GeneratorSettings.FixedSizes)] = new JsonArray();
        node[nameof(GeneratorSettings.InactiveSizeSelection)] = null;
        Assert.Throws<JsonException>(() => SettingsJson.Import(node.ToJsonString(), new GeneratorSettings()));
    }

    [Fact]
    public void ExportPreservesBothSizeSelectionsAndRandomChoice()
    {
        var source = new GeneratorSettings();
        source.SelectFixedSize("640x480", true);
        source.SelectOrientation(ImageOrientation.Portrait);
        source.SetMultipleSizes(true);
        source.SelectRatio("1:1", true);
        source.SelectLongEdge(512, true);
        source.RandomSizes = true;
        var imported = SettingsJson.Import(SettingsJson.Export(source, "0.3.0"), new GeneratorSettings());
        Assert.True(imported.MultipleSizes);
        Assert.True(imported.RandomSizes);
        Assert.Equal(source.BuildSizePool(), imported.BuildSizePool());
        imported.SetMultipleSizes(false);
        var size = Assert.Single(imported.BuildSizePool());
        Assert.Equal((480, 640), (size.Width, size.Height));
    }

    [Fact]
    public void AFullPatternCollectionStillAllowsSelectingAnExistingEntry()
    {
        var settings = new GeneratorSettings();
        for (var i = 1; i < GeneratorSettings.MaxNamePatterns; i++)
            Assert.True(settings.TryAddNamePattern($"pattern{i}_{{n}}"));
        Assert.False(settings.TryAddNamePattern("overflow_{n}"));
        Assert.True(settings.TryAddNamePattern(GeneratorSettings.DefaultNamePattern));
        Assert.Equal(GeneratorSettings.MaxNamePatterns, settings.NamePatterns.Count);
        Assert.Equal(GeneratorSettings.DefaultNamePattern, settings.NamePattern);
    }

    [Fact]
    public async Task AnExportedFileGivesTheSameFiles()
    {
        var settings = new GeneratorSettings
        {
            Seed = "4711",
            ReferenceDate = new DateOnly(2020, 5, 17),
            Count = 4,
            Threads = 2,
            AspectRatios = [],
            LongEdges = [],
            FixedSizes = ["96x64"],
            Formats = [OutputFormat.Jpeg],
            NamePattern = "{date:yyyy-MM-dd}_{style}_{n:00}",
            SpreadDates = true,
            SpreadFrom = new DateOnly(2020, 2, 1),
            SpreadTo = new DateOnly(2020, 3, 1),
            WriteExif = true
        };

        var original = await GenerateAsync(settings);
        var shared = await GenerateAsync(SettingsJson.Import(SettingsJson.Export(settings, "1.0.0"), settings));

        Assert.Equal(settings.Count, original.Count);
        Assert.Equal(original, shared);
        Assert.All(original.Keys, name => Assert.InRange(
            DateOnly.ParseExact(name[..10], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            new DateOnly(2020, 2, 1), new DateOnly(2020, 3, 31)));
    }

    /// <summary>Runs a real generation into a new folder and returns file name → content hash.</summary>
    private static async Task<SortedDictionary<string, string>> GenerateAsync(GeneratorSettings settings)
    {
        var copy = settings.Clone();
        copy.OutputFolder = TestData.NewDirectory();
        var run = ImageGenerator.Start(copy, CancellationToken.None, out var completion);
        await completion;
        Assert.Equal(0, run.Failed);
        return new SortedDictionary<string, string>(
            Directory.GetFiles(copy.OutputFolder, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(copy.OutputFolder, file),
                file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))),
            StringComparer.Ordinal);
    }
}
