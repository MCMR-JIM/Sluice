using System.Text.RegularExpressions;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The window keeps two tables, one per language. A key that exists in only one of them is invisible in one
/// language - <c>Localization.Get</c> returns the key itself, so the user reads <c>ColumnRole</c> instead of
/// a column heading - and a template whose placeholders disagree with the other language throws while the
/// line is being rendered. Both are read from source, like the rest of the window.
/// </summary>
public sealed partial class LocalizationTableTests
{
    [GeneratedRegex(@"\[""([A-Za-z0-9_]+)""\]\s*=\s*""((?:[^""\\]|\\.)*)""")]
    private static partial Regex Entry();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void BothLanguagesDefineTheSameKeys()
    {
        var (chinese, english) = ReadTables();

        // The English table deliberately has no Diag_ entries: those come from the core layer, which
        // already renders English, and Localization.Text falls back to it.
        var missingInEnglish = chinese.Keys
            .Where(key => !key.StartsWith("Diag_", StringComparison.Ordinal) && !english.ContainsKey(key))
            .ToList();
        var missingInChinese = english.Keys
            .Where(key => !chinese.ContainsKey(key))
            .ToList();

        Assert.True(missingInEnglish.Count == 0, "keys only the Chinese table has: " + string.Join(", ", missingInEnglish));
        Assert.True(missingInChinese.Count == 0, "keys only the English table has: " + string.Join(", ", missingInChinese));
    }

    [Fact]
    public void BothLanguagesUseTheSamePlaceholders()
    {
        var (chinese, english) = ReadTables();
        var mismatched = new List<string>();

        foreach (var (key, chineseText) in chinese)
        {
            if (!english.TryGetValue(key, out var englishText))
            {
                continue;
            }

            var zh = Placeholders(chineseText);
            var en = Placeholders(englishText);
            if (!zh.SetEquals(en))
            {
                mismatched.Add($"{key} (zh: {{{string.Join(",", zh)}}} en: {{{string.Join(",", en)}}})");
            }
        }

        Assert.True(mismatched.Count == 0, "templates with different placeholders: " + string.Join("; ", mismatched));
    }

    private static (Dictionary<string, string> Chinese, Dictionary<string, string> English) ReadTables()
    {
        var source = File.ReadAllText(Locate("src/Sluice.Desktop/Localization.cs"));
        var englishStart = source.IndexOf("EnglishText", StringComparison.Ordinal);
        Assert.True(englishStart > 0, "the English table is not where it used to be");

        return (Read(source[..englishStart]), Read(source[englishStart..]));
    }

    private static Dictionary<string, string> Read(string table)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Entry().Matches(table))
        {
            entries[match.Groups[1].Value] = match.Groups[2].Value;
        }

        Assert.NotEmpty(entries);
        return entries;
    }

    private static HashSet<int> Placeholders(string template) =>
        Placeholder().Matches(template)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet();

    private static string Locate(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' above {AppContext.BaseDirectory}");
    }
}
