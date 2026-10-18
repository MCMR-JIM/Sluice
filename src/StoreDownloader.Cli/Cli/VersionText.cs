using System.Runtime.InteropServices;

namespace StoreDownloader.Cli.Cli;

/// <summary>Prints the tool version and the environment it would deploy into.</summary>
internal static class VersionText
{
    public static void Print(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"{BuildInfo.ToolName} {BuildInfo.Version}");
        writer.WriteLine($"{BuildInfo.ProductName} - {BuildInfo.Description}");
        writer.WriteLine($"runtime: .NET {Environment.Version} on {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})");
    }
}
