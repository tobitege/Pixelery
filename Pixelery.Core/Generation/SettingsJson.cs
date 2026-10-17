using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixelery.Core.Generation;

/// <summary>
/// Reads and writes <see cref="GeneratorSettings"/> as JSON (the app's generator.json format, enums as names).
/// </summary>
public static class SettingsJson
{
    /// <summary>
    /// Reads settings. With <paramref name="selectNewStyles"/>, styles added since the file was written are selected;
    /// without it the styles stay exactly as written, so a shared file gives the same images in a newer version.
    /// </summary>
    public static GeneratorSettings Deserialize(string json, bool selectNewStyles = true)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(nameof(GeneratorSettings.FormatVersion), out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var formatVersion) || formatVersion != GeneratorSettings.CurrentFormatVersion)
            throw new JsonException("Unsupported settings format version.");
        var settings = JsonSerializer.Deserialize(json, GeneratorJsonContext.Default.GeneratorSettings)
            ?? throw new JsonException("The settings file is empty.");
        if (settings.PaletteSet?.Entries == null || (settings.PaletteSet.Entries.Count != 0 && !settings.PaletteSet.IsValid())
            || (settings.UsePaletteSet && !settings.PaletteSet.IsValid()))
            throw new JsonException("Invalid palette set.");
        if (settings.AspectRatios == null || settings.Orientations == null || settings.LongEdges == null || settings.FixedSizes == null
            || !settings.CaptureSizeSelection().IsValid(settings.MultipleSizes)
            || settings.InactiveSizeSelection?.IsValid(!settings.MultipleSizes) != true
            || (settings.RandomSizes && !settings.MultipleSizes)
            || settings.CustomAspectRatios == null || settings.CustomAspectRatios.Any(r => !SizeCatalog.TryParseRatio(r, out _, out _))
            || settings.CustomLongEdges == null || settings.CustomLongEdges.Any(e => e is < SizeCatalog.MinEdge or > SizeCatalog.MaxEdge)
            || settings.CustomFixedSizes == null || settings.CustomFixedSizes.Any(s => !SizeCatalog.TryParseSize(s, out _, out _)))
            throw new JsonException("Invalid size selection.");
        if (!settings.HasValidDateRange())
            throw new JsonException("Invalid timestamp range.");
        var selectedPattern = settings.NamePattern?.Trim();
        if (selectedPattern == null || settings.NamePatterns == null || settings.NamePatterns.Count > GeneratorSettings.MaxNamePatterns
            || !NameTemplate.TryParse(selectedPattern, out _, out _))
            throw new JsonException("Invalid name pattern.");
        settings.NamePattern = selectedPattern;
        List<string> patterns = [];
        foreach (var value in settings.NamePatterns.Append(settings.NamePattern))
        {
            var pattern = value?.Trim();
            if (!NameTemplate.TryParse(pattern, out _, out _))
                throw new JsonException("Invalid saved name pattern.");
            if (!patterns.Contains(pattern!, StringComparer.Ordinal))
                patterns.Add(pattern!);
        }
        if (patterns.Count > GeneratorSettings.MaxNamePatterns)
            throw new JsonException("Too many saved name patterns.");
        settings.NamePatterns = patterns;
        if (selectNewStyles)
            settings.SelectNewStyles();
        return settings;
    }

    public static string Serialize(GeneratorSettings settings)
    {
        settings.KnownStyles = [.. Styles.StyleCatalog.All.Select(s => s.Id)];
        return JsonSerializer.Serialize(settings, GeneratorJsonContext.Default.GeneratorSettings);
    }

    public static GeneratorSettings Load(string path, bool selectNewStyles = true) => Deserialize(File.ReadAllText(path), selectNewStyles);

    /// <summary>Settings for another PC: without the output folder and the thread count, with the exporting app version.</summary>
    public static string Export(GeneratorSettings settings, string appVersion)
    {
        var copy = settings.Clone();
        copy.AppVersion = appVersion;
        copy.KnownStyles = [.. Styles.StyleCatalog.All.Select(s => s.Id)];
        var json = JsonSerializer.SerializeToNode(copy, GeneratorJsonContext.Default.GeneratorSettings)!.AsObject();
        json.Remove(nameof(GeneratorSettings.OutputFolder));
        json.Remove(nameof(GeneratorSettings.Threads));
        json.Remove(nameof(GeneratorSettings.NamePatterns));
        return json.ToJsonString(GeneratorJsonContext.Default.Options);
    }

    /// <summary>Reads exported settings: styles exactly as written, output folder and thread count from <paramref name="local"/>.</summary>
    public static GeneratorSettings Import(string json, GeneratorSettings local)
    {
        // Any JSON object would read as default settings; a file without styles is not a Pixelery settings file.
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(nameof(GeneratorSettings.Styles), out _))
                throw new JsonException("Not a Pixelery settings file.");
        }
        var settings = Deserialize(json, selectNewStyles: false);
        settings.OutputFolder = local.OutputFolder;
        settings.Threads = local.Threads;
        settings.NamePatterns = [.. local.NamePatterns];
        if (!settings.TryAddNamePattern(settings.NamePattern))
            throw new JsonException("Cannot add the imported name pattern.");
        return settings;
    }

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
