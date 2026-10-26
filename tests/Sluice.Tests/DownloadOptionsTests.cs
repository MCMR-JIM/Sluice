using Sluice.Download;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// How many files may be downloaded at once. One command clamped the value and the other did not, so
/// <c>--parallel 500</c> queued 500 files for <c>install</c> and 16 for <c>download</c>.
/// </summary>
public sealed class DownloadOptionsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(16, 16)]
    [InlineData(500, 16)]
    public void ParallelismIsClampedForEveryone(int requested, int expected)
    {
        Assert.Equal(expected, new DownloadOptions(MaxParallelism: requested).Parallelism);
    }

    [Fact]
    public void TheDefaultIsThree()
    {
        Assert.Equal(3, DownloadOptions.Default.Parallelism);
    }
}
