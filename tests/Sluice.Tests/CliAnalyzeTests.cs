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
    public async Task AFolderWithoutAReadablePackageIsAFailureNotAnEmptySuccess()
    {
        // Every file was skipped and the command reported success with nothing to show, so a caller asking
        // "can this be installed?" was told yes.
        Directory.CreateDirectory(_directory);
        // A file the locator accepts but the analyzer cannot read: a zip without a package manifest.
        PackageFixtureBuilder.CreatePlainZip(_directory, "Contoso.Broken.appx");

        var run = await CliRunner.RunAsync(() => AnalyzeCommand.RunAsync([_directory, "--json"]));
        var document = CliRunner.ReadDocument(run.StandardOutput);

        Assert.Equal(1, run.ExitCode);
        Assert.False(document.GetProperty("succeeded").GetBoolean());
        Assert.Equal("unreadable-package", document.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task OneUnreadableFileDoesNotStopTheReadableOnes()
    {
        Directory.CreateDirectory(_directory);
        PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.Notes.appx", "Contoso.Notes");
        PackageFixtureBuilder.CreatePlainZip(_directory, "Contoso.Broken.appx");

        var run = await CliRunner.RunAsync(() => AnalyzeCommand.RunAsync([_directory, "--json"]));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(1, CliRunner.ReadDocument(run.StandardOutput).GetArrayLength());
        Assert.Contains("readable package", run.StandardError, StringComparison.OrdinalIgnoreCase);
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
