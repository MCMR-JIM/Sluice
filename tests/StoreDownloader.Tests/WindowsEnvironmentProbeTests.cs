using System.Text.Json;
using StoreDownloader.Windows;
using Xunit;

namespace StoreDownloader.Tests;

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
        Assert.DoesNotContain(report.Warnings, warning => warning.Contains("Sideloading is disabled", StringComparison.Ordinal));
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

        Assert.Contains(report.Warnings, warning => warning.Contains("0x80073CFF", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsUnknownWhenThePolicyIsAbsent()
    {
        var report = Parse("""{"services": {}}""");

        Assert.Null(report.SideloadingEnabled);
        Assert.Null(report.DeveloperModeEnabled);
    }

    [Fact]
    public void WarnsAboutDeploymentServicesThatAreNotRunning()
    {
        var report = Parse("""
            {"allowAllTrustedApps": 1, "services": {"AppXSvc": "Stopped", "ClipSVC": "Stopped"}}
            """);

        Assert.Contains(report.Warnings, warning => warning.Contains("AppXSvc", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Warnings, warning => warning.Contains("ClipSVC", StringComparison.Ordinal));
    }

    private static EnvironmentReport Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return WindowsEnvironmentProbe.ParseReport(document.RootElement, "x64", "pwsh");
    }
}
