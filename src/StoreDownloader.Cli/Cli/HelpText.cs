namespace StoreDownloader.Cli.Cli;

/// <summary>Usage text for the CLI.</summary>
internal static class HelpText
{
    public static void Print(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("storedl - resolve, download and sideload Microsoft Store packages");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  storedl <command> [arguments] [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  resolve <url|product-id>   Resolve a Store link into downloadable packages");
        writer.WriteLine("  download <url|product-id>  Download the app, its dependencies and its resources");
        writer.WriteLine();
        writer.WriteLine("Common options:");
        writer.WriteLine("  --locale <tag>             Locale used for resource packages (default en-US)");
        writer.WriteLine("  --arch <arch>              Target architecture (default: this machine)");
        writer.WriteLine("  --json                     Emit machine readable JSON");
        writer.WriteLine("  -h, --help                 Show this help");
        writer.WriteLine();
        writer.WriteLine("Download options:");
        writer.WriteLine("  --dir <path>               Destination folder (default ./downloads)");
        writer.WriteLine("  --all-arch                 Keep every architecture (useful for offline archives)");
        writer.WriteLine("  --deps-only                Only download framework dependencies");
        writer.WriteLine("  --only <text>              Only download files whose name contains text");
        writer.WriteLine("  --parallel <n>             Concurrent downloads (default 3)");
        writer.WriteLine("  --overwrite                Re-download files that already exist");
        writer.WriteLine("  --no-verify                Skip digest verification");
        writer.WriteLine();
        writer.WriteLine("Examples:");
        writer.WriteLine("  storedl resolve https://apps.microsoft.com/detail/9nksqgp7f2nh");
        writer.WriteLine("  storedl download 9NKSQGP7F2NH --dir D:\\StoreCache");
        writer.WriteLine("  storedl download 9NKSQGP7F2NH --all-arch --locale zh-cn");
    }
}
