using Sluice.Store;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The language tag reaches the catalog request, the delivery request and the resource package selection.
/// A tag typed the POSIX way (<c>zh_CN</c>) matched no resource package, so the install arrived without its
/// resources while every step reported success - in the command line, and, until this was shared, in the
/// window's free text field as well.
/// </summary>
public sealed class LocaleTagTests
{
    [Theory]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData(" ZH-cn ", "ZH-cn")]
    [InlineData("zh-Hans-CN", "zh-Hans-CN")]
    [InlineData("en", "en")]
    public void NormalisesWhatPeopleType(string typed, string expected)
    {
        Assert.Equal(expected, LocaleTag.Normalize(typed));
    }

    [Theory]
    [InlineData("chinese")]
    [InlineData("a&b")]
    [InlineData("zh CN")]
    [InlineData("zh-CN-")]
    [InlineData("-zh")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesWhatIsNotATag(string? typed)
    {
        Assert.Null(LocaleTag.Normalize(typed));
        Assert.False(LocaleTag.IsValid(typed));
    }

    [Fact]
    public void FallsBackToTheDisplayLanguage()
    {
        Assert.Equal(LocaleTag.Default, LocaleTag.NormalizeOrDefault("chinese"));
        Assert.False(string.IsNullOrWhiteSpace(LocaleTag.Default));
        Assert.True(LocaleTag.IsValid(LocaleTag.Default));
    }
}
