using System.Text.Json;
using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class WindowsEnvironmentProbeTests
{
    [Fact]
    public void ReadsSideloadingPolicyWhetherItArrivesAsBooleanOrDword()
    {
        // PowerShell turns registry DWORDs into 1 and 0, which used to be reported as unknown.
        var report = Parse("""
            {
              "productName": "Windows 11 Pro",
              "build": "26100",
              "allowAllTrustedApps": 1,
              "allowDevelopmentWithoutDevLicense": 0,
              "services": { "AppXSvc": "Running" },
              "elevated": true,
              "freeSpace": 90000000000
            }
            """);

        Assert.True(report.SideloadingEnabled);
        Assert.False(report.DeveloperModeEnabled);
        Assert.Equal("Windows 11 Pro", report.ProductName);
        Assert.Equal("26100", report.Build);
        Assert.DoesNotContain(report.Warnings, warning => warning.Text.Contains("Sideloading is disabled", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadsBooleanPoliciesToo()
    {
        var report = Parse("""{"allowAllTrustedApps": true, "allowDevelopmentWithoutDevLicense": true}""");

        Assert.True(report.SideloadingEnabled);
        Assert.True(report.DeveloperModeEnabled);
    }

    [Fact]
    public void WarnsWhenSideloadingIsOff()
    {
        var report = Parse("""{"allowAllTrustedApps": 0, "allowDevelopmentWithoutDevLicense": 0, "services": {}}""");

        Assert.Contains(report.Warnings, warning => warning.Text.Contains("0x80073CFF", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsUnknownWhenThePolicyIsAbsent()
    {
        var report = Parse("""{"services": {}}""");

        Assert.Null(report.SideloadingEnabled);
        Assert.Null(report.DeveloperModeEnabled);
    }

    [Fact]
    public void DeveloperModeOffIsNotAWarning()
    {
        // Developer Mode is what the loose file fallback needs, and that fallback only runs after a
        // registration failure. Reporting it as a warning made doctor exit 3 - which is documented as
        // "the machine cannot deploy packages until the warnings are fixed" - on machines that install
        // signed packages perfectly well, because the policy is explicitly off on most of them.
        var report = Parse("""{"allowAllTrustedApps": 1, "allowDevelopmentWithoutDevLicense": 0, "services": {}}""");

        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void DeveloperModeOffIsStillReported()
    {
        var report = Parse("""{"allowAllTrustedApps": 1, "allowDevelopmentWithoutDevLicense": 0, "services": {}}""");

        Assert.Contains(report.Notes, note => note.Text.Contains("Developer Mode", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnsAboutDeploymentServicesThatAreNotRunning()
    {
        var report = Parse("""
            {"allowAllTrustedApps": 1, "services": {"AppXSvc": "Stopped", "ClipSVC": "Stopped"}}
            """);

        Assert.Contains(report.Warnings, warning => warning.Text.Contains("AppXSvc", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Warnings, warning => warning.Text.Contains("ClipSVC", StringComparison.Ordinal));
    }

    private static EnvironmentReport Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return WindowsEnvironmentProbe.ParseReport(document.RootElement, "x64", "pwsh");
    }
}
