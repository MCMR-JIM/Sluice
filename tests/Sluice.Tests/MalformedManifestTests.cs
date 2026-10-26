using Sluice.Packages;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

public sealed class MalformedManifestTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-broken-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AManifestThatIsNotXmlIsReportedAsSuch()
    {
        var path = PackageFixtureBuilder.CreateMalformedPackage(_directory, "Contoso.Broken_1.0.0.0_x64.appx");

        var analysis = PackageAnalyzer.TryAnalyze(path, out var reason);

        Assert.Null(analysis);
        Assert.NotNull(reason);
        Assert.Contains("XML", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABundleWithAnUnreadableChildStillProducesAPlan()
    {
        // The child manifest used to escape as an XmlException and end the planning run with a stack
        // trace, instead of planning around one unusable child.
        var child = PackageFixtureBuilder.CreateMalformedPackage(_directory, "Contoso.Broken_1.0.0.0_x64.appx");
        var bundle = PackageFixtureBuilder.CreateBundleWithPayload(
            _directory,
            "Contoso.Broken_1.0.0.0.msixbundle",
            "Contoso.Broken",
            "1.0.0.0",
            child,
            "Contoso.Broken_1.0.0.0_x64.appx");

        var plan = await new Sluice.Install.InstallPlanner().CreatePlanAsync(
            [bundle],
            new Sluice.Install.InstallPlannerOptions("x64"));

        Assert.Contains(plan.Issues, issue => issue.Message.Code == "PlanBundleManifestUnreadable");
        Assert.Contains(plan.Steps, step => step.Kind == Sluice.Install.InstallStepKind.InstallMain);
    }
}
