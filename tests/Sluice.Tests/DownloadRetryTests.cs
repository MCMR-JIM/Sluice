using Sluice.Download;
using Xunit;

namespace Sluice.Tests;

public sealed class DownloadRetryTests
{
    [Fact]
    public void NetworkFailuresAreWorthAnotherAttempt()
    {
        Assert.True(PackageDownloader.IsTransient(new HttpRequestException("connection reset")));
    }

    [Fact]
    public void AFileAnotherProcessHoldsIsWorthAnotherAttempt()
    {
        // ERROR_SHARING_VIOLATION: a scanner or an indexer holding the file for a moment.
        Assert.True(PackageDownloader.IsTransient(new IOException("in use", unchecked((int)0x80070020))));
    }

    [Fact]
    public void AFullDiskIsReportedAtOnce()
    {
        // Waiting does not free space, so three attempts and six seconds of sleeping only delay the
        // same failure - and the message has to carry the reason.
        Assert.False(PackageDownloader.IsTransient(new IOException("disk full", unchecked((int)0x80070070))));
        Assert.False(PackageDownloader.IsTransient(new IOException("disk full", unchecked((int)0x80070027))));
    }

    [Fact]
    public void FailuresThatCannotClearUpAreReportedAtOnce()
    {
        Assert.False(PackageDownloader.IsTransient(new PathTooLongException()));
        Assert.False(PackageDownloader.IsTransient(new DirectoryNotFoundException()));
        Assert.False(PackageDownloader.IsTransient(new FileNotFoundException()));
        Assert.False(PackageDownloader.IsTransient(new UnauthorizedAccessException()));
    }

    [Fact]
    public void AnythingElseIsNotRetried()
    {
        Assert.False(PackageDownloader.IsTransient(null));
        Assert.False(PackageDownloader.IsTransient(new InvalidOperationException()));
        Assert.False(PackageDownloader.IsTransient(new DownloadException("bad digest")));
    }
}
