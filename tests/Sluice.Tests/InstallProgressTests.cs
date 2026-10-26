using Sluice.Diagnostics;
using Sluice.Install;
using Xunit;

namespace Sluice.Tests;

public sealed class InstallProgressTests
{
    private static InstallStep Step(InstallStepKind kind, string file) =>
        new(kind, file, "Contoso.Notes", "1.0.0.0", "x64", DiagnosticMessage.Of("PlanStepMain"));

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 25)]
    [InlineData(3, 4, 75)]
    [InlineData(4, 4, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(9, 4, 100)]
    public void ReportsCompletionAsAPercentage(int completed, int total, double expected)
    {
        Assert.Equal(expected, InstallProgress.Percent(completed, total));
    }

    [Fact]
    public void NumbersTheStepsOfAPlanFromOne()
    {
        var framework = Step(InstallStepKind.InstallFramework, @"C:\packages\VCLibs.appx");
        var main = Step(InstallStepKind.InstallMain, @"C:\packages\app.msix");
        var plan = new InstallPlan("Contoso.Notes", "1.0.0.0", "x64", [framework, main], []);

        Assert.Equal(1, InstallProgress.StepNumber(plan, framework));
        Assert.Equal(2, InstallProgress.StepNumber(plan, main));
    }

    [Fact]
    public void SaysZeroForAStepThatIsNotInThePlan()
    {
        var plan = new InstallPlan("Contoso.Notes", "1.0.0.0", "x64", [Step(InstallStepKind.InstallMain, "a.msix")], []);

        Assert.Equal(0, InstallProgress.StepNumber(plan, Step(InstallStepKind.InstallMain, "other.msix")));
    }
}
