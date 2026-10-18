using System.Text;
using System.Text.RegularExpressions;

namespace Sluice.Windows;

/// <summary>
/// Cleans up text that PowerShell produces when its streams are redirected: progress records and
/// error records arrive as CLIXML with ANSI colour codes embedded.
/// </summary>
internal sealed partial class PowerShellOutput
{
    [GeneratedRegex("<S S=\"Error\">(.*?)</S>", RegexOptions.Singleline)]
    private static partial Regex ErrorRecord();

    [GeneratedRegex("\u001b\\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();

    [GeneratedRegex("_x([0-9A-Fa-f]{4})_")]
    private static partial Regex XmlEscape();

    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var working = text;
        if (working.Contains("CLIXML", StringComparison.Ordinal))
        {
            var records = ErrorRecord().Matches(working);
            if (records.Count > 0)
            {
                var builder = new StringBuilder();
                foreach (Match record in records)
                {
                    builder.AppendLine(record.Groups[1].Value);
                }

                working = builder.ToString();
            }
        }

        working = Ansi().Replace(working, string.Empty);
        working = XmlEscape().Replace(
            working,
            match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());

        return working.Trim();
    }
}
