using StoreDownloader.Cli.Commands;
using StoreDownloader.Cli.Cli;

namespace StoreDownloader.Cli;

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
        catch (StoreDownloaderException ex)
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
    }

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
