using Sluice.Diagnostics;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// Guards the one thing that cannot be tested across the project boundary: every diagnostic the core
/// layer can emit has a translation in the window. The window is a WPF project the test project cannot
/// reference, so the translation table is checked at the source level instead - which is exactly how the
/// missing entry for a newly added code would otherwise reach a user in the wrong language.
/// </summary>
public sealed class LocalizationCoverageTests
{
    [Fact]
    public void EveryDiagnosticCodeHasAChineseTranslation()
    {
        var source = File.ReadAllText(LocateDesktopLocalization());
        var missing = DiagnosticText.Codes
            .Where(code => !source.Contains($"\"Diag_{code}\"", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"these diagnostic codes have no Diag_ entry in the window: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryDiagnosticCodeHasAnEnglishTemplate()
    {
        Assert.All(DiagnosticText.Codes, code =>
            Assert.False(string.IsNullOrWhiteSpace(DiagnosticText.Format(code, [])), code));
    }

    private static string LocateDesktopLocalization()
    {
        // Walk up from the test binaries until the repository root shows up.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Sluice.Desktop", "Localization.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find src/Sluice.Desktop/Localization.cs above " + AppContext.BaseDirectory);
    }
}
