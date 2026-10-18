using System.Text.Json;

namespace Sluice.Windows;

/// <summary>An appx package that is already installed on the machine.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Version">Installed version.</param>
/// <param name="Architecture">Installed architecture.</param>
/// <param name="Status">Deployment status reported by Windows.</param>
/// <param name="PackageFullName">Full package name.</param>
/// <param name="IsFramework">True for framework packages.</param>
public sealed record InstalledPackage(
    string Name,
    string? Version,
    string? Architecture,
    string? Status,
    string? PackageFullName,
    bool IsFramework)
{
    /// <summary>Version parsed for comparisons.</summary>
    public Version GetVersion() =>
        System.Version.TryParse(Version, out var parsed) ? parsed : new System.Version(0, 0);
}

/// <summary>What the planner needs to know about already installed packages.</summary>
public interface IInstalledPackageInventory
{
    /// <summary>Lists installed framework packages.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<InstalledPackage>> GetFrameworksAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds the highest installed version of a package name.</summary>
    /// <param name="name">Package name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<InstalledPackage?> FindHighestAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the machine's installed package inventory through <c>Get-AppxPackage</c>, which is the
/// only reliable way to know whether a dependency actually needs installing.
/// </summary>
public sealed class AppxInventory(PowerShellHost shell) : IInstalledPackageInventory
{
    private const string SelectClause =
        "@{n='Name';e={$_.Name}},@{n='Version';e={$_.Version.ToString()}},"
        + "@{n='Architecture';e={$_.Architecture.ToString()}},@{n='Status';e={$_.Status.ToString()}},"
        + "@{n='PackageFullName';e={$_.PackageFullName}},@{n='IsFramework';e={$_.IsFramework}}";

    /// <inheritdoc />
    public Task<IReadOnlyList<InstalledPackage>> GetFrameworksAsync(CancellationToken cancellationToken = default) =>
        QueryAsync($"Get-AppxPackage -PackageTypeFilter Framework | Select-Object {SelectClause}", cancellationToken);

    /// <summary>Lists every installed package for the current user.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<InstalledPackage>> GetAllAsync(CancellationToken cancellationToken = default) =>
        QueryAsync($"Get-AppxPackage | Select-Object {SelectClause}", cancellationToken);

    /// <summary>Lists installed packages whose name starts with the given name.</summary>
    /// <param name="name">Package name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<InstalledPackage>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var escaped = name.Replace("'", "''", StringComparison.Ordinal);
        return QueryAsync($"Get-AppxPackage -Name '{escaped}*' | Select-Object {SelectClause}", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InstalledPackage?> FindHighestAsync(string name, CancellationToken cancellationToken = default)
    {
        var packages = await GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
        return packages
            .Where(package => string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(package => package.GetVersion())
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<InstalledPackage>> QueryAsync(string pipeline, CancellationToken cancellationToken)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            try {
                $result = @({{pipeline}})
                if ($result.Count -eq 0) { '[]' } else { ConvertTo-Json -InputObject $result -Depth 4 -Compress }
            } catch {
                [pscustomobject]@{ error = $_.Exception.Message } | ConvertTo-Json -Compress
            }
            """;

        var result = await shell.RunAsync(script, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new PowerShellUnavailableException(
                $"Get-AppxPackage failed (exit {result.ExitCode}): {Truncate(result.StandardError)}");
        }

        var output = result.StandardOutput.Trim();
        if (output.Length == 0)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error))
            {
                throw new PowerShellUnavailableException($"Get-AppxPackage failed: {error.GetString()}");
            }

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [Read(document.RootElement)];
            }

            return document.RootElement.EnumerateArray().Select(Read).ToList();
        }
        catch (JsonException ex)
        {
            throw new PowerShellUnavailableException($"Unexpected output from Get-AppxPackage: {Truncate(output)} ({ex.Message})");
        }
    }

    private static InstalledPackage Read(JsonElement element) =>
        new(
            element.TryGetProperty("Name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("Version", out var version) ? version.GetString() : null,
            element.TryGetProperty("Architecture", out var architecture) ? architecture.GetString() : null,
            element.TryGetProperty("Status", out var status) ? status.GetString() : null,
            element.TryGetProperty("PackageFullName", out var fullName) ? fullName.GetString() : null,
            element.TryGetProperty("IsFramework", out var framework) && framework.ValueKind == JsonValueKind.True);

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "...";
}
