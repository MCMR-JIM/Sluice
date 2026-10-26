using System.Text.Json;
using Xunit;

namespace Sluice.Tests.Support;

/// <summary>
/// Runs a command line with the console captured. <see cref="Console"/> is process wide, so the runs are
/// serialised here rather than in every test that needs them.
/// </summary>
internal static class CliRunner
{
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);

    /// <summary>Runs a command and returns its exit code and both streams.</summary>
    /// <param name="run">Command to run.</param>
    public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(Func<Task<int>> run)
    {
        ArgumentNullException.ThrowIfNull(run);

        await ConsoleGate.WaitAsync().ConfigureAwait(false);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var output = new StringWriter();
        var errors = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(errors);
            var exitCode = await run().ConfigureAwait(false);
            return (exitCode, output.ToString(), errors.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            ConsoleGate.Release();
        }
    }

    /// <summary>Reads the JSON document a command wrote to standard output.</summary>
    /// <param name="stdout">Captured standard output.</param>
    public static JsonElement ReadDocument(string stdout)
    {
        var start = stdout.IndexOfAny(['{', '[']);
        var end = stdout.LastIndexOfAny(['}', ']']);
        Assert.True(
            start >= 0 && end > start,
            $"standard output carried no JSON document. It said: '{stdout.Trim()}'");

        using var document = JsonDocument.Parse(stdout[start..(end + 1)]);
        return document.RootElement.Clone();
    }
}
