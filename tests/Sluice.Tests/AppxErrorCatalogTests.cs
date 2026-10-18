using Sluice.Diagnostics;
using Xunit;

namespace Sluice.Tests;

public sealed class AppxErrorCatalogTests
{
    [Theory]
    [InlineData("0x80073CF3", 0x80073CF3u)]
    [InlineData("80073CF3", 0x80073CF3u)]
    [InlineData("2147958003", 0x80073CF3u)]
    [InlineData("-2147024891", 0x80070005u)]
    public void ParsesHResultsInEveryShapeWindowsProduces(string text, uint expected)
    {
        Assert.True(AppxErrorCatalog.TryParse(text, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("0xZZ")]
    public void RejectsTextThatIsNotAnHResult(string text)
    {
        Assert.False(AppxErrorCatalog.TryParse(text, out _));
    }

    [Fact]
    public void CataloguesTheErrorsThatBreakSideloading()
    {
        uint[] expected =
        [
            0x80073CF3, 0x80073CF6, 0x80073CF9, 0x80073D02, 0x80073CFB,
            0x80073CFF, 0x800B0109, 0x80070005,
        ];

        foreach (var code in expected)
        {
            Assert.True(AppxErrorCatalog.TryLookup(code, out var info), $"{AppxErrorCatalog.Format(code)} is missing");
            Assert.False(string.IsNullOrWhiteSpace(info.Meaning));
            Assert.False(string.IsNullOrWhiteSpace(info.TypicalCause));
            Assert.False(string.IsNullOrWhiteSpace(info.ManualSteps));
        }
    }

    [Fact]
    public void MapsDependencyAndRegistrationFailuresToAutomaticRemedies()
    {
        Assert.True(AppxErrorCatalog.TryLookup(0x80073CF3u, out var dependency));
        Assert.Equal(DeploymentRemedy.SupplyDependencies, dependency.Remedy);

        Assert.True(AppxErrorCatalog.TryLookup(0x80073CF6u, out var registration));
        Assert.Equal(DeploymentRemedy.RegisterLooseFiles, registration.Remedy);

        Assert.True(AppxErrorCatalog.TryLookup(0x80073D02u, out var inUse));
        Assert.Equal(DeploymentRemedy.ForceApplicationShutdown, inUse.Remedy);
    }

    [Fact]
    public void FormatsHResultsLikeWindowsDoes()
    {
        Assert.Equal("0x80073CF3", AppxErrorCatalog.Format(0x80073CF3));
        Assert.Equal("0x00000000", AppxErrorCatalog.Format(0));
    }

    [Fact]
    public void LookupAcceptsFormattedText()
    {
        Assert.True(AppxErrorCatalog.TryLookup("0x80073D02", out var info));
        Assert.Equal("ERROR_PACKAGES_IN_USE", info.Name);
    }
}
