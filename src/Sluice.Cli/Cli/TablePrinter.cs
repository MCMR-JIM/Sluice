using System.Globalization;
using System.Text;

namespace Sluice.Cli.Cli;

/// <summary>Renders simple aligned tables in the console.</summary>
public static class TablePrinter
{
    /// <summary>Prints a table with a header row and optional emphasis on the first column.</summary>
    /// <param name="headers">Column headers.</param>
    /// <param name="rows">Row values, one entry per column.</param>
    /// <param name="writer">Destination writer.</param>
    public static void Print(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        if (headers.Count == 0)
        {
            return;
        }

        // Widths are measured in terminal cells, not in UTF-16 units: a Chinese path is one character
        // per cell-pair, so counting characters sheared every column after it.
        var widths = new int[headers.Count];
        for (var column = 0; column < headers.Count; column++)
        {
            widths[column] = DisplayWidth(headers[column]);
        }

        foreach (var row in rows)
        {
            for (var column = 0; column < headers.Count && column < row.Count; column++)
            {
                widths[column] = Math.Max(widths[column], DisplayWidth(row[column]));
            }
        }

        writer.WriteLine(RenderRow(headers, widths));
        writer.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));
        foreach (var row in rows)
        {
            writer.WriteLine(RenderRow(row, widths));
        }

        if (rows.Count == 0)
        {
            writer.WriteLine("(no rows)");
        }
    }

    /// <summary>
    /// Width of a string in terminal cells: East Asian characters and emoji take two, combining marks
    /// and zero width characters take none.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    public static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += RuneWidth(rune.Value);
        }

        return width;
    }

    /// <summary>Formats a byte count for humans.</summary>
    /// <param name="bytes">Size in bytes.</param>
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value:0.##} {units[unit]}");
    }

    private static string RenderRow(IReadOnlyList<string> values, int[] widths)
    {
        var builder = new StringBuilder();
        for (var column = 0; column < widths.Length; column++)
        {
            var value = column < values.Count ? values[column] ?? string.Empty : string.Empty;
            builder.Append(value);
            builder.Append(' ', Math.Max(0, widths[column] - DisplayWidth(value)));
            if (column < widths.Length - 1)
            {
                builder.Append("  ");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Cells one rune occupies in a terminal.</summary>
    /// <param name="codePoint">Unicode code point.</param>
    private static int RuneWidth(int codePoint) => codePoint switch
    {
        // Combining marks, zero width space/joiners and the byte order mark do not advance the cursor.
        0x0300 or 0x036F => 0,
        >= 0x0300 and <= 0x036F => 0,
        >= 0x1AB0 and <= 0x1AFF => 0,
        >= 0x20D0 and <= 0x20F0 => 0,
        >= 0x200B and <= 0x200F => 0,
        0xFEFF => 0,
        >= 0xFE00 and <= 0xFE0F => 0,

        // East Asian wide and fullwidth ranges.
        >= 0x1100 and <= 0x115F => 2,
        >= 0x2E80 and <= 0x303E => 2,
        >= 0x3041 and <= 0x33FF => 2,
        >= 0x3400 and <= 0x4DBF => 2,
        >= 0x4E00 and <= 0x9FFF => 2,
        >= 0xA000 and <= 0xA4CF => 2,
        >= 0xAC00 and <= 0xD7A3 => 2,
        >= 0xF900 and <= 0xFAFF => 2,
        >= 0xFE30 and <= 0xFE6F => 2,
        >= 0xFF00 and <= 0xFF60 => 2,
        >= 0xFFE0 and <= 0xFFE6 => 2,
        >= 0x1F300 and <= 0x1F64F => 2,
        >= 0x1F900 and <= 0x1F9FF => 2,
        >= 0x20000 and <= 0x3FFFD => 2,

        _ => 1,
    };
}
