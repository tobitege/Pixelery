namespace Pixelery.Cli;

/// <summary>Raised for invalid command line input; the message is shown to the user (exit code 2).</summary>
public sealed class UsageException(string message) : Exception(message);

/// <summary>
/// Minimal argument reader: positional values, <c>--option value</c>, <c>--option=value</c>, short aliases and flags.
/// Options must be consumed by the command; leftovers are reported as unknown.
/// </summary>
public sealed class ArgReader
{
    private readonly List<string> _positional = [];
    private readonly Dictionary<string, List<string?>> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _consumed = new(StringComparer.OrdinalIgnoreCase);

    public ArgReader(IEnumerable<string> args, IReadOnlyDictionary<string, string> aliases, IReadOnlySet<string> flags)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (arg == "--")
            {
                _positional.AddRange(list.Skip(i + 1));
                break;
            }
            if (!arg.StartsWith('-') || arg.Length == 1 || (char.IsDigit(arg[1]) && !arg.StartsWith("--", StringComparison.Ordinal)))
            {
                _positional.Add(arg);
                continue;
            }

            string name;
            string? value = null;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                name = arg[2..];
                var eq = name.IndexOf('=');
                if (eq >= 0)
                {
                    value = name[(eq + 1)..];
                    name = name[..eq];
                }
            }
            else
            {
                var shortName = arg[1..];
                if (!aliases.TryGetValue(shortName, out var longName))
                    throw new UsageException($"Unknown option '{arg}'.");
                name = longName;
            }

            if (value == null && !flags.Contains(name) && !name.StartsWith("no-", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= list.Count)
                    throw new UsageException($"Option '--{name}' needs a value.");
                value = list[++i];
            }

            if (!_options.TryGetValue(name, out var values))
                _options[name] = values = [];
            values.Add(value);
        }
    }

    public IReadOnlyList<string> Positional => _positional;

    public bool Has(string name)
    {
        _consumed.Add(name);
        return _options.ContainsKey(name);
    }

    /// <summary>Last value of an option, or null when absent.</summary>
    public string? Value(string name)
    {
        _consumed.Add(name);
        return _options.TryGetValue(name, out var values) ? values[^1] : null;
    }

    /// <summary>All comma-separated values of an option (it may be given several times).</summary>
    public List<string>? List(string name)
    {
        _consumed.Add(name);
        if (!_options.TryGetValue(name, out var values))
            return null;
        return [.. values.SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))];
    }

    public int? Int(string name, int min, int max)
    {
        var text = Value(name);
        if (text == null)
            return null;
        if (!int.TryParse(text.Replace("_", string.Empty), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            || value < min || value > max)
            throw new UsageException($"--{name} expects a whole number from {min} to {max}, got '{text}'.");
        return value;
    }

    /// <summary>True for <c>--name</c>, false for <c>--no-name</c>, null when neither is given.</summary>
    public bool? Switch(string name)
    {
        var on = Has(name);
        var off = Has("no-" + name);
        if (on && off)
            throw new UsageException($"Use either --{name} or --no-{name}, not both.");
        return on ? true : off ? false : null;
    }

    public void EnsureAllConsumed()
    {
        var unknown = _options.Keys.Where(k => !_consumed.Contains(k)).ToList();
        if (unknown.Count > 0)
            throw new UsageException($"Unknown option{(unknown.Count > 1 ? "s" : string.Empty)}: {string.Join(", ", unknown.Select(u => "--" + u))}. Use --help to see all options.");
    }
}
