using Sluice.Packages;
using Sluice.Store;

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

    /// <summary>
    /// Reads the locale option, normalising the separator people actually type and rejecting values that
    /// are not language tags.
    /// </summary>
    /// <param name="arguments">Parsed command line.</param>
    /// <exception cref="FormatException">The value is not a language tag.</exception>
    /// <remarks>
    /// A tag typed the POSIX way (<c>zh_CN</c> instead of <c>zh-CN</c>) used to travel unchanged: the
    /// catalog answered in English, the resource packages - which carry a ResourceId of <c>zh-CN</c> -
    /// matched no locale, and the install arrived without its resources while every step reported success.
    /// A tag that is not a tag at all produced a request the delivery service answered with a fault, which
    /// read as an outage rather than as a typing mistake.
    /// </remarks>
    public static string Locale(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var value = arguments.Get("locale");
        if (value is null)
        {
            return LocaleTag.Default;
        }

        // The normalising and the shape check live in the core, because the window has a free text field
        // for the same value and used to keep the silent resource loss this command line no longer has.
        return LocaleTag.Normalize(value)
            ?? throw new FormatException(
                $"'{value}' is not a language tag; use one like zh-CN, zh-Hans-CN or en-US");
    }
}
