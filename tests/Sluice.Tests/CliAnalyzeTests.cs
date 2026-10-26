using Sluice.Cli.Commands;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// <c>analyze</c> takes <c>--arch</c> to say which child of a bundle Windows would install. The option was
/// read after the JSON branch, so in <c>--json</c> mode it was neither validated nor used: a misspelled
/// architecture produced a JSON document that silently ignored it, and the bundle selection it controls
/// was missing from the document altogether.
/// </summary>
public sealed class CliAnalyzeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sluice-analyze-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task AnUnusableArchitectureIsRejectedInJsonModeToo()
    {
        Directory.CreateDirectory(_directory);
        PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.Notes.appx", "Contoso.Notes");

        await Assert.ThrowsAsync<FormatException>(() => AnalyzeCommand.RunAsync([_directory, "--arch", "x46", "--json"]));
    }

    [Fact]
    public async Task AJsonAnalysisNamesTheChildForThisMachine()
    {
        Directory.CreateDirectory(_directory);
        var bundle = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.Notes.msixbundle",
            "Contoso.Notes",
            "1.0.0.0",
            ("Contoso.Notes_x64.msix", "application", "x64", null),
            ("Contoso.Notes_arm64.msix", "application", "arm64", null));

        var run = await CliRunner.RunAsync(() => AnalyzeCommand.RunAsync([bundle, "--arch", "arm64", "--json"]));
        var document = CliRunner.ReadDocument(run.StandardOutput);
        var analysis = document.ValueKind == System.Text.Json.JsonValueKind.Array
            ? document[0]
            : document;

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("arm64", analysis.GetProperty("forArchitecture").GetString());
        Assert.Equal("Contoso.Notes_arm64.msix", analysis.GetProperty("selectedChild").GetString());
    }
}
