using Sluice.Install;
using Sluice.Tests.Support;
using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class InstallPlannerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-plan-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task InstallsFrameworksBeforeTheApplication()
    {
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));

        var plan = await new InstallPlanner().CreatePlanAsync([framework, app], new InstallPlannerOptions("x64"));

        Assert.True(plan.CanProceed);
        Assert.Equal("Contoso.Notes", plan.MainPackageName);
        Assert.Collection(
            plan.Steps,
            step => Assert.Equal(InstallStepKind.InstallFramework, step.Kind),
            step => Assert.Equal(InstallStepKind.InstallMain, step.Kind),
            step => Assert.Equal(InstallStepKind.Verify, step.Kind));
        Assert.Equal([framework], plan.DependencyPaths);
    }

    [Fact]
    public async Task ReportsMissingDependenciesThatHaveNoPackage()
    {
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.WindowsAppRuntime.2", "2.0.0.0"));

        var plan = await new InstallPlanner().CreatePlanAsync([app], new InstallPlannerOptions("x64"));

        Assert.False(plan.CanProceed);
        var issue = Assert.Single(plan.Issues, candidate => candidate.Severity == PlanIssueSeverity.Error);
        Assert.Contains("Microsoft.WindowsAppRuntime.2", issue.Message.Text, StringComparison.Ordinal);
        Assert.NotNull(issue.Remediation);
    }

    [Fact]
    public async Task AcceptsADependencyThatIsAlreadyInstalled()
    {
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));
        var inventory = new FakeInventory(
            new InstalledPackage("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", "Ok", "full", true));

        var plan = await new InstallPlanner(inventory).CreatePlanAsync([app], new InstallPlannerOptions("x64"));

        Assert.True(plan.CanProceed);
        Assert.DoesNotContain(plan.Steps, step => step.Kind == InstallStepKind.InstallFramework);
        Assert.Contains(plan.Issues, issue => issue.Message.Text.Contains("already installed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkipsAFrameworkThatIsInstalledEvenWhenTheFileIsPresent()
    {
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));
        var inventory = new FakeInventory(
            new InstalledPackage("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", "Ok", "full", true));

        var plan = await new InstallPlanner(inventory).CreatePlanAsync([framework, app], new InstallPlannerOptions("x64"));

        Assert.True(plan.CanProceed);
        Assert.Empty(plan.DependencyPaths);
        Assert.Contains(plan.Issues, issue => issue.Message.Text.Contains("skipping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WarnsWhenTheInventoryCannotBeRead()
    {
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));
        var inventory = new FakeInventory { Failure = new PowerShellUnavailableException("no powershell") };

        var plan = await new InstallPlanner(inventory).CreatePlanAsync([framework, app], new InstallPlannerOptions("x64"));

        Assert.True(plan.CanProceed);
        Assert.Single(plan.DependencyPaths);
        Assert.Contains(plan.Issues, issue => issue.Severity == PlanIssueSeverity.Warning);
    }

    [Fact]
    public async Task ReadsDependenciesOutOfTheBundle()
    {
        var framework = CreateFramework("Contoso.Framework", "1.0.0.0", "x64");
        var bundle = Path.Combine(_directory, "Contoso.Notes_2.0.0.0_neutral.msixbundle");
        var innerDirectory = Path.Combine(_directory, "inner");
        var childFile = "Contoso.Notes_2.0.0.0_x64.msix";
        var childPath = PackageFixtureBuilder.CreateAppPackage(
            innerDirectory,
            childFile,
            "Contoso.Notes",
            "2.0.0.0",
            "x64",
            dependencies: [("Contoso.Framework", "1.0.0.0")]);
        PackageFixtureBuilder.CreateBundleWithPayload(_directory, Path.GetFileName(bundle)!, "Contoso.Notes", "2.0.0.0", childPath, childFile);

        var plan = await new InstallPlanner().CreatePlanAsync([bundle, framework], new InstallPlannerOptions("x64"));

        Assert.True(plan.CanProceed);
        Assert.Equal("Contoso.Notes", plan.MainPackageName);
        Assert.Equal([framework], plan.DependencyPaths);
    }

    [Fact]
    public async Task RefusesFoldersWithoutAnApplicationPackage()
    {
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");

        await Assert.ThrowsAsync<SluiceException>(
            () => new InstallPlanner().CreatePlanAsync([framework], new InstallPlannerOptions("x64")));
    }

    [Fact]
    public async Task NotesFilesThatAreNotUsed()
    {
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64");
        var unrelated = CreateFramework("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "x64");

        var plan = await new InstallPlanner().CreatePlanAsync([app, unrelated], new InstallPlannerOptions("x64"));

        Assert.Contains(plan.Issues, issue => issue.Message.Text.Contains("not required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InstallsTheFrameworkForTheTargetArchitectureWhenOnlyAnotherIsInstalled()
    {
        // An x86 copy of the same framework is installed. An x64 app loads the x64 framework, so
        // skipping the x64 package here produced a main install that failed with 0x80073CF3 - a code
        // whose own explanation blames a missing framework.
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));
        var inventory = new FakeInventory(
            new InstalledPackage("Microsoft.VCLibs.140.00", "14.0.33728.0", "x86", "Ok", "full", true));

        var plan = await new InstallPlanner(inventory).CreatePlanAsync([framework, app], new InstallPlannerOptions("x64"));

        var step = Assert.Single(plan.Steps, candidate => candidate.Kind == InstallStepKind.InstallFramework);
        Assert.Equal("x64", step.Architecture);
    }

    [Fact]
    public async Task SkipsAFrameworkOnlyWhenTheInstalledCopyHasTheTargetArchitecture()
    {
        var framework = CreateFramework("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64");
        var app = CreateApp("Contoso.Notes", "2.0.0.0", "x64", ("Microsoft.VCLibs.140.00", "14.0.30035.0"));
        var inventory = new FakeInventory(
            new InstalledPackage("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", "Ok", "full", true));

        var plan = await new InstallPlanner(inventory).CreatePlanAsync([framework, app], new InstallPlannerOptions("x64"));

        Assert.DoesNotContain(plan.Steps, candidate => candidate.Kind == InstallStepKind.InstallFramework);
    }

    private string CreateFramework(string name, string version, string architecture) =>
        PackageFixtureBuilder.CreateAppPackage(
            _directory,
            $"{name}_{version}_{architecture}.appx",
            name,
            version,
            architecture,
            framework: true,
            withApplications: false);

    private string CreateApp(string name, string version, string architecture, params (string Name, string MinVersion)[] dependencies) =>
        PackageFixtureBuilder.CreateAppPackage(
            _directory,
            $"{name}_{version}_{architecture}.msix",
            name,
            version,
            architecture,
            dependencies: dependencies);
}
