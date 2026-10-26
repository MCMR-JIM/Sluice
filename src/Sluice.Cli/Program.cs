using Sluice.Cli.Commands;
using Sluice.Cli.Cli;

namespace Sluice.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        TryEnableUtf8();

        if (args.Length == 0)
        {
            HelpText.Print(Console.Out);
            return 2;
        }

        var command = args[0].Trim().ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        if (command is "-h" or "--help" or "help" or "/?")
        {
            return ShowHelp(rest);
        }

        if (command is "-v" or "--version" or "version")
        {
            VersionText.Print(Console.Out);
            return 0;
        }

        if (rest.Contains("-h", StringComparer.Ordinal) || rest.Contains("--help", StringComparer.Ordinal))
        {
            return ShowHelp([command]);
        }

        if (CommandCatalog.Find(command) is { } manual)
        {
            var unaccepted = CommandArguments(rest).FindUnaccepted(CommandCatalog.AcceptedOptionNames(manual));
            if (unaccepted.Count > 0)
            {
                // A misspelled switch used to be accepted and ignored: the command ran with its
                // defaults while the caller believed the option had been applied.
                Console.Error.WriteLine($"error: {manual.Name} does not take '--{unaccepted[0]}'");
                Console.Error.WriteLine($"       run 'Sluice help {manual.Name}' for the options it does take");
                return 2;
            }
        }

        try
        {
            return command switch
            {
                "resolve" => await ResolveCommand.RunAsync(rest).ConfigureAwait(false),
                "download" => await DownloadCommand.RunAsync(rest).ConfigureAwait(false),
                "analyze" => await AnalyzeCommand.RunAsync(rest).ConfigureAwait(false),
                "plan" => await PlanCommand.RunAsync(rest).ConfigureAwait(false),
                "install" => await InstallCommand.RunAsync(rest).ConfigureAwait(false),
                "doctor" => await DoctorCommand.RunAsync(rest).ConfigureAwait(false),
                "explain" => ExplainCommand.RunAsync(rest),
                _ => UnknownCommand(command),
            };
        }
        catch (SluiceException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"error: network failure ({ex.Message})");
            return 1;
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C, or an HttpClient timeout surfacing as a cancellation: neither deserves a stack
            // trace in a tool that is meant to be scriptable.
            Console.Error.WriteLine("error: cancelled");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Parses the arguments of a command the way the command itself will.</summary>
    /// <param name="rest">Arguments following the command name.</param>
    private static CommandLineArguments CommandArguments(string[] rest) => CommandLineArguments.Parse(rest);

    private static int ShowHelp(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            HelpText.Print(Console.Out);
            return 0;
        }

        var command = CommandCatalog.Find(arguments[0]);
        if (command is null)
        {
            HelpText.PrintUnknownCommand(arguments[0], Console.Error);
            return 2;
        }

        HelpText.PrintCommand(command, Console.Out);
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'");
        Console.Error.WriteLine();
        HelpText.Print(Console.Error);
        return 2;
    }

    private static void TryEnableUtf8()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Redirected or detached console: keep the default encoding.
        }
    }
}
