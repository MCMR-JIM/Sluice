using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The plan list keeps a state per step, and the row is bound to the flags derived from it. A setter that
/// announces only some of them leaves the others showing their previous value - which is how a step the
/// run never reached kept the look it had before <see cref="IsSkipped"/> was announced. The window is a
/// WPF project the tests cannot reference, so the announcement is read from source, the same way the
/// localisation table is.
/// </summary>
public sealed class PlanRowBindingTests
{
    [Fact]
    public void AStateChangeAnnouncesEveryFlagDerivedFromIt()
    {
        var source = File.ReadAllText(Locate("src/Sluice.Desktop/ViewModels/PlanRow.cs"));
        var setter = Slice(source, "public string State", "public string StateLabel");

        foreach (var flag in new[] { "StateLabel", "IsDone", "IsFailed", "IsSkipped" })
        {
            Assert.Contains($"Raise(nameof({flag}))", setter, StringComparison.Ordinal);
        }
    }

    private static string Slice(string source, string from, string to)
    {
        var start = source.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' is not in the source any more");
        var end = source.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, $"'{to}' does not follow '{from}' any more");
        return source[start..end];
    }

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
