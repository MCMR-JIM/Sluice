namespace StoreDownloader.Cli.Cli;

/// <summary>Finds package files in a folder or validates a single file path.</summary>
internal static class PackageFileLocator
{
    private static readonly string[] Extensions =
        [".msix", ".appx", ".msixbundle", ".appxbundle", ".eappx", ".emsix", ".eappxbundle", ".emsixbundle"];

    /// <summary>Expands a path into the package files it refers to.</summary>
    /// <param name="path">File or directory path.</param>
    public static IReadOnlyList<string> Resolve(string path)
    {
        if (File.Exists(path))
        {
            return [Path.GetFullPath(path)];
        }

        if (Directory.Exists(path))
        {
            return Directory
                .EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        throw new StoreDownloaderException($"'{path}' is neither an existing file nor a directory.");
    }

    /// <summary>True when the argument looks like an existing path rather than a Store reference.</summary>
    /// <param name="value">Candidate argument.</param>
    public static bool LooksLikePath(string value) =>
        File.Exists(value) || Directory.Exists(value) || value.Contains(Path.DirectorySeparatorChar);
}
