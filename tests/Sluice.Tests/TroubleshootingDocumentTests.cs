using System.Text.RegularExpressions;
using Sluice.Diagnostics;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// Keeps the troubleshooting table honest: the document people read when something failed has to name
/// every error the tool can explain, with the name the tool prints. Drift here means telling somebody
/// to look up a code in a table that does not list it.
/// </summary>
public sealed partial class TroubleshootingDocumentTests
{
    [GeneratedRegex(@"\|\s*`(0x[0-9A-Fa-f]{8})`\s*\|\s*([A-Z_0-9]+)\s*\|", RegexOptions.CultureInvariant)]
    private static partial Regex TableRow();

    [Fact]
    public void DocumentsEveryCataloguedErrorWithItsName()
    {
        // The code keeps its spelling: "0x" and "0X" are the same number but not the same string.
        var rows = TableRow().Matches(File.ReadAllText(Locate("troubleshooting.md")))
            .Select(match => (Code: match.Groups[1].Value, Name: match.Groups[2].Value))
            .ToList();

        Assert.True(
            rows.Count > 0,
            $"the table in docs/troubleshooting.md produced no rows at all: {Locate("troubleshooting.md")}");

        foreach (var entry in AppxErrorCatalog.All)
        {
            Assert.True(
                rows.Any(row =>
                    string.Equals(row.Code, entry.FormattedCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.Name, entry.Name, StringComparison.Ordinal)),
                $"{entry.FormattedCode} {entry.Name} is missing from docs/troubleshooting.md (parsed {rows.Count} rows)");
        }
    }

    [Fact]
    public void DocumentsNothingTheToolCannotExplain()
    {
        var known = AppxErrorCatalog.All.Select(entry => entry.FormattedCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = TableRow().Matches(File.ReadAllText(Locate("troubleshooting.md")))
            .Select(match => match.Groups[1].Value)
            .ToList();

        var unknown = rows.Where(code => !known.Contains(code)).ToList();
        Assert.True(unknown.Count == 0, $"documented but not catalogued: {string.Join(", ", unknown)}");
    }

    private static string Locate(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find docs/{fileName} above {AppContext.BaseDirectory}");
    }
}
