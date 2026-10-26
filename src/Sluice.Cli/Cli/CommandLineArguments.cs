namespace Sluice.Cli.Cli;

/// <summary>
/// Minimal, dependency free command line parser: positionals plus <c>--name value</c>,
/// <c>--name=value</c> and boolean switches.
/// </summary>
public sealed class CommandLineArguments
{
    /// <summary>
    /// Switches that never take a value. Listing them keeps <c>sluice resolve --json 9NKS...</c>
    /// from reading the product id as the value of <c>--json</c>.
    /// </summary>
    private static readonly HashSet<string> ValueLessOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json", "yes", "dry-run", "force", "quiet", "verbose", "list", "help", "h",
        "all-arch", "all-architectures", "deps-only", "dependencies-only", "overwrite",
        "no-verify", "skip-download", "all-users", "no-register", "no-force-shutdown",
    };

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

        var tokens = args.Where(token => token is not null).ToList();
        var positionals = new List<string>();
        var parsed = new CommandLineArguments(positionals);

        for (var index = 0; index < tokens.Count; index++)
        {
            var current = tokens[index];

            if (current == "--")
            {
                positionals.AddRange(tokens.Skip(index + 1));
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

                if (!ValueLessOptions.Contains(body)
                    && index + 1 < tokens.Count
                    && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    parsed._options[body] = tokens[index + 1];
                    index++;
                    continue;
                }

                parsed._options[body] = null;
                continue;
            }

            if (current.Length == 2 && current[0] == '-')
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

    /// <summary>
    /// Lists the options that were supplied but are not in <paramref name="accepted"/>. A misspelled
    /// switch used to be accepted and ignored, so the command quietly did something else than asked.
    /// </summary>
    /// <param name="accepted">Option names the command understands, without leading dashes.</param>
    /// <returns>Supplied names that are not accepted, in the order they were typed.</returns>
    public IReadOnlyList<string> FindUnaccepted(IEnumerable<string> accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        var known = new HashSet<string>(accepted, StringComparer.OrdinalIgnoreCase);
        return _options.Keys.Where(name => !known.Contains(name)).ToList();
    }
}
