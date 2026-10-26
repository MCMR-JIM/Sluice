using System.Globalization;

namespace Sluice.Diagnostics;

/// <summary>What the tool can do about a deployment failure, when it can do anything at all.</summary>
public enum DeploymentRemedy
{
    /// <summary>Nothing automatic; the operator has to change the machine or the file.</summary>
    None,

    /// <summary>Re-run the install with <c>-ForceApplicationShutdown</c>.</summary>
    ForceApplicationShutdown,

    /// <summary>Re-run the install with <c>-ForceUpdateFromAnyVersion</c>.</summary>
    ForceUpdateFromAnyVersion,

    /// <summary>Re-run the install passing the planned dependencies explicitly.</summary>
    SupplyDependencies,

    /// <summary>Extract the package and register its manifest instead of staging it.</summary>
    RegisterLooseFiles,

    /// <summary>The machine must allow sideloading or development mode first.</summary>
    EnableSideloading,

    /// <summary>The failing package should be removed before installing again.</summary>
    RemoveConflictingPackage,
}

/// <summary>A known appx deployment HRESULT.</summary>
/// <param name="Code">The HRESULT value.</param>
/// <param name="Name">Symbolic name from the Windows headers.</param>
/// <param name="Meaning">What Windows is complaining about.</param>
/// <param name="TypicalCause">Why this happens in practice.</param>
/// <param name="Remedy">What the tool can try automatically.</param>
/// <param name="ManualSteps">What the operator can do when the automatic path does not help.</param>
public sealed record AppxErrorInfo(
    uint Code,
    string Name,
    string Meaning,
    string TypicalCause,
    DeploymentRemedy Remedy,
    string ManualSteps)
{
    /// <summary>Formatted HRESULT, for example <c>0x80073CF3</c>.</summary>
    public string FormattedCode => AppxErrorCatalog.Format(Code);
}

/// <summary>
/// The deployment errors that actually show up when sideloading Store packages, with what
/// they mean and what fixes them. This is the knowledge that normally lives in forum threads.
/// </summary>
public static class AppxErrorCatalog
{
    private static readonly Dictionary<uint, AppxErrorInfo> Entries = BuildEntries();

    /// <summary>Every catalogued error.</summary>
    public static IReadOnlyList<AppxErrorInfo> All { get; } = Entries.Values.OrderBy(entry => entry.Code).ToList();

