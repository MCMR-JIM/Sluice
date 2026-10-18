namespace Sluice.Install;

/// <summary>The kind of work a plan step performs.</summary>
public enum InstallStepKind
{
    /// <summary>Install a framework the application depends on.</summary>
    InstallFramework,

    /// <summary>Install the application itself.</summary>
    InstallMain,

    /// <summary>Install a resource package for the target locale.</summary>
    InstallResource,

    /// <summary>Read back the registration to prove the install worked.</summary>
    Verify,
}

/// <summary>One action in an install plan.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="FilePath">Package file the step installs.</param>
/// <param name="PackageName">Package identity name, when known.</param>
/// <param name="Version">Package version, when known.</param>
/// <param name="Architecture">Package architecture, when known.</param>
/// <param name="Reason">Why the step exists, in operator language.</param>
public sealed record InstallStep(
    InstallStepKind Kind,
    string FilePath,
    string? PackageName,
    string? Version,
    string? Architecture,
    string Reason)
{
    /// <summary>File name of the package.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <inheritdoc />
    public override string ToString() => $"{Kind}: {FileName}";
}

/// <summary>Severity of something the planner noticed.</summary>
public enum PlanIssueSeverity
{
    /// <summary>Informational note.</summary>
    Info,

    /// <summary>The plan can proceed but something looks wrong.</summary>
    Warning,

    /// <summary>The plan cannot work until this is resolved.</summary>
    Error,
}

/// <summary>Something the planner wants the operator to know.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">What was noticed.</param>
/// <param name="Remediation">What to do about it.</param>
public sealed record PlanIssue(PlanIssueSeverity Severity, string Message, string? Remediation = null);

/// <summary>A complete, ordered installation plan.</summary>
/// <param name="MainPackageName">Identity name of the application being installed.</param>
/// <param name="MainVersion">Version being installed.</param>
/// <param name="Architecture">Architecture the plan targets.</param>
/// <param name="Steps">Ordered steps: dependencies, application, resources, verification.</param>
/// <param name="Issues">Everything the planner noticed while building the plan.</param>
public sealed record InstallPlan(
    string? MainPackageName,
    string? MainVersion,
    string Architecture,
    IReadOnlyList<InstallStep> Steps,
    IReadOnlyList<PlanIssue> Issues)
{
    /// <summary>True when nothing blocking was found.</summary>
    public bool CanProceed => Issues.All(issue => issue.Severity != PlanIssueSeverity.Error);

    /// <summary>Steps that install something, in order.</summary>
    public IReadOnlyList<InstallStep> InstallSteps =>
        Steps.Where(step => step.Kind != InstallStepKind.Verify).ToList();

    /// <summary>Framework files the plan will install.</summary>
    public IReadOnlyList<string> DependencyPaths =>
        Steps.Where(step => step.Kind == InstallStepKind.InstallFramework).Select(step => step.FilePath).ToList();
}
