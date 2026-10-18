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

        var widths = new int[headers.Count];
        for (var column = 0; column < headers.Count; column++)
        {
            widths[column] = headers[column].Length;
        }

        foreach (var row in rows)
        {
            for (var column = 0; column < headers.Count && column < row.Count; column++)
            {
                widths[column] = Math.Max(widths[column], (row[column] ?? string.Empty).Length);
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
            builder.Append(value.PadRight(widths[column]));
            if (column < widths.Length - 1)
            {
                builder.Append("  ");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
