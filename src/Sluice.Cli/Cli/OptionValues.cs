using Sluice.Packages;

namespace Sluice.Cli.Cli;

/// <summary>Reads option values that have a closed set of valid answers.</summary>
internal static class OptionValues
{
    /// <summary>
    /// Reads the architecture option, rejecting values the tool cannot target. A misspelled
    /// architecture used to travel all the way into a selection that matched nothing, which reported a
    /// product problem instead of a typing problem.
    /// </summary>
    /// <param name="arguments">Parsed command line.</param>
    /// <exception cref="FormatException">The value is not an architecture.</exception>
    public static string Architecture(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var value = arguments.Get("arch");
        if (value is null)
        {
            return WindowsArchitecture.Current;
        }

        if (!WindowsArchitecture.IsTargetable(value))
        {
            throw new FormatException(
                $"'{value}' is not an architecture; use one of {string.Join(", ", WindowsArchitecture.Targetable)}");
        }

        return WindowsArchitecture.Normalize(value);
    }
}
