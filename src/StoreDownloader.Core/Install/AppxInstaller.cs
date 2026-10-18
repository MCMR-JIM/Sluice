using System.Text.Json;
using StoreDownloader.Diagnostics;
using StoreDownloader.Windows;

namespace StoreDownloader.Install;

/// <summary>Options for executing an install plan.</summary>
/// <param name="AllUsers">Install for every user (needs elevation).</param>
/// <param name="ForceShutdown">Ask Windows to shut down running instances of the app.</param>
/// <param name="ForceUpdateFromAnyVersion">Allow installing over a higher version.</param>
/// <param name="AllowRegisterFallback">Extract and register the manifest when staging fails.</param>
/// <param name="VerifyRegistration">Read the registration back after installing.</param>
public sealed record AppxInstallerOptions(
    bool AllUsers = false,
    bool ForceShutdown = true,
    bool ForceUpdateFromAnyVersion = false,
    bool AllowRegisterFallback = true,
    bool VerifyRegistration = true);

/// <summary>Outcome of one plan step.</summary>
/// <param name="Step">The step that ran.</param>
/// <param name="Succeeded">Whether it worked.</param>
/// <param name="HResult">Deployment HRESULT when it failed.</param>
/// <param name="Message">Message from Windows.</param>
/// <param name="Error">Catalogued explanation when the HRESULT is known.</param>
/// <param name="Attempts">Human readable log of what was tried.</param>
public sealed record InstallStepResult(
    InstallStep Step,
    bool Succeeded,
    uint? HResult,
    string? Message,
    AppxErrorInfo? Error,
    IReadOnlyList<string> Attempts);

/// <summary>Outcome of a whole installation.</summary>
/// <param name="Succeeded">True when every step succeeded.</param>
/// <param name="StepResults">Per step results, in execution order.</param>
/// <param name="Registered">Registration read back after the install, when verification ran.</param>
/// <param name="Issues">Plan issues carried through for reporting.</param>
public sealed record InstallOutcome(
    bool Succeeded,
    IReadOnlyList<InstallStepResult> StepResults,
    InstalledPackage? Registered,
    IReadOnlyList<PlanIssue> Issues);

/// <summary>
/// Executes an install plan with <c>Add-AppxPackage</c>, escalating through the known remedies
/// when Windows reports a specific deployment error, then reads the registration back.
/// </summary>
public sealed class AppxInstaller(PowerShellHost shell, AppxInventory inventory, AppxInstallerOptions? options = null)
{
    private readonly AppxInstallerOptions _options = options ?? new AppxInstallerOptions();

    /// <summary>Runs the plan.</summary>
    /// <param name="plan">Plan to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<InstallOutcome> InstallAsync(InstallPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsureWindows();

        var results = new List<InstallStepResult>();
        var dependencyPaths = plan.DependencyPaths;

        foreach (var step in plan.Steps.Where(step => step.Kind != InstallStepKind.Verify))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await RunStepAsync(step, dependencyPaths, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (!result.Succeeded)
            {
                return new InstallOutcome(false, results, null, plan.Issues);
            }
        }

        if (!_options.VerifyRegistration || plan.MainPackageName is null)
        {
            return new InstallOutcome(true, results, null, plan.Issues);
        }

        var registered = await ReadRegistrationAsync(plan, results, cancellationToken).ConfigureAwait(false);
        var succeeded = registered is not null
            && (plan.MainVersion is null
                || !System.Version.TryParse(plan.MainVersion, out var expected)
                || registered.GetVersion() >= expected);

