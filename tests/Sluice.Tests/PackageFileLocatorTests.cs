using Sluice.Cli.Cli;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// Whether an argument is a folder is asked before a Store reference is resolved. The check compared
/// against <see cref="Path.DirectorySeparatorChar"/>, which is <c>/</c> on Linux and macOS, where every
/// Store link contains one: <c>install --dry-run &lt;url&gt;</c> - the form the manual recommends on a
/// machine that cannot install - treated the link as a folder and stopped with "is neither an existing
/// file nor a directory".
/// </summary>
public sealed class PackageFileLocatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sluice-locator-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH")]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH?hl=zh-CN&gl=CN")]
    [InlineData("http://example.invalid/x")]
    [InlineData("ms-windows-store://pdp/?ProductId=9NKSQGP7F2NH")]
    public void AStoreLinkIsNeverAFolder(string value)
    {
        Assert.False(PackageFileLocator.LooksLikePath(value));
    }

    [Theory]
    [InlineData("cache\\packages")]
    [InlineData("cache/packages")]
    [InlineData(".\\downloads")]
    [InlineData("/tmp/cache")]
    public void ASeparatorMakesItAPath(string value)
    {
        Assert.True(PackageFileLocator.LooksLikePath(value));
    }

    [Fact]
    public void AnExistingFolderIsAPath()
    {
        Directory.CreateDirectory(_directory);

        Assert.True(PackageFileLocator.LooksLikePath(_directory));
    }

    [Fact]
    public void ABareNameIsNotAPath()
    {
        // A bare word with no separator and no folder behind it is a Store reference, not a path.
        Assert.False(PackageFileLocator.LooksLikePath("9NKSQGP7F2NH"));
        Assert.False(PackageFileLocator.LooksLikePath("cache-" + Guid.NewGuid().ToString("N")));
    }
}
