using Sluice.Cli.Commands;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// Every <c>--json</c> document answers with camelCase keys. Documents used to be written with whatever
/// name the anonymous type or record carried, so <c>doctor --json</c> emitted <c>IsWindows</c>,
/// <c>plan --json</c> emitted <c>issues[].Message.Code</c> and <c>install --dry-run --json</c> emitted
/// <c>MainPackageName</c>, next to hand written documents that said <c>succeeded</c> and <c>kind</c>. A
/// script had to know which document it was reading before it could name a field.
/// </summary>
public sealed class CliJsonShapeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sluice-json-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task TheDoctorDocumentUsesCamelCaseKeys()
    {
        await AssertCamelCase(() => DoctorCommand.RunAsync(["--json"]));
    }

    [Fact]
    public async Task ThePlanDocumentUsesCamelCaseKeys()
    {
        Directory.CreateDirectory(_directory);
        PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.Notes.appx", "Contoso.Notes");

        await AssertCamelCase(() => PlanCommand.RunAsync([_directory, "--json"]));
    }

    [Fact]
    public async Task TheDryRunDocumentUsesCamelCaseKeys()
    {
        Directory.CreateDirectory(_directory);
        PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.Notes.appx", "Contoso.Notes");

        await AssertCamelCase(() => InstallCommand.RunAsync([_directory, "--dry-run", "--json"]));
    }

    [Fact]
    public async Task TheAnalysisAndExplanationDocumentsUseCamelCaseKeys()
    {
        Directory.CreateDirectory(_directory);
        PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.Notes.appx", "Contoso.Notes");

        await AssertCamelCase(() => AnalyzeCommand.RunAsync([_directory, "--json"]));
        await AssertCamelCase(() => Task.FromResult(ExplainCommand.RunAsync(["0x80073CF3", "--json"])));
        await AssertCamelCase(() => Task.FromResult(ExplainCommand.RunAsync(["--list", "--json"])));
    }

    private static async Task AssertCamelCase(Func<Task<int>> run)
    {
        var result = await CliRunner.RunAsync(run).ConfigureAwait(false);
        var document = CliRunner.ReadDocument(result.StandardOutput);
        var offenders = new List<string>();
        Walk(document, offenders);

        Assert.True(
            offenders.Count == 0,
            $"these keys are not camelCase: {string.Join(", ", offenders.Distinct())}. It said: {result.StandardOutput.Trim()}");
    }

    private static void Walk(System.Text.Json.JsonElement element, List<string> offenders)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Length > 0 && char.IsUpper(property.Name[0]))
                    {
                        offenders.Add(property.Name);
                    }

                    if (string.Equals(property.Name, "services", StringComparison.Ordinal))
                    {
                        // A map of Windows service names to states: the names are data, not field names.
                        continue;
                    }

                    Walk(property.Value, offenders);
                }

                break;

            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, offenders);
                }

                break;
        }
    }
}
