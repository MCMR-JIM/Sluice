using System.Runtime.InteropServices;

namespace Sluice.Packages;

/// <summary>Architecture names as Windows writes them in package manifests.</summary>
public static class WindowsArchitecture
{
    /// <summary>The architecture of the machine this process runs on.</summary>
    public static string Current => Normalize(RuntimeInformation.OSArchitecture.ToString());

    /// <summary>Maps .NET or catalog architecture names onto manifest names.</summary>
    /// <param name="value">Architecture name in any of the shapes the codebase receives.</param>
    /// <returns>One of <c>x86</c>, <c>x64</c>, <c>arm</c>, <c>arm64</c>, <c>neutral</c>, or the lowercased input.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "neutral";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "amd64" or "x86_64" or "x64" => "x64",
            "x86" or "i386" or "i686" => "x86",
            "arm" or "arm32" => "arm",
            "arm64" or "aarch64" => "arm64",
            "neutral" or "anycpu" or "none" => "neutral",
            var other => other,
        };
    }

    /// <summary>
    /// True when a package with <paramref name="packageArchitecture"/> is usable on
    /// <paramref name="targetArchitecture"/>. Neutral packages are usable everywhere.
    /// </summary>
    /// <param name="packageArchitecture">Architecture declared by the package.</param>
    /// <param name="targetArchitecture">Architecture of the target machine.</param>
    public static bool IsCompatible(string? packageArchitecture, string targetArchitecture)
    {
        var package = Normalize(packageArchitecture);
        var target = Normalize(targetArchitecture);

        if (package == "neutral")
        {
            return true;
        }

        if (package == target)
        {
            return true;
        }

        // An x64 machine can run x86 packages, and an arm64 machine can run x86 and x64 ones.
        return target switch
        {
            "x64" => package == "x86",
            "arm64" => package is "arm" or "x86" or "x64",
            _ => false,
        };
    }

    /// <summary>Scores how good a match an architecture is, higher being better.</summary>
    /// <param name="packageArchitecture">Architecture declared by the package.</param>
    /// <param name="targetArchitecture">Architecture of the target machine.</param>
    public static int MatchScore(string? packageArchitecture, string targetArchitecture)
    {
        var package = Normalize(packageArchitecture);
        var target = Normalize(targetArchitecture);

        if (package == target)
        {
            return 3;
        }

        return IsCompatible(package, target) ? (package == "neutral" ? 1 : 2) : 0;
    }
}
