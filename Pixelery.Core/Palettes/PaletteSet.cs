using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace Pixelery.Core.Palettes;

/// <summary>One saved sample, including its exact colors and its shuffle lock.</summary>
public sealed record PaletteSample(string Name, PaletteFamily Family, BackgroundTone Tone,
    string Background, string[] Colors, bool Keep = false)
{
    public Palette ToPalette() => new(Name, SKColor.Parse(Background), Colors.Select(SKColor.Parse).ToArray(), Tone);

    public static PaletteSample Create(Random rng, PaletteFamily family, BackgroundTone tone)
    {
        var palette = PaletteFactory.Create(rng, family, tone);
        return new(palette.Name, family, tone, palette.Background.ToString(),
            palette.Colors.Select(color => color.ToString()).ToArray());
    }

    internal bool IsValid() => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100
        && Enum.IsDefined(Family) && Enum.IsDefined(Tone)
        && ValidColor(Background) && Colors is { Length: >= 2 and <= 32 } && Colors.All(ValidColor);

    private static bool ValidColor(string? color) => color is { Length: 9 } && color[0] == '#'
        && color.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
}

/// <summary>The eight palettes displayed in the editor, portable as a JSON file.</summary>
public sealed class PaletteSet
{
    public const int Size = 8;

    public List<PaletteSample> Entries { get; set; } = [];

    public PaletteSet Clone() => new() { Entries = Entries.Select(entry => entry with { Colors = [.. entry.Colors] }).ToList() };

    public bool IsValid() => Entries is { Count: Size } && Entries.All(entry => entry != null && entry.IsValid());

    /// <summary>Replaces only unlocked slots. With no source colors the existing set stays intact.</summary>
    public void Shuffle(IReadOnlyList<PaletteFamily> families, IReadOnlyList<BackgroundTone> tones)
    {
        if (families.Count == 0 || tones.Count == 0)
            return;
        for (var i = 0; i < Size; i++)
        {
            if (i < Entries.Count && Entries[i].Keep)
                continue;
            var rng = Random.Shared;
            var sample = PaletteSample.Create(rng, families[rng.Next(families.Count)], tones[rng.Next(tones.Count)]);
            if (i < Entries.Count)
                Entries[i] = sample;
            else
                Entries.Add(sample);
        }
    }

    public static async Task<PaletteSet> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var set = JsonSerializer.Deserialize(json, PaletteSetJsonContext.Default.PaletteSet);
        if (set?.IsValid() != true)
            throw new JsonException("Expected eight palettes with valid names, families, tones and colors.");
        return set;
    }

    public async Task SaveAsync(string path)
    {
        if (!IsValid())
            throw new InvalidOperationException("Expected eight valid palettes.");
        var json = JsonSerializer.Serialize(this, PaletteSetJsonContext.Default.PaletteSet);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PaletteSet))]
internal partial class PaletteSetJsonContext : JsonSerializerContext;
