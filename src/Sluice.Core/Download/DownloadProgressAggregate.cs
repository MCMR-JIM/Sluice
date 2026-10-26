namespace Sluice.Download;

/// <summary>
/// Turns the progress of individual files into the progress of the whole download.
/// </summary>
/// <remarks>
/// A download run reports per file, and more than one file is in flight at a time. A window that shows a
/// single file's fraction therefore watches the bar jump to 100% and back as files finish and start, which
/// reads as a broken bar rather than as progress. This keeps what it has seen per file and answers the
/// fraction of the whole run.
/// </remarks>
public sealed class DownloadProgressAggregate
{
    private readonly Dictionary<string, (long Received, long Total)> _files = new(StringComparer.Ordinal);

    /// <summary>Forgets everything, for a new download run.</summary>
    public void Reset() => _files.Clear();

    /// <summary>How many files have reported progress since the last reset.</summary>
    public int Count => _files.Count;

    /// <summary>Records one file report and returns the fraction of the whole run, between 0 and 1.</summary>
    /// <param name="progress">Report from one file.</param>
    public double Fraction(DownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var previous = _files.TryGetValue(progress.FileName, out var known) ? known : (Received: 0L, Total: 0L);

        // A file that has not been told its size yet must not drag the total down to nothing; the largest
        // total seen for that file is the best estimate there is.
        var total = Math.Max(previous.Total, progress.TotalBytes);
        var received = Math.Max(previous.Received, progress.BytesReceived);
        _files[progress.FileName] = (received, total);

        var sumTotal = 0L;
        var sumReceived = 0L;
        foreach (var file in _files.Values)
        {
            if (file.Total <= 0)
            {
                // Nothing to weigh a file against yet, so it does not take part in the fraction.
                continue;
            }

            sumTotal += file.Total;
            sumReceived += Math.Min(file.Received, file.Total);
        }

        return sumTotal > 0 ? Math.Clamp((double)sumReceived / sumTotal, 0, 1) : 0;
    }
}
