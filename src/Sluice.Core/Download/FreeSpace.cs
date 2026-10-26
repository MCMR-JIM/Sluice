namespace Sluice.Download;

/// <summary>
/// Answers whether a download can fit where it is going. Kept apart from the downloader so the decision
/// can be tested without a disk that is actually full.
/// </summary>
public static class FreeSpace
{
    /// <summary>Headroom left for the partial file, the manifest and the file system itself.</summary>
    public const long ReserveBytes = 64L * 1024 * 1024;

    /// <summary>Free bytes on the drive that holds <paramref name="directory"/>.</summary>
    /// <param name="directory">Folder a download would be written to.</param>
    /// <returns>Free bytes, or <c>long.MaxValue</c> when the drive cannot be inspected.</returns>
    public static long AvailableBytes(string directory)
    {
        try
        {
            var full = Path.GetFullPath(directory);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
            {
                return long.MaxValue;
            }

            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // A folder on a network share or a drive that vanished: do not block the download over it.
            return long.MaxValue;
        }
    }

    /// <summary>
    /// Describes why a download cannot fit, or <c>null</c> when it can.
    /// </summary>
    /// <param name="requiredBytes">Bytes the run will write.</param>
    /// <param name="availableBytes">Bytes the drive reports as free.</param>
    /// <param name="directory">Folder the run would write to.</param>
    public static string? Check(long requiredBytes, long availableBytes, string directory)
    {
        if (availableBytes == long.MaxValue || requiredBytes + ReserveBytes <= availableBytes)
        {
            return null;
        }

        return $"Downloading needs {Format(requiredBytes + ReserveBytes)} free in '{directory}' but only "
            + $"{Format(availableBytes)} is available; free some space or point --dir at another drive.";
    }

    private static string Format(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.#} GiB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.#} MiB",
        >= 1024 => $"{bytes / 1024d:0.#} KiB",
        _ => $"{bytes} B",
    };
}
