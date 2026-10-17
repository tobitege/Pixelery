using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixelery.Core.Generation;

/// <summary>
/// Reads and writes <see cref="GeneratorSettings"/> as JSON (the app's generator.json format, enums as names).
/// </summary>
public static class SettingsJson
{
    /// <summary>Reads settings; styles added since the file was written are selected.</summary>
    public static GeneratorSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize(json, GeneratorJsonContext.Default.GeneratorSettings)
            ?? throw new JsonException("The settings file is empty.");
        if (settings.PaletteSet?.Entries == null || (settings.PaletteSet.Entries.Count != 0 && !settings.PaletteSet.IsValid())
            || (settings.UsePaletteSet && !settings.PaletteSet.IsValid()))
            throw new JsonException("Invalid palette set.");
        settings.SelectNewStyles();
        return settings;
    }

    public static string Serialize(GeneratorSettings settings)
    {
        settings.KnownStyles = [.. Styles.StyleCatalog.All.Select(s => s.Id)];
        return JsonSerializer.Serialize(settings, GeneratorJsonContext.Default.GeneratorSettings);
    }

    public static GeneratorSettings Load(string path) => Deserialize(File.ReadAllText(path));

    public static void Save(string path, GeneratorSettings settings)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, Serialize(settings));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(GeneratorSettings))]
internal partial class GeneratorJsonContext : JsonSerializerContext
{
}
