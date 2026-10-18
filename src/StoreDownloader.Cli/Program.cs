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
            HelpText.Print(Console.Out);
            return 0;
        }

        try
        {
            return command switch
            {
                "resolve" => await ResolveCommand.RunAsync(rest).ConfigureAwait(false),
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
