using Sluice.Cli.Cli;
using Xunit;

namespace Sluice.Tests;

public sealed class TablePrinterTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("微信", 4)]
    [InlineData("D:\\下载\\包", 10)]
    [InlineData("ﾃｽﾄ", 3)]
    [InlineData("", 0)]
    public void MeasuresWidthInTerminalCells(string text, int expected)
    {
        Assert.Equal(expected, TablePrinter.DisplayWidth(text));
    }

    [Fact]
    public void KeepsColumnsAlignedWhenACellIsChinese()
    {
        // The path column used to be measured in UTF-16 units, so a Chinese path pushed every column
        // after it two cells to the left and the table sheared.
        using var writer = new StringWriter();
        TablePrinter.Print(
            ["ROLE", "PATH", "RESULT"],
            [
                ["main", "D:\\下载\\Controller.msixbundle", "ok"],
                ["framework", "C:\\cache\\Microsoft.VCLibs.appx", "ok"],
            ],
            writer);

        var offsets = writer.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("ok", StringComparison.Ordinal))
            .Select(line => TablePrinter.DisplayWidth(line[..line.IndexOf("ok", StringComparison.Ordinal)]))
            .Distinct()
            .ToList();

        Assert.Single(offsets);
    }
}
