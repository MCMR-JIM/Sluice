namespace StoreDownloader.Cli.Cli;

/// <summary>
/// Minimal, dependency free command line parser: positionals plus <c>--name value</c>,
/// <c>--name=value</c> and boolean switches.
/// </summary>
public sealed class CommandLineArguments
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    private CommandLineArguments(IReadOnlyList<string> positionals) => Positionals = positionals;

    /// <summary>Positional arguments in order.</summary>
    public IReadOnlyList<string> Positionals { get; }

    /// <summary>All options that were supplied.</summary>
    public IReadOnlyDictionary<string, string?> Options => _options;

    /// <summary>Parses a raw argument list.</summary>
    /// <param name="args">Arguments following the command name.</param>
    public static CommandLineArguments Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var positionals = new List<string>();
        var parsed = new CommandLineArguments(positionals);
        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (current is null)
            {
                continue;
            }

            if (current == "--")
            {
                while (enumerator.MoveNext())
                {
                    positionals.Add(enumerator.Current);
                }

                break;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                var body = current[2..];
                var separator = body.IndexOf('=', StringComparison.Ordinal);
                if (separator > 0)
                {
                    parsed._options[body[..separator]] = body[(separator + 1)..];
                    continue;
                }

                if (enumerator.MoveNext())
                {
                    var next = enumerator.Current;
                    if (next is not null && !next.StartsWith("--", StringComparison.Ordinal))
                    {
                        parsed._options[body] = next;
                        continue;
                    }

                    parsed._options[body] = null;
                    positionals.Add(next!);
                    continue;
                }

                parsed._options[body] = null;
                continue;
            }

            if (current.StartsWith('-') && current.Length == 2)
            {
                parsed._options[current[1..]] = null;
                continue;
            }

            positionals.Add(current);
        }

        return parsed;
    }

    /// <summary>Returns an option value when present and non empty.</summary>
    /// <param name="name">Option name without dashes.</param>
    public string? Get(string name) =>
        _options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>True when the option was supplied at all.</summary>
    /// <param name="name">Option name without dashes.</param>
    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>Returns an option as an integer.</summary>
    /// <param name="name">Option name without dashes.</param>
    /// <param name="fallback">Value returned when the option is missing or unparsable.</param>
    public int GetInt32(string name, int fallback) =>
        int.TryParse(Get(name), out var parsed) ? parsed : fallback;
}
