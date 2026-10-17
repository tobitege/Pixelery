using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pixelery.Core.Generation;

/// <summary>Values available to file name placeholders for one image.</summary>
public sealed record NameValues(
    int Index,
    int Total,
    string Style,
    string Category,
    int Width,
    int Height,
    string Ratio,
    string Orientation,
    string Palette,
    int Seed,
    DateTime Date,
    DateTime Now,
    string Format);

/// <summary>
/// File name schema with placeholders such as <c>{style}_{n:00000}</c>. Backslashes or slashes create subfolders.
/// </summary>
public sealed partial class NameTemplate
{
    public const int MaxPatternLength = 1024;
    /// <summary>Supported placeholders (token, sample format) for help texts and insert buttons.</summary>
    public static IReadOnlyList<(string Token, string Example)> Placeholders { get; } =
    [
        ("{n}", "{n:00000}"),
        ("{style}", "{style}"),
        ("{w}", "{w}"),
        ("{h}", "{h}"),
        ("{ratio}", "{ratio}"),
        ("{orient}", "{orient}"),
        ("{palette}", "{palette}"),
        ("{category}", "{category}"),
        ("{seed}", "{seed}"),
        ("{date}", "{date:yyyy-MM-dd}"),
        ("{now}", "{now:yyyyMMdd_HHmmss}"),
        ("{rand}", "{rand:6}"),
        ("{guid}", "{guid}"),
        ("{fmt}", "{fmt}"),
        ("{total}", "{total}")
    ];

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "n", "style", "w", "h", "ratio", "orient", "palette", "category", "seed", "date", "now", "rand", "guid", "fmt", "total"
    };

    private const string RandomAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    private readonly List<(string? Literal, string? Token, string? Format)> _parts;

    private NameTemplate(List<(string?, string?, string?)> parts) => _parts = parts;

    [GeneratedRegex(@"\{([A-Za-z]+)(?::([^{}]*))?\}")]
    private static partial Regex TokenRegex();

    public static bool TryParse(string? pattern, out NameTemplate? template, out string? error)
    {
        template = null;
        error = null;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            error = "empty";
            return false;
        }

        if (pattern.Length > MaxPatternLength)
        {
            error = $"> {MaxPatternLength}";
            return false;
        }
        if (pattern.Any(char.IsControl))
        {
            error = $"U+{(int)pattern.First(char.IsControl):X4}";
            return false;
        }

        var parts = new List<(string?, string?, string?)>();
        var last = 0;
        foreach (Match match in TokenRegex().Matches(pattern))
        {
            if (match.Index > last)
                parts.Add((pattern[last..match.Index], null, null));
            var token = match.Groups[1].Value.ToLowerInvariant();
            if (!Known.Contains(token))
            {
                error = match.Value;
                return false;
            }
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;
            if (format != null && !IsValidFormat(token, format))
            {
                error = match.Value;
                return false;
            }
            parts.Add((null, token, format));
            last = match.Index + match.Length;
        }
        if (last < pattern.Length)
            parts.Add((pattern[last..], null, null));

        if (parts.Any(p => p.Item1 != null && (p.Item1.Contains('{') || p.Item1.Contains('}'))))
        {
            error = "{ }";
            return false;
        }
        // Drive letters or rooted paths would escape the output folder.
        if (pattern.StartsWith('\\') || pattern.StartsWith('/') || parts.Any(p => p.Item1?.Contains(':') == true))
        {
            error = ":";
            return false;
        }

        var invalidSegment = pattern.Split(['\\', '/']).FirstOrDefault(s => (s.Trim() is "." or "..") || IsReservedName(s.Trim().TrimEnd('.')));
        if (parts.Any(p => p.Item1?.Any(InvalidChars.Contains) == true) || invalidSegment != null)
        {
            error = invalidSegment ?? pattern;
            return false;
        }

        template = new NameTemplate(parts);
        return true;
    }

    private static bool IsValidFormat(string token, string format)
    {
        // Numeric standard formats accept enormous precision values; reject them before formatting allocates memory.
        if (format.Length > 128 || ((token is "n" or "total" or "w" or "h")
            && format.Length > 1 && char.IsLetter(format[0]) && format[1..].All(char.IsDigit)
            && (!int.TryParse(format[1..], out var precision) || precision > 128)))
            return false;
        try
        {
            switch (token)
            {
                case "n" or "total" or "w" or "h":
                    _ = 1.ToString(format, CultureInfo.InvariantCulture);
                    return true;
                case "date" or "now":
                    _ = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
                    return true;
                case "rand":
                    return int.TryParse(format, out var length) && length is > 0 and <= 32;
                case "style" or "palette" or "category" or "orient":
                    return format is "upper" or "lower" or "title";
                case "seed":
                    return format is "x" or "d";
                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Replaces known placeholders in free text (watermarks); anything else stays as typed.</summary>
    public static string FormatText(string text, NameValues values, Random rng) =>
        TokenRegex().Replace(text, match =>
        {
            var token = match.Groups[1].Value.ToLowerInvariant();
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;
            if (!Known.Contains(token) || (format != null && !IsValidFormat(token, format)))
                return match.Value;
            return Resolve(token, format, values, rng);
        });

    /// <summary>True when every image gets a distinct name (contains {n}, {seed}, {rand} or {guid}).</summary>
    public bool IsUnique => _parts.Any(p => p.Token is "n" or "seed" or "rand" or "guid");

    /// <summary>Builds a relative path (without extension) with every segment made safe for Windows.</summary>
    public string Format(NameValues values, Random rng)
    {
        var sb = new StringBuilder();
        foreach (var (literal, token, format) in _parts)
        {
            if (literal != null)
            {
                sb.Append(literal);
                continue;
            }
            sb.Append(Resolve(token!, format, values, rng));
        }
        return Sanitize(sb.ToString());
    }

    private static string Resolve(string token, string? format, NameValues v, Random rng)
    {
        var inv = CultureInfo.InvariantCulture;
        return token switch
        {
            "n" => v.Index.ToString(format ?? "0", inv),
            "total" => v.Total.ToString(format ?? "0", inv),
            "w" => v.Width.ToString(format ?? "0", inv),
            "h" => v.Height.ToString(format ?? "0", inv),
            "ratio" => v.Ratio,
            "orient" => Case(v.Orientation, format),
            "style" => Case(v.Style, format),
            "palette" => Case(v.Palette, format),
            "category" => Case(v.Category, format),
            "seed" => format == "d" ? ((uint)v.Seed).ToString(inv) : ((uint)v.Seed).ToString("x8", inv),
            "date" => v.Date.ToString(format ?? "yyyyMMdd_HHmmss", inv),
            "now" => v.Now.ToString(format ?? "yyyyMMdd_HHmmss", inv),
            "rand" => RandomText(rng, int.TryParse(format, out var length) ? length : 6),
            "guid" => Guid.NewGuid().ToString("N"),
            "fmt" => v.Format,
            _ => string.Empty
        };
    }

    private static string Case(string text, string? format) => format switch
    {
        "upper" => text.ToUpperInvariant(),
        "lower" => text.ToLowerInvariant(),
        "title" => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.Replace('-', ' ')).Replace(" ", string.Empty),
        _ => text
    };

    private static string RandomText(Random rng, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = RandomAlphabet[rng.Next(RandomAlphabet.Length)];
        return new string(chars);
    }

    private static readonly char[] InvalidChars = [.. Path.GetInvalidFileNameChars().Where(c => c != '\\' && c != '/')];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static bool IsReservedName(string segment) => ReservedNames.Contains(segment.Split('.')[0]);

    /// <summary>Removes invalid characters, "." / ".." segments and reserved device names.</summary>
    public static string Sanitize(string relative)
    {
        var segments = relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s =>
            {
                var clean = new string([.. s.Select(c => InvalidChars.Contains(c) ? '_' : c)]).Trim().TrimEnd('.');
                if (clean is "" or "." or "..")
                    return null;
                return IsReservedName(clean) ? "_" + clean : clean;
            })
            .Where(s => s != null)
            .ToList();
        return segments.Count == 0 ? "image" : string.Join(Path.DirectorySeparatorChar, segments);
    }
}
