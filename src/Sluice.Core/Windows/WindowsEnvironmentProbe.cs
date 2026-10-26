using System.Text.Json;

namespace Sluice.Windows;

/// <summary>What the machine looks like from the deployment stack's point of view.</summary>
/// <param name="IsWindows">True when running on Windows.</param>
/// <param name="Architecture">Machine architecture.</param>
/// <param name="ProductName">Windows product name.</param>
/// <param name="Build">Windows build number.</param>
/// <param name="DisplayVersion">Windows release, for example 24H2.</param>
/// <param name="SideloadingEnabled">Whether non-Store apps may be installed.</param>
/// <param name="DeveloperModeEnabled">Whether Developer Mode is on.</param>
/// <param name="Services">Deployment service states by name.</param>
/// <param name="IsElevated">Whether the current process is elevated.</param>
/// <param name="PowerShellPath">PowerShell executable that will be used.</param>
/// <param name="FreeDiskBytes">Free space on the system drive.</param>
/// <param name="Warnings">Things that break an install until they are changed.</param>
/// <param name="Notes">Advisory findings: worth knowing, never a reason to refuse an install.</param>
public sealed record EnvironmentReport(
    bool IsWindows,
    string Architecture,
    string? ProductName,
    string? Build,
    string? DisplayVersion,
    bool? SideloadingEnabled,
    bool? DeveloperModeEnabled,
    IReadOnlyDictionary<string, string?> Services,
    bool? IsElevated,
    string? PowerShellPath,
    long FreeDiskBytes,
    IReadOnlyList<Diagnostics.DiagnosticMessage> Warnings,
    IReadOnlyList<Diagnostics.DiagnosticMessage> Notes);

/// <summary>
/// Checks the parts of Windows that actually decide whether an appx install will succeed:
/// sideloading policy, Developer Mode, the deployment services and elevation. Most "the package
/// could not be registered" reports come down to one of these.
/// </summary>
public static class WindowsEnvironmentProbe
{
    /// <summary>Services that appx deployment depends on.</summary>
    public static IReadOnlyList<string> RequiredServices { get; } = ["AppXSvc", "StateRepository", "ClipSVC", "InstallService"];

    /// <summary>Probes the machine.</summary>
    /// <param name="shell">PowerShell host to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<EnvironmentReport> ProbeAsync(PowerShellHost shell, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shell);

        var architecture = Packages.WindowsArchitecture.Current;
        if (!OperatingSystem.IsWindows())
        {
            return new EnvironmentReport(
                false,
                architecture,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                null,
                shell.Executable,
                0,
                [Diagnostics.DiagnosticMessage.Of("EnvNotWindows")],
                []);
        }

        var script = """
            $ErrorActionPreference = 'SilentlyContinue'
            $version = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
            $unlock = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
            $services = [ordered]@{}
            foreach ($name in @('AppXSvc','StateRepository','ClipSVC','InstallService')) {
                $service = Get-Service -Name $name -ErrorAction SilentlyContinue
                $services[$name] = if ($null -ne $service) { $service.Status.ToString() } else { 'Missing' }
            }
            $elevated = $false
            try {
                $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
                $elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            } catch { }
            $drive = Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':')) -ErrorAction SilentlyContinue
            [pscustomobject]@{
                productName = $version.ProductName
                build = $version.CurrentBuild
                ubr = $version.UBR
                displayVersion = $version.DisplayVersion
                allowAllTrustedApps = $unlock.AllowAllTrustedApps
                allowDevelopmentWithoutDevLicense = $unlock.AllowDevelopmentWithoutDevLicense
                services = $services
                elevated = $elevated
                freeSpace = if ($null -ne $drive) { $drive.Free } else { 0 }
            } | ConvertTo-Json -Depth 4 -Compress
            """;

        var result = await shell.RunAsync(script, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return new EnvironmentReport(
                true,
                architecture,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                null,
                shell.Executable,
                0,
                [Diagnostics.DiagnosticMessage.Of("EnvProbeFailed", result.StandardError)],
                []);
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        return ParseReport(document.RootElement, architecture, shell.Executable);
    }

    /// <summary>
    /// Turns the JSON the probe script emits into a report. Internal so the mapping from the
    /// script's output to the report can be tested without a machine.
    /// </summary>
    /// <param name="root">Root of the probe payload.</param>
    /// <param name="architecture">Machine architecture.</param>
    /// <param name="powerShellPath">PowerShell the caller will use.</param>
    internal static EnvironmentReport ParseReport(JsonElement root, string architecture, string? powerShellPath)
    {
        var services = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("services", out var servicesElement) && servicesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in servicesElement.EnumerateObject())
            {
                services[property.Name] = property.Value.GetString();
            }
        }

        var sideloading = ReadBool(root, "allowAllTrustedApps");
        var developerMode = ReadBool(root, "allowDevelopmentWithoutDevLicense");
        var elevated = ReadBool(root, "elevated");
        var freeSpace = root.TryGetProperty("freeSpace", out var freeSpaceElement) && freeSpaceElement.TryGetInt64(out var space)
            ? space
            : 0;

        var warnings = new List<Diagnostics.DiagnosticMessage>();
        if (sideloading == false && developerMode != true)
        {
            warnings.Add(Diagnostics.DiagnosticMessage.Of("EnvSideloadingOff"));
        }

        // Developer Mode is not needed to deploy a signed package: it is needed for the loose file
        // fallback, which only runs after a registration failure. Reporting it as a warning made every
        // machine with the policy explicitly off exit 3, for a machine that can install perfectly well -
        // the exit code is documented as "the machine cannot deploy packages until the warnings are fixed".
        var notes = new List<Diagnostics.DiagnosticMessage>();
        if (developerMode == false)
        {
            notes.Add(Diagnostics.DiagnosticMessage.Of("EnvDevModeOff"));
        }

        foreach (var (name, status) in services)
        {
            if (status is null || string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // ClipSVC is start-on-demand and being idle is normal; the others must be running.
            if (string.Equals(name, "ClipSVC", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            warnings.Add(Diagnostics.DiagnosticMessage.Of("EnvServiceDown", name, status));
        }

        if (freeSpace > 0 && freeSpace < 2L * 1024 * 1024 * 1024)
        {
            warnings.Add(Diagnostics.DiagnosticMessage.Of(
                "EnvLowDisk", (freeSpace / 1024d / 1024d / 1024d).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
        }

        return new EnvironmentReport(
            true,
            architecture,
            ReadString(root, "productName"),
            ReadString(root, "build"),
            ReadString(root, "displayVersion"),
            sideloading,
            developerMode,
            services,
            elevated,
            powerShellPath,
            freeSpace,
            warnings,
            notes);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // The registry returns DWORD values, so ConvertTo-Json emits 1 and 0 rather than true and
    // false. Treating those as unknown made doctor report the policy as unreadable on every
    // machine where it is actually configured.
    private static bool? ReadBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            _ => null,
        };
    }
}
