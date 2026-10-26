using Sluice.Download;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// A download run reports per file, and several files are in flight at once. The window used to show one
/// file's fraction, so the bar jumped to 100% and back as files finished and started.
/// </summary>
public sealed class DownloadProgressAggregateTests
{
    [Fact]
    public void ReportsTheWholeRunNotTheFileThatJustReported()
    {
        var aggregate = new DownloadProgressAggregate();

        var first = aggregate.Fraction(new DownloadProgress("a.appx", 50, 100));
        var second = aggregate.Fraction(new DownloadProgress("b.appx", 0, 300));

        Assert.Equal(0.5, first, 3);
        Assert.Equal(0.125, second, 3);
    }

    [Fact]
    public void NeverGoesBackwardsWhenAFileFinishesAndAnotherStarts()
    {
        var aggregate = new DownloadProgressAggregate();
        aggregate.Fraction(new DownloadProgress("a.appx", 100, 100));

        var afterSecondFileStarts = aggregate.Fraction(new DownloadProgress("b.appx", 0, 100));

        Assert.Equal(0.5, afterSecondFileStarts, 3);
    }

    [Fact]
    public void ForgetsEverythingOnReset()
    {
        var aggregate = new DownloadProgressAggregate();
        aggregate.Fraction(new DownloadProgress("a.appx", 100, 100));

        aggregate.Reset();

        Assert.Equal(0, aggregate.Count);
        Assert.Equal(0, aggregate.Fraction(new DownloadProgress("c.appx", 0, 0)), 3);
    }

    [Fact]
    public void IgnoresAReportWithoutASize()
    {
        var aggregate = new DownloadProgressAggregate();
        aggregate.Fraction(new DownloadProgress("a.appx", 100, 100));

        var unknown = aggregate.Fraction(new DownloadProgress("b.appx", 5, 0));

        Assert.Equal(1, unknown, 3);
    }
}
