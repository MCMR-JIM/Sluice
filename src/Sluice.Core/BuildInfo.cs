using System.Reflection;

namespace Sluice;

/// <summary>Static metadata about the tool.</summary>
public static class BuildInfo
{
    /// <summary>Name of the tool, used everywhere it is shown to a person.</summary>
    public const string ToolName = "Sluice";

    /// <summary>Product name shown in help output.</summary>
    public const string ProductName = "Sluice";

    /// <summary>Semantic version of the assembly, for example <c>0.1.0</c>.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>One line describing the tool, used by <c>--version</c> and help output.</summary>
    public static string Description => "resolve, download and sideload Microsoft Store packages";

    private static string ReadVersion()
    {
        var assembly = typeof(BuildInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Informational versions can carry a source revision suffix; keep the version part.
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
