using Xunit;

namespace StoreDownloader.Tests;

public sealed class BuildInfoTests
{
    [Fact]
    public void ToolNameIsStable()
    {
        Assert.Equal("storedl", BuildInfo.ToolName);
    }
}
