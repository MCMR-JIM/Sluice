namespace Sluice.Cli.Cli;

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

        throw new SluiceException($"'{path}' is neither an existing file nor a directory.");
    }

    /// <summary>True when the argument looks like a path rather than a Store reference.</summary>
    /// <param name="value">Candidate argument.</param>
    /// <remarks>
    /// Both separators are looked for and a value that names a scheme is never a path. The check used to
    /// compare against <see cref="Path.DirectorySeparatorChar"/>, which is <c>/</c> on Linux and macOS:
    /// every Store link contains one, so <c>install --dry-run &lt;url&gt;</c> treated the link as a folder
    /// and stopped with "is neither an existing file nor a directory" on those platforms - the exact form
    /// the manual recommends for machines that cannot install.
    /// </remarks>
    public static bool LooksLikePath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (File.Exists(value) || Directory.Exists(value))
        {
            return true;
        }

        if (NamesAScheme(value))
        {
            return false;
        }

        return value.Contains('\\') || value.Contains('/');
    }

    /// <summary>True when the text starts with <c>scheme://</c>.</summary>
    /// <param name="value">Candidate text.</param>
    private static bool NamesAScheme(string value)
    {
        var separator = value.IndexOf("://", StringComparison.Ordinal);
        return separator > 0
            && value[..separator].All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.');
    }
}
