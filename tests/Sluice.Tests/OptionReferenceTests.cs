using System.Text.RegularExpressions;
using Sluice.Cli.Cli;
using Sluice.Diagnostics;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The tool explains a failure by naming the option that gets past it, and it used to advise
/// <c>sluice install --register</c> in four places - an option that does not exist, on a command line that
/// rejects options it does not take. Every switch the built-in help mentions has to exist.
/// </summary>
public sealed partial class OptionReferenceTests
{
    /// <summary>Sources whose text reaches a user: the window and the core's message templates.</summary>
    private static readonly string[] Sources =
    [
        Path.Combine("src", "Sluice.Desktop", "Localization.cs"),
        Path.Combine("src", "Sluice.Desktop", "ErrorExplainText.cs"),
        Path.Combine("src", "Sluice.Core", "Diagnostics", "DiagnosticMessage.cs"),
    ];

    [GeneratedRegex("--[a-z][a-z0-9-]*")]
    private static partial Regex OptionPattern();

    [Fact]
    public void EveryOptionTheErrorCatalogueNamesExists()
    {
        var text = string.Join(
            " ",
            AppxErrorCatalog.All.SelectMany(entry => new[] { entry.Meaning, entry.TypicalCause, entry.ManualSteps }));

        AssertNoUnknownOptions("the deployment error catalogue", text);
    }

    [Fact]
    public void EveryOptionTheWindowAndTemplatesNameExists()
    {
        // The window is a WPF project the test project cannot reference, so its strings are read from source.
        foreach (var source in Sources)
        {
            AssertNoUnknownOptions(source, File.ReadAllText(LocateRepositoryFile(source)));
        }
    }

    private static void AssertNoUnknownOptions(string where, string text)
    {
        var known = CommandCatalog.All
            .SelectMany(CommandCatalog.AcceptedOptionNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = OptionPattern()
            .Matches(text)
            .Select(match => match.Value[2..])
            .Where(name => !known.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            unknown.Count == 0,
            $"{where} names options no command accepts: {string.Join(", ", unknown.Select(name => "--" + name))}");
    }

    private static string LocateRepositoryFile(string relativePath)
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
