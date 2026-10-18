using System.Text.Json;
using System.Text.Json.Nodes;
using Pixelery.Core.Generation;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;

namespace Pixelery.Core.Tests;

public sealed class PaletteSetTests
{
    [Fact]
    public async Task SaveAndLoadPreserveEveryPaletteAndKeepFlag()
    {
        var original = TestData.Palettes();
        var path = Path.Combine(TestData.NewDirectory(), "palettes.json");

        await original.SaveAsync(path);
        var loaded = await PaletteSet.LoadAsync(path);

        Assert.True(loaded.IsValid());
        Assert.Equal(PaletteSet.Size, loaded.Entries.Count);
        for (var i = 0; i < PaletteSet.Size; i++)
        {
            var expected = original.Entries[i];
            var actual = loaded.Entries[i];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Family, actual.Family);
            Assert.Equal(expected.Tone, actual.Tone);
            Assert.Equal(expected.Background, actual.Background);
            Assert.Equal(expected.Colors, actual.Colors);
            Assert.Equal(expected.Keep, actual.Keep);
            Assert.Equal(expected.ToPalette().Colors, actual.ToPalette().Colors);
        }
        // Exclusive access proves that both asynchronous operations closed their file handles.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.Length > 0);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Entries\":null}")]
    [InlineData("{\"Entries\":[]}")]
    [InlineData("{broken")]
    public async Task LoadRejectsMissingOrMalformedSets(string json)
    {
        var path = Path.Combine(TestData.NewDirectory(), "invalid.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<JsonException>(() => PaletteSet.LoadAsync(path));
    }

    [Theory]
    [InlineData("Name", "")]
    [InlineData("Family", "Unknown")]
    [InlineData("Tone", "Unknown")]
    [InlineData("Background", "#123456")]
    [InlineData("Background", "#FFGG0000")]
    [InlineData("Colors", "invalid")]
    public async Task LoadRejectsInvalidSampleFields(string field, string value)
    {
        var path = Path.Combine(TestData.NewDirectory(), "invalid-sample.json");
        await TestData.Palettes().SaveAsync(path);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        var sample = document["Entries"]![0]!;
        sample[field] = field == "Colors" ? new JsonArray(value, "#FFFFFFFF") : JsonValue.Create(value);
        await File.WriteAllTextAsync(path, document.ToJsonString());

        await Assert.ThrowsAsync<JsonException>(() => PaletteSet.LoadAsync(path));
    }

    [Fact]
    public async Task InvalidSaveDoesNotOverwriteExistingFile()
    {
        var path = Path.Combine(TestData.NewDirectory(), "existing.json");
        await File.WriteAllTextAsync(path, "existing contents");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PaletteSet().SaveAsync(path));
        Assert.Equal("existing contents", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    public async Task LoadRejectsAnIncorrectPaletteCount(int count)
    {
        var entries = Enumerable.Range(0, count).Select(i => TestData.Palettes().Entries[i % PaletteSet.Size]).ToArray();
        var json = JsonSerializer.Serialize(new { Entries = entries });
        var path = Path.Combine(TestData.NewDirectory(), "wrong-count.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<JsonException>(() => PaletteSet.LoadAsync(path));
    }

    [Fact]
    public void CloneOwnsItsEntriesAndColorArrays()
    {
        var original = TestData.Palettes();
        var clone = original.Clone();
        clone.Entries[0].Colors[0] = "#FFFFFFFF";
        clone.Entries.RemoveAt(1);
        Assert.Equal("#80112233", original.Entries[0].Colors[0]);
        Assert.Equal(PaletteSet.Size, original.Entries.Count);
    }

    [Fact]
    public void ShuffleKeepsLockedSamplesAndReplacesUnlockedSamples()
    {
        var set = TestData.Palettes();
        var before = set.Entries.ToArray();
        set.Shuffle([PaletteFamily.Earth], [BackgroundTone.Light]);
        Assert.True(set.IsValid());
        for (var i = 0; i < PaletteSet.Size; i++)
        {
            if (before[i].Keep)
                Assert.Same(before[i], set.Entries[i]);
            else
            {
                Assert.NotSame(before[i], set.Entries[i]);
                Assert.Equal(PaletteFamily.Earth, set.Entries[i].Family);
                Assert.Equal(BackgroundTone.Light, set.Entries[i].Tone);
                Assert.False(set.Entries[i].Keep);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShuffleWithoutFamiliesOrTonesLeavesTheSetIntact(bool noFamilies)
    {
        var set = TestData.Palettes();
        var before = set.Entries.ToArray();
        set.Shuffle(noFamilies ? [] : [PaletteFamily.Earth], noFamilies ? [BackgroundTone.Light] : []);
        for (var i = 0; i < before.Length; i++)
            Assert.Same(before[i], set.Entries[i]);
    }

    [Fact]
    public void SettingsRoundTripPreservesPaletteOptInAndOwnsColorsAfterClone()
    {
        var settings = new GeneratorSettings { UsePaletteSet = true, PaletteSet = TestData.Palettes() };
        var loaded = SettingsJson.Deserialize(SettingsJson.Serialize(settings));
        Assert.True(loaded.UsePaletteSet);
        Assert.True(loaded.PaletteSet.IsValid());
        Assert.Equal(settings.PaletteSet.Entries[0].Colors, loaded.PaletteSet.Entries[0].Colors);
        var clone = loaded.Clone();
        clone.PaletteSet.Entries[0].Colors[0] = "#FFFFFFFF";
        Assert.Equal("#80112233", loaded.PaletteSet.Entries[0].Colors[0]);
    }

    [Theory]
    [InlineData("{\"UsePaletteSet\":true}")]
    [InlineData("{\"PaletteSet\":null}")]
    [InlineData("{\"PaletteSet\":{\"Entries\":null}}")]
    public void SettingsRejectInvalidPaletteSets(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node[nameof(GeneratorSettings.FormatVersion)] = GeneratorSettings.CurrentFormatVersion;
        Assert.Throws<JsonException>(() => SettingsJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void RecipeUsesTheSavedPaletteWhenOptedIn()
    {
        var settings = new GeneratorSettings
        {
            UsePaletteSet = true,
            PaletteSet = TestData.Palettes(),
            Palettes = [],
            Tones = [],
            SpreadDates = false
        };
        var recipe = ImageGenerator.CreateRecipe(settings, [new TruchetStyle()], settings.BuildSizePool(),
            42, 1, new DateTime(2026, 1, 2));
        var sample = Assert.IsType<PaletteSample>(recipe.SamplePalette);
        Assert.Contains(sample, settings.PaletteSet.Entries);
        Assert.Equal(sample.Family, recipe.Family);
        Assert.Equal(sample.Tone, recipe.Tone);
        var palette = ImageGenerator.CreatePalette(recipe);
        Assert.Equal(sample.ToPalette().Colors, palette.Colors);
        Assert.Equal(sample.ToPalette().Background, palette.Background);
    }
}