        return new InstallOutcome(succeeded, results, registered, plan.Issues);
    }

    /// <summary>Reads a package registration back from the machine.</summary>
    /// <param name="plan">Plan whose main package should be verified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<InstalledPackage?> VerifyAsync(InstallPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.MainPackageName is null)
        {
            return null;
        }

        try
        {
            return await inventory.FindHighestAsync(plan.MainPackageName, cancellationToken).ConfigureAwait(false);
        }
        catch (PowerShellUnavailableException)
        {
            return null;
        }
    }

    private async Task<InstalledPackage?> ReadRegistrationAsync(
        InstallPlan plan,
        List<InstallStepResult> results,
        CancellationToken cancellationToken)
    {
        var registered = await VerifyAsync(plan, cancellationToken).ConfigureAwait(false);
        var verifyStep = plan.Steps.FirstOrDefault(step => step.Kind == InstallStepKind.Verify);
        var message = registered is null
            ? $"Nothing is registered for {plan.MainPackageName} after the install."
            : $"{registered.Name} {registered.Version} is registered with status {registered.Status}.";

        if (verifyStep is not null)
        {
            results.Add(new InstallStepResult(verifyStep, registered is not null, null, message, null, [message]));
        }

        return registered;
    }

    private async Task<InstallStepResult> RunStepAsync(
        InstallStep step,
        IReadOnlyList<string> dependencyPaths,
        CancellationToken cancellationToken)
    {
        var attempts = new List<string>();
        var forceShutdown = _options.ForceShutdown;
        var forceUpdate = _options.ForceUpdateFromAnyVersion;
        var supplyDependencies = step.Kind == InstallStepKind.InstallMain && dependencyPaths.Count > 0;

        for (var round = 0; round < 4; round++)
        {
            var script = BuildAddAppxPackageScript(
                step.FilePath,
                supplyDependencies ? dependencyPaths : [],
                forceShutdown,
                forceUpdate,
                _options.AllUsers);

            attempts.Add($"Add-AppxPackage {step.FileName}"
                + (supplyDependencies ? " with -DependencyPath" : string.Empty)
                + (forceShutdown ? " -ForceApplicationShutdown" : string.Empty)
                + (forceUpdate ? " -ForceUpdateFromAnyVersion" : string.Empty)
                + (_options.AllUsers ? " -AllUsers" : string.Empty));

            var attempt = await RunScriptAsync(script, cancellationToken).ConfigureAwait(false);
            if (attempt.Succeeded)
            {
                return new InstallStepResult(step, true, null, null, null, attempts);
            }

            if (attempt.HResult is null || !AppxErrorCatalog.TryLookup(attempt.HResult.Value, out var info))
            {
                return new InstallStepResult(step, false, attempt.HResult, attempt.Message, null, attempts);
            }

            switch (info.Remedy)
            {
                case DeploymentRemedy.ForceApplicationShutdown when !forceShutdown:
                    forceShutdown = true;
                    continue;

                case DeploymentRemedy.ForceUpdateFromAnyVersion when !forceUpdate:
                    forceUpdate = true;
                    continue;

                case DeploymentRemedy.SupplyDependencies when !supplyDependencies && dependencyPaths.Count > 0:
                    supplyDependencies = true;
                    continue;

                case DeploymentRemedy.RegisterLooseFiles when _options.AllowRegisterFallback:
                    attempts.Add($"Add-AppxPackage -Register (extracted) for {step.FileName}");
                    var registerAttempt = await RunScriptAsync(BuildRegisterScript(step.FilePath), cancellationToken).ConfigureAwait(false);
                    return registerAttempt.Succeeded
                        ? new InstallStepResult(step, true, null, null, info, attempts)
                        : new InstallStepResult(step, false, registerAttempt.HResult ?? attempt.HResult, registerAttempt.Message, info, attempts);

                default:
                    return new InstallStepResult(step, false, attempt.HResult, attempt.Message, info, attempts);
            }
        }

        return new InstallStepResult(step, false, null, "The install did not succeed after several attempts.", null, attempts);
    }

    private static string BuildAddAppxPackageScript(
        string path,
        IReadOnlyList<string> dependencyPaths,
        bool forceShutdown,
        bool forceUpdate,
        bool allUsers)
    {
        var arguments = new List<string> { "-Path " + Quote(path) };
        if (dependencyPaths.Count > 0)
        {
            arguments.Add("-DependencyPath " + string.Join(", ", dependencyPaths.Select(Quote)));
        }

        if (forceShutdown)
        {
            arguments.Add("-ForceApplicationShutdown");
        }

        if (forceUpdate)
        {
            arguments.Add("-ForceUpdateFromAnyVersion");
        }

        if (allUsers)
        {
            arguments.Add("-AllUsers");
        }

        // Add-AppxPackage reports the interesting deployment HRESULT inside the message text,
        // while $_.Exception.HResult usually carries a generic wrapper code, so prefer the former.
        return $$"""
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            try {
                Add-AppxPackage {{string.Join(" ", arguments)}}
                [pscustomobject]@{ ok = $true } | ConvertTo-Json -Compress
            } catch {
                $ex = $_.Exception
                $message = $ex.Message
                $code = [int64]$ex.HResult
                if ($code -lt 0) { $code += 4294967296 }
                if ($message -match '0x([0-9A-Fa-f]{8})') {
                    $code = [Convert]::ToInt64($Matches[1], 16)
                }
                [pscustomobject]@{ ok = $false; hresult = ('0x{0:X8}' -f $code); message = $message } | ConvertTo-Json -Compress
            }
            """;
    }

    private static string BuildRegisterScript(string path) => $$"""
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $destination = Join-Path ([System.IO.Path]::GetTempPath()) ('storedl-' + [guid]::NewGuid().ToString('N'))
        try {
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            try { Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop } catch { }
            Copy-Item -LiteralPath {{Quote(path)}} -Destination (Join-Path $destination 'package.zip') -Force
            Expand-Archive -LiteralPath (Join-Path $destination 'package.zip') -DestinationPath $destination -Force
            $manifest = Get-ChildItem -Path $destination -Filter AppxManifest.xml -Recurse | Select-Object -First 1
            if ($null -eq $manifest) { throw 'No AppxManifest.xml found after extraction.' }
            Add-AppxPackage -Register $manifest.FullName -DisableDevelopmentMode
            [pscustomobject]@{ ok = $true; extractedTo = $destination } | ConvertTo-Json -Compress
        } catch {
            $ex = $_.Exception
            $message = $ex.Message
            $code = [int64]$ex.HResult
            if ($code -lt 0) { $code += 4294967296 }
            if ($message -match '0x([0-9A-Fa-f]{8})') {
                $code = [Convert]::ToInt64($Matches[1], 16)
            }
            [pscustomobject]@{ ok = $false; hresult = ('0x{0:X8}' -f $code); message = $message } | ConvertTo-Json -Compress
        }
        """;

    private async Task<ScriptOutcome> RunScriptAsync(string script, CancellationToken cancellationToken)
    {
        var result = await shell.RunAsync(script, cancellationToken).ConfigureAwait(false);
        var output = result.StandardOutput.Trim();

        if (output.Length == 0)
        {
            var failure = string.IsNullOrWhiteSpace(result.StandardError) ? "no output" : result.StandardError;
            return new ScriptOutcome(false, null, $"PowerShell returned nothing ({failure}).");
        }

        var lastLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? output;
        try
        {
            using var document = JsonDocument.Parse(lastLine);
            var root = document.RootElement;
            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
            var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
            uint? hresult = root.TryGetProperty("hresult", out var hresultElement)
                && AppxErrorCatalog.TryParse(hresultElement.GetString(), out var parsed)
                    ? parsed
                    : null;

            return new ScriptOutcome(ok, hresult, message);
        }
        catch (JsonException)
        {
            return new ScriptOutcome(result.Succeeded, null, output);
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new StoreDownloaderException(
                "Installing packages needs Windows: it drives Add-AppxPackage through PowerShell. "
                + "Resolving and downloading work on any platform.");
        }
    }

    private sealed record ScriptOutcome(bool Succeeded, uint? HResult, string? Message);
}
