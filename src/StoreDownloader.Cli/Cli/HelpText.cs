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
        writer.WriteLine("  install <url|product-id|dir>  Resolve, download, install and verify in one go");
        writer.WriteLine("  resolve <url|product-id>      Resolve a Store link into downloadable packages");
        writer.WriteLine("  download <url|product-id>     Download the app, its dependencies and its resources");
        writer.WriteLine("  analyze <file|dir>            Show what is inside appx/msix/bundle files");
        writer.WriteLine("  plan <dir>                    Show the install plan (dependencies first) without installing");
        writer.WriteLine("  doctor                        Check whether this machine can install sideloaded packages");
        writer.WriteLine("  explain <hresult>             Explain a deployment error code and how to get past it");
        writer.WriteLine();
        writer.WriteLine("Common options:");
        writer.WriteLine("  --locale <tag>                Locale for resource packages (default en-US)");
        writer.WriteLine("  --arch <arch>                 Target architecture (default: this machine)");
        writer.WriteLine("  --json                        Emit machine readable JSON");
        writer.WriteLine("  -h, --help                    Show this help");
        writer.WriteLine();
        writer.WriteLine("Download options:");
        writer.WriteLine("  --dir <path>                  Destination folder (default ./downloads)");
        writer.WriteLine("  --all-arch                    Keep every architecture (useful for offline archives)");
        writer.WriteLine("  --deps-only                   Only download framework dependencies");
        writer.WriteLine("  --only <text>                 Only download files whose name contains text");
        writer.WriteLine("  --parallel <n>                Concurrent downloads (default 3)");
        writer.WriteLine("  --overwrite                   Re-download files that already exist");
        writer.WriteLine("  --no-verify                   Skip digest verification");
        writer.WriteLine();
        writer.WriteLine("Install options:");
        writer.WriteLine("  --dry-run                     Print the plan and stop");
        writer.WriteLine("  --yes                         Do not ask for confirmation");
        writer.WriteLine("  --force                       Allow installing over a higher version");
        writer.WriteLine("  --skip-download               Install from --dir without resolving the Store again");
        writer.WriteLine("  --all-deps                    Download every candidate dependency, not just the declared ones");
        writer.WriteLine("  --all-users                   Install for every user (needs elevation)");
        writer.WriteLine("  --no-register                 Do not fall back to loose file registration");
        writer.WriteLine();
        writer.WriteLine("Examples:");
        writer.WriteLine("  storedl install https://apps.microsoft.com/detail/9nksqgp7f2nh");
        writer.WriteLine("  storedl download 9NKSQGP7F2NH --all-arch --dir D:\\StoreCache");
        writer.WriteLine("  storedl install --dir D:\\StoreCache --dry-run");
        writer.WriteLine("  storedl explain 0x80073CF3");
    }
}