    /// <summary>Formats an HRESULT the way Windows tools print it.</summary>
    /// <param name="code">HRESULT value.</param>
    public static string Format(uint code) => string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}");

    /// <summary>Looks up an HRESULT.</summary>
    /// <param name="code">HRESULT value.</param>
    /// <param name="info">Catalogued information when found.</param>
    public static bool TryLookup(uint code, out AppxErrorInfo info) => Entries.TryGetValue(code, out info!);

    /// <summary>Looks up an HRESULT by its formatted or decimal representation.</summary>
    /// <param name="text">Text such as <c>0x80073CF3</c> or <c>2147950323</c>.</param>
    /// <param name="info">Catalogued information when found.</param>
    public static bool TryLookup(string? text, out AppxErrorInfo info)
    {
        info = null!;
        if (!TryParse(text, out var code))
        {
            return false;
        }

        return TryLookup(code, out info);
    }

    /// <summary>Parses an HRESULT written in hex, with or without the <c>0x</c> prefix, or in decimal.</summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="code">Parsed value.</param>
    public static bool TryParse(string? text, out uint code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code);
        }

        // Bare eight digit values appear in event logs without the 0x prefix. A value with the top bit
        // set can only be hex: as a decimal it would sit well below the failure range, which is why
        // 80070005 had to be read as 0x80070005 (access denied) rather than as 80,070,005.
        if (trimmed.Length == 8
            && uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bare)
            && (bare >= 0x80000000
                || trimmed.Any(character => character is 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'a' or 'b' or 'c' or 'd' or 'e' or 'f')))
        {
            code = bare;
            return true;
        }

        if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
        {
            return true;
        }

        // Signed 32 bit representations such as -2147024891 show up in PowerShell output.
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
        {
            code = unchecked((uint)signed);
            return true;
        }

        return false;
    }

    private static Dictionary<uint, AppxErrorInfo> BuildEntries()
    {
        var entries = new[]
        {
            new AppxErrorInfo(
                0x80073CF3,
                "ERROR_PACKAGE_UPDATE_POLICY_VIOLATION",
                "The package could not be installed because it conflicts with a package that is already installed.",
                "Almost always a dependency problem: a framework the app needs (VCLibs, WindowsAppRuntime, .NET Native) "
                + "is missing, is older than the manifest requires, or is installed for a different architecture.",
                DeploymentRemedy.SupplyDependencies,
                "Install the matching framework package first (sluice downloads them automatically with --deps-only), "
                + "or remove the conflicting version with Get-AppxPackage -Name <name> | Remove-AppxPackage."),

            new AppxErrorInfo(
                0x80073CF6,
                "ERROR_PACKAGE_COULD_NOT_BE_REGISTERED",
                "The package was staged but could not be registered for the user.",
                "Typical after installing a bundle that was unpacked by hand, after a partial deployment, or when the "
                + "manifest no longer matches the staged files.",
                DeploymentRemedy.RegisterLooseFiles,
                "Enable Developer Mode and install again: after this failure the tool unpacks the package and "
                + "registers its manifest itself (turn that off with --no-register). That only works for a single "
                + "package file, not for a bundle - unpack the bundle and install the child package for this "
                + "architecture instead. If the registration is stale, remove it with Get-AppxPackage <name> | "
                + "Remove-AppxPackage and install again."),

            new AppxErrorInfo(
                0x80073CF9,
                "ERROR_DEPLOYMENT_FAILED",
                "The AppX Deployment Service could not complete the operation.",
                "The AppXSvc or StateRepository service is disabled or wedged, or the deployment cache is corrupt.",
                DeploymentRemedy.None,
                "Check that AppXSvc, ClipSVC and StateRepository are running (Get-Service AppXSvc,ClipSVC,StateRepository), "
                + "restart them, and if the state is corrupt run 'wsreset.exe' or DISM /Online /Cleanup-Image /RestoreHealth."),

            new AppxErrorInfo(
                0x80073D02,
                "ERROR_PACKAGES_IN_USE",
                "A package that must be replaced is currently in use.",
                "The app, or the framework package being updated, is still running.",
                DeploymentRemedy.ForceApplicationShutdown,
                "Close the app and any process using it, then install again."),

            new AppxErrorInfo(
                0x80073CFB,
                "ERROR_PACKAGE_ALREADY_INSTALLED",
                "This exact package (or a newer version) is already installed.",
                "The Store delivered a build that is already present, often after a partially completed upgrade.",
                DeploymentRemedy.ForceUpdateFromAnyVersion,
                "Nothing to do, or uninstall the existing package first and install again."),

            new AppxErrorInfo(
                0x80073CFF,
                "ERROR_INSTALL_POLICY_FAILURE",
                "Deployment policy blocked the installation of this package.",
                "Sideloading is disabled ('Install apps from any source') or Developer Mode is off, and the package is "
                + "not from the Store for this machine.",
                DeploymentRemedy.EnableSideloading,
                "Settings > System > For developers: enable Developer Mode, or set "
                + "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock\\AllowAllTrustedApps to 1."),

            new AppxErrorInfo(
                0x800B0109,
                "CERT_E_UNTRUSTEDROOT",
                "The package signature chains to a root certificate this machine does not trust.",
                "The package was re-signed, or the Microsoft root certificate is missing from the trusted store.",
                DeploymentRemedy.None,
                "Install the certificate the package was signed with into Trusted Root Certification Authorities, or use "
                + "the original Microsoft-signed package (sluice always downloads the original)."),

            new AppxErrorInfo(
                0x800B0100,
                "TRUST_E_NOSIGNATURE",
                "The package is not signed, so Windows refuses to deploy it.",
                "Unsigned or re-packed packages, including folders registered without a valid signature.",
                DeploymentRemedy.None,
                "Use the original signed package from the Store. Loose files that were unpacked by hand need "
                + "Developer Mode before Windows will register them at all."),

            new AppxErrorInfo(
                0x800B010A,
                "CERT_E_CHAINING",
                "The certificate chain of the package signature is invalid.",
                "A signature that was built for a different package, or a corrupted download.",
                DeploymentRemedy.None,
                "Verify the downloaded file digest (sluice verifies it automatically) and download it again."),

            new AppxErrorInfo(
                0x80070005,
                "E_ACCESSDENIED",
                "Access was denied while deploying the package.",
                "The package must be installed for all users, or a file it needs is locked by another account.",
                DeploymentRemedy.None,
                "Run the shell elevated, or install for the current user only (without -AllUsers)."),

            new AppxErrorInfo(
                0x80073CF0,
                "ERROR_PACKAGE_OPEN_FAILED",
                "The package could not be opened.",
                "The file is truncated, still downloading, or is not an appx/msix container at all.",
                DeploymentRemedy.None,
                "Check the digest and the file size against the resolution output and download again."),

            new AppxErrorInfo(
                0x80073CF1,
                "ERROR_PACKAGE_NOT_FOUND",
                "The package file was not found at the given path.",
                "The path moved between download and install, or a relative path resolved elsewhere.",
                DeploymentRemedy.None,
                "Re-run the install with the same --dir that was used for the download."),

            new AppxErrorInfo(
                0x80073CFD,
                "ERROR_PACKAGE_NOT_SUPPORTED_ON_THIS_DEVICE",
                "The package is not applicable to this device.",
                "Wrong architecture, wrong device family (for example an Xbox or Holographic package), or a package that "
                + "requires a newer Windows build.",
                DeploymentRemedy.None,
                "Install the build for this architecture (sluice picks it automatically) or update Windows."),

            new AppxErrorInfo(
                0x80070490,
                "ERROR_NOT_FOUND",
                "Deployment could not find a component it expected.",
                "A corrupt StateRepository database or a stale registration that points at files that no longer exist.",
                DeploymentRemedy.None,
                "DISM /Online /Cleanup-Image /RestoreHealth, then re-register or reinstall the affected packages."),

            new AppxErrorInfo(
                0x8007000B,
                "ERROR_BAD_FORMAT",
                "The image (package payload) is not valid.",
                "An architecture mismatch between the package and the machine, or a corrupted download.",
                DeploymentRemedy.None,
                "Install the architecture this machine actually runs, and let sluice verify the digest."),
        };

        var map = new Dictionary<uint, AppxErrorInfo>(entries.Length);
        foreach (var entry in entries)
        {
            map[entry.Code] = entry;
        }

        return map;
    }
}
