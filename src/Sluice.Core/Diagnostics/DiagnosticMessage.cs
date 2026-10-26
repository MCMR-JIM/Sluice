using System.Globalization;

namespace Sluice.Diagnostics;

/// <summary>
/// One thing the tool has to say: a stable code plus the values it is formatted with.
/// </summary>
/// <remarks>
/// The core layer keeps the English rendering so the command line output does not change, while a
/// front end can look the same code up in its own language instead of parsing a finished sentence.
/// A plain string converts implicitly, so messages that are only ever shown as written stay one
/// liners.
/// </remarks>
/// <param name="Code">Message code, or a ready made sentence when the message needs no translation.</param>
/// <param name="Arguments">Values the message template formats.</param>
public sealed record DiagnosticMessage(string Code, IReadOnlyList<object?> Arguments)
{
    /// <summary>Creates a coded message.</summary>
    /// <param name="code">Message code understood by the front end.</param>
    /// <param name="arguments">Values the template formats.</param>
    public static DiagnosticMessage Of(string code, params object?[] arguments) => new(code, arguments);

    /// <summary>The message in English.</summary>
    public string Text => DiagnosticText.Format(Code, Arguments);

    /// <summary>Wraps a ready made sentence.</summary>
    /// <param name="text">Sentence to show as written.</param>
    public static implicit operator DiagnosticMessage(string text) => new(text, []);

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>English templates for every coded diagnostic.</summary>
internal static class DiagnosticText
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal)
    {
        ["EnvNotWindows"] = "Not running on Windows: packages can be resolved and downloaded, but not installed.",
        ["EnvProbeFailed"] = "Could not read the Windows environment: {0}",
        ["EnvSideloadingOff"] = "Sideloading is disabled: packaging deployment will fail with 0x80073CFF. Enable Developer Mode or set HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock\\AllowAllTrustedApps to 1.",
        ["EnvDevModeOff"] = "Developer Mode is disabled: the -Register fallback for registration failures will not work.",
        ["EnvServiceDown"] = "The {0} service is {1}; deployment needs it running (Start-Service {0}).",
        ["EnvLowDisk"] = "Only {0} GiB free on the system drive; large packages may fail to stage.",
        ["BundleNoApplication"] = "The bundle contains no application package; it may only carry resources.",
        ["PlanNoMatchingArchitecture"] = "No application package in this folder is built for {0}; the plan uses the one that is there and Windows will probably refuse it.",
        ["BundleSelected"] = "Selected {0} for {1}.",
        ["BundleKeptResources"] = "Kept {0} resource package(s) for {1}.",
        ["PrepareNoApplication"] = "Bundle {0} has no application package for {1}.",
        ["PrepareInnerPackage"] = "{0} carries {1} for {2}.",
        ["PrepareUnreadableBundle"] = "Could not read the inner manifest of {0}: {1}",
        ["PrepareNotDeclared"] = "{0} is not declared by the app manifest; not downloading it.",
        ["PlanSkippedFile"] = "Skipped '{0}': it is not a readable appx/msix package.",
        ["PlanBundleNoApplication"] = "Bundle '{0}' has no application package for {1}.",
        ["PlanBundleNoApplicationHint"] = "Download the product again; the delivery service may not offer this architecture.",
        ["PlanBundleManifestUnreadable"] = "Could not read the manifest inside the bundle ({0}); falling back to the files in the folder.",
        ["PlanDependencyInstalled"] = "{0} {1} is already installed; no package needed.",
        ["PlanDependencySkipped"] = "{0} {1} is already installed; skipping {2}.",
        ["PlanMissingDependency"] = "Missing dependency '{0}' declared by {1}.",
        ["PlanMissingDependencyHint"] = "Download the product again without --only/--deps-only so the framework packages are fetched, or install that framework from the Microsoft Store.",
        ["PlanUnusedFile"] = "{0} is not required by this app and will not be installed.",
        ["PlanMainAlreadyInstalled"] = "{0} {1} is already installed. Use --force to install anyway.",
        ["PlanStepFramework"] = "Required by {0}: {1}",
        ["PlanStepMain"] = "Application package",
        ["PlanStepMainBundle"] = "Application bundle; Windows selects the matching architecture and resources",
        ["PlanStepResource"] = "Resource package for {0}",
        ["PlanStepVerify"] = "Read the registration back to prove the install worked",
        ["PlanInventoryUnreadable"] = "Could not read the installed package inventory ({0}); every dependency in the folder will be installed.",
        ["PlanInventoryCheckFailed"] = "Could not check whether {0} is installed ({1}).",
    };

    /// <summary>Formats a code in English; unknown codes are returned unchanged.</summary>
    /// <param name="code">Message code or ready made sentence.</param>
    /// <param name="arguments">Values the template formats.</param>
    public static string Format(string code, IReadOnlyList<object?> arguments) =>
        Templates.TryGetValue(code, out var template) && arguments.Count > 0
            ? string.Format(CultureInfo.InvariantCulture, template, arguments.ToArray())
            : Templates.TryGetValue(code, out var plain)
                ? plain
                : code;
}
