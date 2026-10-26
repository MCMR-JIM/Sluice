using System.Text.Json;
using Sluice.Cli.Commands;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// A command that answers with a JSON document has to answer with a document when it fails too. A bad
/// argument or a refused install put its reason on standard error only, so a caller that reads standard
/// output as JSON failed with a parse error that hid the real reason - the same quiet lie the empty
/// selection and top level failures had.
/// </summary>
public sealed class CliFailureDocumentTests
{
    /// <summary>Serialises the console redirection: <see cref="Console"/> is process wide.</summary>
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);

    [Theory]
    [InlineData("resolve")]
    [InlineData("download")]
    [InlineData("analyze")]
    [InlineData("plan")]
    [InlineData("install")]
    public async Task AMissingArgumentStillAnswersWithADocument(string command)
    {
        var (exitCode, document) = await Capture(() => Run(command, ["--json"]));

        Assert.Equal(2, exitCode);
        Assert.False(document.GetProperty("succeeded").GetBoolean());
        Assert.Equal("invalid-arguments", document.GetProperty("kind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.GetProperty("error").GetString()));
    }

    [Theory]
    [InlineData("analyze")]
    [InlineData("plan")]
    public async Task AnEmptyFolderStillAnswersWithADocument(string command)
    {
        var folder = Path.Combine(Path.GetTempPath(), "sluice-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var (exitCode, document) = await Capture(() => Run(command, [folder, "--json"]));

            Assert.Equal(1, exitCode);
            Assert.False(document.GetProperty("succeeded").GetBoolean());
            Assert.Equal("no-packages", document.GetProperty("kind").GetString());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ABlockedPlanAnswersWithTheIssuesThatBlockedIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "sluice-blocked-" + Guid.NewGuid().ToString("N"));

        try
        {
            // The application declares a framework the folder does not contain: the plan cannot proceed,
            // and the reason a JSON caller needs is the issue list, not just "failed".
            PackageFixtureBuilder.CreateAppPackage(
                folder,
                "Contoso.Notes.appx",
                "Contoso.Notes",
                dependencies: [("Contoso.Framework", "1.0.0.0")]);

            var (exitCode, document) = await Capture(() => InstallCommand.RunAsync([folder, "--dry-run", "--json"]));

            Assert.Equal(1, exitCode);
            Assert.False(document.GetProperty("succeeded").GetBoolean());
            Assert.Equal("plan-blocked", document.GetProperty("kind").GetString());

            var issues = document.GetProperty("blockingIssues")
                .EnumerateArray()
                .Select(issue => issue.GetString() ?? string.Empty)
                .ToList();

            Assert.Contains(issues, issue => issue.Contains("Contoso.Framework", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static Task<int> Run(string command, string[] args) => command switch
    {
        "resolve" => ResolveCommand.RunAsync(args),
        "download" => DownloadCommand.RunAsync(args),
        "analyze" => AnalyzeCommand.RunAsync(args),
        "plan" => PlanCommand.RunAsync(args),
        "install" => InstallCommand.RunAsync(args),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "unknown command"),
    };

    /// <summary>Runs a command with the console captured and returns what it wrote as a document.</summary>
    /// <param name="run">Command to run.</param>
    private static async Task<(int ExitCode, JsonElement Document)> Capture(Func<Task<int>> run)
    {
        ArgumentNullException.ThrowIfNull(run);

        await ConsoleGate.WaitAsync().ConfigureAwait(false);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(new StringWriter());
            var exitCode = await run().ConfigureAwait(false);
            return (exitCode, ReadDocument(output.ToString()));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            ConsoleGate.Release();
        }
    }

    private static JsonElement ReadDocument(string stdout)
    {
        var start = stdout.IndexOf('{', StringComparison.Ordinal);
        var end = stdout.LastIndexOf('}');
        Assert.True(
            start >= 0 && end > start,
            $"standard output carried no JSON document. It said: '{stdout.Trim()}'");

        using var document = JsonDocument.Parse(stdout[start..(end + 1)]);
        return document.RootElement.Clone();
    }
}
