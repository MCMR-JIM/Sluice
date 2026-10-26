using System.Diagnostics;
using System.Text;

namespace Sluice.Windows;

/// <summary>Result of running a PowerShell script.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>True when the process exited with code zero.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Raised when PowerShell (or the appx deployment cmdlets) cannot be used.</summary>
public sealed class PowerShellUnavailableException : SluiceException
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Human readable description.</param>
    public PowerShellUnavailableException(string message) : base(message)
    {
    }
}

/// <summary>
/// Runs PowerShell scripts. The tool never reimplements the deployment APIs: it drives
/// <c>Add-AppxPackage</c> and friends exactly like an operator would, so failures surface as
/// real HRESULTs that can be explained and remediated.
/// </summary>
public sealed class PowerShellHost
{
    /// <summary>PowerShell executables that are tried in order.</summary>
    public static IReadOnlyList<string> CandidateExecutables { get; } = ["pwsh", "powershell.exe"];

    /// <summary>Creates a host using the first available PowerShell executable.</summary>
    /// <param name="executable">Explicit executable path, or <c>null</c> to auto-detect.</param>
    public PowerShellHost(string? executable = null) => Executable = executable ?? FindExecutable();

    /// <summary>The executable this host will run, or <c>null</c> when none was found.</summary>
    public string? Executable { get; }

    /// <summary>True when a PowerShell executable is available.</summary>
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Executable);

    /// <summary>Finds a usable PowerShell executable on PATH.</summary>
    public static string? FindExecutable()
    {
        foreach (var candidate in CandidateExecutables)
        {
            var resolved = Where(candidate);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>Runs a script and returns its output.</summary>
    /// <param name="script">Script text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="PowerShellUnavailableException">No PowerShell executable is available.</exception>
    public async Task<PowerShellResult> RunAsync(string script, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        if (!IsAvailable)
        {
            throw new PowerShellUnavailableException(
                "PowerShell was not found on PATH. Installing packages requires PowerShell 5.1 or PowerShell 7.");
        }

        // Pin the child's console encoding to UTF-8 so localized Windows messages survive the pipe.
        var wrappedScript = string.Join(
            Environment.NewLine,
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8",
            "$OutputEncoding = [System.Text.Encoding]::UTF8",
            script);

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));
        var startInfo = new ProcessStartInfo
        {
            FileName = Executable!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encoded);

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) { stdout.AppendLine(args.Data); } };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) { stderr.AppendLine(args.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Waiting with a token only abandons the wait: the child would keep running whatever it was
        // told to do, so a cancelled install kept deploying after the window said it stopped. The
        // registration is what actually ends the work, and the catch is the path where the token had
        // already fired when the wait started.
        using var registration = cancellationToken.Register(() => Terminate(process));

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            throw;
        }

        return new PowerShellResult(
            process.ExitCode,
            stdout.ToString().Trim(),
            Sanitize(stderr.ToString()));
    }

    /// <summary>
    /// Ends the child process and everything it started, then reaps it so no handle or output reader
    /// is left behind. Failures are swallowed: the process may have exited between the check and the
    /// kill, which is the outcome we want anyway.
    /// </summary>
    /// <param name="process">Process to end.</param>
    private static void Terminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
            // The process was never started or is already gone.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied on a process that outlived its parent; nothing else to do about it.
        }
    }

    /// <summary>
    /// Turns the CLIXML and ANSI noise PowerShell writes to a redirected standard error stream
    /// into plain text.
    /// </summary>
    /// <param name="text">Raw standard error text.</param>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return PowerShellOutput.Clean(text);
    }

    private static string? Where(string executable)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", "" }
            : new[] { "", ".sh" };

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim('"'), executable + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
