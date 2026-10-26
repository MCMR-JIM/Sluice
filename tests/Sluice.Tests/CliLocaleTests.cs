using Sluice.Cli.Cli;
using Sluice.Cli.Commands;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The locale reaches three places: the catalog request, the delivery request and the resource package
/// selection. A tag typed the POSIX way (<c>zh_CN</c>) used to travel unchanged, matched no resource
/// package, and the install arrived without its resources while every step reported success.
/// </summary>
public sealed class CliLocaleTests
{
    [Fact]
    public async Task AFolderPlanKeepsTheResourcePackageWhenTheTagIsTypedWithAnUnderscore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "sluice-locale-" + Guid.NewGuid().ToString("N"));

        try
        {
            PackageFixtureBuilder.CreateAppPackage(folder, "Contoso.Notes.appx", "Contoso.Notes", architecture: "x64");
            PackageFixtureBuilder.CreateAppPackage(
                folder,
                "Contoso.Notes_zh-CN.appx",
                "Contoso.Notes",
                architecture: "neutral",
                resourceId: "zh-CN");

            var run = await CliRunner.RunAsync(() => PlanCommand.RunAsync([folder, "--locale", "zh_CN", "--json"]));
            var document = CliRunner.ReadDocument(run.StandardOutput);

            Assert.Equal(0, run.ExitCode);
            Assert.True(document.GetProperty("canProceed").GetBoolean());

            var kinds = document.GetProperty("steps")
                .EnumerateArray()
                .Select(step => step.GetProperty("kind").GetString() ?? string.Empty)
                .ToList();

            Assert.Contains("InstallResource", kinds);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task APlanWithoutALocaleStillPicksTheDisplayLanguageResources()
    {
        var folder = Path.Combine(Path.GetTempPath(), "sluice-locale-" + Guid.NewGuid().ToString("N"));

        try
        {
            PackageFixtureBuilder.CreateAppPackage(folder, "Contoso.Notes.appx", "Contoso.Notes", architecture: "x64");
            PackageFixtureBuilder.CreateAppPackage(
                folder,
                "Contoso.Notes_" + Defaults.Locale + ".appx",
                "Contoso.Notes",
                architecture: "neutral",
                resourceId: Defaults.Locale);

            var run = await CliRunner.RunAsync(() => PlanCommand.RunAsync([folder, "--json"]));
            var document = CliRunner.ReadDocument(run.StandardOutput);

            var kinds = document.GetProperty("steps")
                .EnumerateArray()
                .Select(step => step.GetProperty("kind").GetString() ?? string.Empty)
                .ToList();

            Assert.Contains("InstallResource", kinds);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
