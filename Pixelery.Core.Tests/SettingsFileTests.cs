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
            WriteExif = true
        };

        var original = await GenerateAsync(settings);
        var shared = await GenerateAsync(SettingsJson.Import(SettingsJson.Export(settings, "1.0.0"), settings));

        Assert.Equal(settings.Count, original.Count);
        Assert.Equal(original, shared);
        // File dates count back from the reference date, not from today.
        Assert.All(original.Keys, name => Assert.True(string.CompareOrdinal(name[..10], "2020-05-17") <= 0, name));
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
