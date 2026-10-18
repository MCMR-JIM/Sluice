namespace Sluice.Cli.Cli;

/// <summary>
/// The built-in manual: an overview of every command, and one page per command.
/// Everything printed here ships inside the binary, so it works offline.
/// </summary>
internal static class HelpText
{
    private const int OptionColumnWidth = 22;

    /// <summary>Prints the command index.</summary>
    /// <param name="writer">Destination writer.</param>
    public static void Print(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"{BuildInfo.ToolName} {BuildInfo.Version} - {BuildInfo.Description}");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine($"  {BuildInfo.ToolName} <command> [arguments] [options]");
        writer.WriteLine($"  {BuildInfo.ToolName} help <command>          the manual page for one command");
        writer.WriteLine();
        writer.WriteLine("COMMANDS");

        var width = CommandCatalog.All.Max(command => command.Name.Length);
        foreach (var command in CommandCatalog.All)
        {
            writer.WriteLine($"  {command.Name.PadRight(width)}  {command.Summary}");
        }

        writer.WriteLine();
        writer.WriteLine("GLOBAL OPTIONS");
        PrintOptions(CommandCatalog.GlobalOptions, writer);
        writer.WriteLine();
        writer.WriteLine("EXIT CODES");
        foreach (var line in CommandCatalog.ExitCodes)
        {
            writer.WriteLine($"  {line}");
        }

        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine($"  {BuildInfo.ToolName} install https://apps.microsoft.com/detail/9nksqgp7f2nh");
        writer.WriteLine($"  {BuildInfo.ToolName} install 9NKSQGP7F2NH --dry-run");
        writer.WriteLine($"  {BuildInfo.ToolName} download 9NKSQGP7F2NH --all-arch --dir D:\\StoreCache");
        writer.WriteLine($"  {BuildInfo.ToolName} install --dir D:\\StoreCache --skip-download");
        writer.WriteLine($"  {BuildInfo.ToolName} doctor");
        writer.WriteLine($"  {BuildInfo.ToolName} explain 0x80073CF3");
        writer.WriteLine();
        writer.WriteLine($"Run '{BuildInfo.ToolName} help <command>' for arguments, options and notes of one command.");
    }

    /// <summary>Prints the manual page of one command.</summary>
    /// <param name="command">Command to document.</param>
    /// <param name="writer">Destination writer.</param>
    public static void PrintCommand(CommandHelp command, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"{BuildInfo.ToolName} {command.Name} - {command.Summary}");
        writer.WriteLine();

        Section("SYNOPSIS", command.Synopsis, writer, indent: 2);

        if (command.Behaviour.Count > 0)
        {
            Section("WHAT IT DOES", command.Behaviour, writer, indent: 2);
        }

        if (command.Arguments.Count > 0)
        {
            Section("ARGUMENTS", command.Arguments, writer, indent: 2);
        }

        if (command.Options.Count > 0)
        {
            writer.WriteLine("OPTIONS");
            PrintOptions(command.Options, writer);
            writer.WriteLine();
        }

        if (command.Examples.Count > 0)
        {
            Section("EXAMPLES", command.Examples, writer, indent: 2);
        }

        if (command.Notes.Count > 0)
        {
            Section("NOTES", command.Notes, writer, indent: 2);
        }

        Section("GLOBAL OPTIONS", CommandCatalog.GlobalOptions.Select(Format).ToList(), writer, indent: 2);

        if (command.SeeAlso.Count > 0)
        {
            Section("SEE ALSO", command.SeeAlso, writer, indent: 2);
        }
    }

    /// <summary>Prints an unknown command error followed by the index.</summary>
    /// <param name="name">The command the operator typed.</param>
    /// <param name="writer">Destination writer.</param>
    public static void PrintUnknownCommand(string name, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"error: unknown command '{name}'");
        writer.WriteLine();
        writer.WriteLine("Did you mean one of these?");
        foreach (var command in CommandCatalog.All)
        {
            if (command.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(command.Name, StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteLine($"  {command.Name}");
            }
        }

        writer.WriteLine();
        Print(writer);
    }

    private static void Section(string title, IReadOnlyList<string> lines, TextWriter writer, int indent)
    {
        writer.WriteLine(title);
        var padding = new string(' ', indent);
        foreach (var line in lines)
        {
            writer.WriteLine(padding + line);
        }

        writer.WriteLine();
    }

    private static void PrintOptions(IReadOnlyList<CommandOption> options, TextWriter writer)
    {
        foreach (var option in options)
        {
            writer.WriteLine("  " + Format(option));
        }
    }

    private static string Format(CommandOption option) =>
        option.Flag.PadRight(OptionColumnWidth)
        + option.Description
        + (option.Default is null ? string.Empty : $" (default: {option.Default})");
}