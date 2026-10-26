using Sluice.Diagnostics;
using Sluice.Install;
using Xunit;

namespace Sluice.Tests;

public sealed class NotAttemptedStepsTests
{
    private static InstallStep Step(InstallStepKind kind, string file) =>
        new(kind, file, "Contoso.Notes", "1.0.0.0", "x64", DiagnosticMessage.Of("PlanStepMain"));

    [Fact]
    public void ReportsTheStepsAFailedRunNeverReached()
    {
        // The install stops at the first failure; the rest used to sit in the plan list as "waiting"
        // for ever, which reads as if the tool were still working.
        var framework = Step(InstallStepKind.InstallFramework, @"C:\packages\VCLibs.appx");
        var main = Step(InstallStepKind.InstallMain, @"C:\packages\app.msix");
        var verify = Step(InstallStepKind.Verify, @"C:\packages\app.msix");
        var plan = new InstallPlan("Contoso.Notes", "1.0.0.0", "x64", [framework, main, verify], []);

        var results = new[] { new InstallStepResult(framework, false, 0x80073CF3u, "failed", null, ["attempt"]) };

        var notAttempted = plan.NotAttempted(results);

        Assert.Single(notAttempted);
        Assert.Equal(InstallStepKind.InstallMain, notAttempted[0].Kind);
        Assert.DoesNotContain(notAttempted, step => step.Kind == InstallStepKind.Verify);
    }

    [Fact]
    public void ReportsNothingWhenEveryStepRan()
    {
        var main = Step(InstallStepKind.InstallMain, @"C:\packages\app.msix");
        var plan = new InstallPlan("Contoso.Notes", "1.0.0.0", "x64", [main], []);
        var results = new[]
        {
            new InstallStepResult(main, true, null, "ok", null, ["attempt"]),
            new InstallStepResult(Step(InstallStepKind.Verify, @"C:\packages\app.msix"), true, null, "ok", null, ["attempt"]),
        };

        Assert.Empty(plan.NotAttempted(results));
    }
}
