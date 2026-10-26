using System.Globalization;

namespace Sluice.Store;

/// <summary>
/// Language tags as the Store spells them.
/// </summary>
/// <remarks>
/// The tag reaches three places - the catalog request, the delivery request and the resource package
/// selection - and each of them compares it against what Microsoft ships, which is a BCP 47 tag such as
/// <c>zh-CN</c> or <c>zh-Hans-CN</c>. A tag typed the POSIX way (<c>zh_CN</c>) matches none of them, so the
/// install arrives without its resource packages while every step reports success.
/// </remarks>
public static class LocaleTag
{
    /// <summary>The Windows display language, or <c>en-US</c> when the platform reports none.</summary>
    public static string Default { get; } =
        string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name) ? "en-US" : CultureInfo.CurrentUICulture.Name;

    /// <summary>
    /// Normalises what a person typed into a tag, or <c>null</c> when it is not a language tag at all.
    /// </summary>
    /// <param name="value">Candidate tag, as typed.</param>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Replace('_', '-');
        return IsLanguageTag(trimmed) ? trimmed : null;
    }

    /// <summary>True when the text has the shape of a language tag.</summary>
    /// <param name="value">Candidate tag.</param>
    public static bool IsValid(string? value) => Normalize(value) is not null;

    /// <summary>Normalises a tag, falling back to the display language.</summary>
    /// <param name="value">Candidate tag, as typed.</param>
    public static string NormalizeOrDefault(string? value) => Normalize(value) ?? Default;

    private static bool IsLanguageTag(string value)
    {
        var parts = value.Split('-');
        if (parts.Length is < 1 or > 5)
        {
            return false;
        }

        if (parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsAsciiLetter))
        {
            return false;
        }

        return parts
            .Skip(1)
            .All(part => part.Length is >= 2 and <= 8 && part.All(char.IsAsciiLetterOrDigit));
    }
}
