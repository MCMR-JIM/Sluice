namespace StoreDownloader.Cli.Cli;

/// <summary>Usage text for the CLI.</summary>
internal static class HelpText
{
    public static void Print(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("storedl — resolve, download and sideload Microsoft Store packages");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  storedl <command> [arguments] [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  resolve <url|product-id>   Resolve a Store link into downloadable packages");
        writer.WriteLine();
        writer.WriteLine("Common options:");
        writer.WriteLine("  --locale <tag>             Locale used for resource packages (default en-US)");
        writer.WriteLine("  --json                     Emit machine readable JSON");
        writer.WriteLine("  -h, --help                 Show this help");
        writer.WriteLine();
        writer.WriteLine("Examples:");
        writer.WriteLine("  storedl resolve https://apps.microsoft.com/detail/9nksqgp7f2nh");
        writer.WriteLine("  storedl resolve 9NKSQGP7F2NH --locale zh-cn");
    }
}
