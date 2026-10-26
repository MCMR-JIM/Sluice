using System.Diagnostics;
using System.Globalization;
using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class PowerShellHostTests
{
    [Fact]
    public async Task CancellingEndsTheChildProcess()
    {
        var host = new PowerShellHost();
        if (!host.IsAvailable)
        {
            // Nothing to assert on a machine without PowerShell; the rest of the suite still runs.
            return;
        }

        var marker = Path.Combine(Path.GetTempPath(), $"sluice-child-{Guid.NewGuid():N}.txt");
        var script = $"$PID | Out-File -FilePath '{marker}' -Encoding ascii; Start-Sleep -Seconds 60";

        try
        {
            using var cancellation = new CancellationTokenSource();
            var run = host.RunAsync(script, cancellation.Token);

            var childPid = await ReadMarkerAsync(marker).ConfigureAwait(true);
            Assert.True(childPid > 0, "the child PowerShell never reported its process id");

            // The run is in the middle of a long sleep when the caller cancels, which is exactly the
            // shape of a cancelled install.
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run).ConfigureAwait(true);

            Assert.True(
                await WaitForExitAsync(childPid).ConfigureAwait(true),
                $"the child PowerShell process {childPid} outlived the cancellation");
        }
        finally
        {
            File.Delete(marker);
        }
    }

    private static async Task<int> ReadMarkerAsync(string path)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (File.Exists(path))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(path).ConfigureAwait(true);
                    if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0)
                    {
                        return pid;
                    }
                }
                catch (IOException)
                {
                    // PowerShell still has the file open; try again on the next tick.
                }
            }

            await Task.Delay(100).ConfigureAwait(true);
        }

        return 0;
    }

    private static async Task<bool> WaitForExitAsync(int pid)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (!IsRunning(pid))
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(true);
        }

        return !IsRunning(pid);
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // No process with that id: it is gone, which is what the test wants.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
