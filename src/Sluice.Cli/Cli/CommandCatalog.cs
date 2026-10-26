namespace Sluice.Cli.Cli;

/// <summary>One documented option of a command.</summary>
/// <param name="Flag">Flag as it has to be typed, including any placeholder.</param>
/// <param name="Description">What it does.</param>
/// <param name="Default">Default behaviour when it is omitted.</param>
internal sealed record CommandOption(string Flag, string Description, string? Default = null);

/// <summary>The built-in manual page of one command.</summary>
/// <param name="Name">Command name.</param>
/// <param name="Summary">One line description for the index.</param>
/// <param name="Synopsis">How the command is invoked, one line per form.</param>
/// <param name="Behaviour">What the command does, in order.</param>
/// <param name="Arguments">Positional arguments and accepted reference shapes.</param>
/// <param name="Options">Options the command understands.</param>
/// <param name="Examples">Copy-pasteable examples.</param>
/// <param name="Notes">Caveats worth reading before running it.</param>
/// <param name="SeeAlso">Related commands.</param>
internal sealed record CommandHelp(
    string Name,
    string Summary,
    IReadOnlyList<string> Synopsis,
    IReadOnlyList<string> Behaviour,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<CommandOption> Options,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> SeeAlso);

/// <summary>
/// The manual that ships inside the binary. Every command is described here, so
/// <c>Sluice help install</c> works on a machine with no network and no docs folder.
/// </summary>
internal static class CommandCatalog
{
    /// <summary>Options every command accepts.</summary>
    public static IReadOnlyList<CommandOption> GlobalOptions { get; } =
    [
        new CommandOption("--json", "Emit machine readable JSON instead of tables."),
        new CommandOption("-h, --help", "Show this manual page."),
        new CommandOption("-v, --version", "Show the tool version and runtime."),
    ];

    /// <summary>Exit codes shared by all commands.</summary>
    public static IReadOnlyList<string> ExitCodes { get; } =
    [
        "0   the command did what it says",
        "1   the command failed (unresolvable product, download error, install failure)",
        "2   the arguments were wrong, or a required one was missing",
        "3   doctor only: the machine cannot deploy packages until the warnings are fixed",
    ];

    /// <summary>
    /// Option names a command accepts, without leading dashes. Derived from the manual entries plus the
    /// global switches, so an option that is documented is accepted and an option nobody documents is
    /// not silently swallowed.
    /// </summary>
    /// <param name="help">Command whose manual page describes the options.</param>
    public static IReadOnlyList<string> AcceptedOptionNames(CommandHelp help)
    {
        ArgumentNullException.ThrowIfNull(help);

        var names = new List<string>();
        foreach (var option in help.Options.Concat(GlobalOptions))
        {
            // "--dir <path>" -> "dir"; "-h, --help" -> "h" and "help".
            foreach (var part in option.Flag.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var name = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (name is not null && name.StartsWith('-'))
                {
                    names.Add(name.TrimStart('-'));
                }
            }
        }

        return names;
    }

    /// <summary>All documented commands.</summary>
    public static IReadOnlyList<CommandHelp> All { get; } =
    [
        new CommandHelp(
            "install",
            "Resolve, download, install and verify in one go",
            [
                "Sluice install <store-reference> [options]",
                "Sluice install --dir <folder> [options]",
            ],
            [
                "1. Resolves the reference into packages and picks the ones for this architecture.",
                "2. Downloads the application, reads its manifest, downloads the declared frameworks.",
                "3. Builds a plan: frameworks first, then the application, then the resources.",
                "4. Runs Add-AppxPackage per step, escalating known remedies on failure.",
                "5. Reads Get-AppxPackage back and compares it with the plan.",
            ],
            [
                "<store-reference>   Store URL, ms-windows-store deep link, or bare product id.",
                "--dir <folder>      Install from packages already on disk instead of resolving.",
            ],
            [
                new CommandOption("--dir <path>", "Folder to download into, or to install from.", "./downloads"),
                new CommandOption("--arch <arch>", "Target architecture: x64, x86, arm64, arm.", "this machine"),
                new CommandOption("--locale <tag>", "Locale of the resource packages.", "Windows display language"),
                new CommandOption("--all-arch", "Download every architecture instead of this one."),
                new CommandOption("--all-deps", "Download every candidate dependency, not just declared ones."),
                new CommandOption(
                    "--dry-run",
                    "Report what a run would do and stop, downloading nothing. From a Store link that is the "
                    + "package selection; from --dir it is the install plan."),
                new CommandOption("--yes", "Do not ask for confirmation before installing."),
                new CommandOption("--skip-download", "Install from --dir without asking the Store again."),
                new CommandOption("--force", "Allow installing over a higher version."),
                new CommandOption("--all-users", "Install for every user; needs an elevated shell."),
                new CommandOption("--parallel <n>", "Concurrent downloads, 1 to 16.", "3"),
                new CommandOption("--no-verify", "Skip the post-install registration check."),
                new CommandOption("--no-register", "Disable the loose-file registration fallback."),
                new CommandOption("--no-force-shutdown", "Do not close a running copy of the app before replacing it."),
            ],
            [
                "Sluice install https://apps.microsoft.com/detail/9nksqgp7f2nh",
                "Sluice install 9NKSQGP7F2NH --dry-run",
                "Sluice install --dir D:\\StoreCache --skip-download --yes",
            ],
            [
                "Needs Windows and PowerShell: deployment goes through Add-AppxPackage.",
                "Encrypted Store packages (.eappx, .emsix) are refused; they need a Store licence.",
                "When the plan reports a missing dependency, nothing is installed at all.",
                "A folder holding several products is narrowed down by Sluice-manifest.json, which download "
                + "writes next to the packages, so the product the folder was downloaded for is the one installed.",
                "If the run stops at a failure, the steps it never reached are listed as not attempted.",
            ],
            ["Sluice help download", "Sluice help plan", "Sluice help doctor"]),

        new CommandHelp(
            "resolve",
            "Resolve a Store link into downloadable packages",
            ["Sluice resolve <store-reference> [options]"],
            [
                "Looks the product up in the Store catalog, expands its Windows Update category,",
                "and prints every package with its architecture, size and whether a URL was issued.",
            ],
            [
                "<store-reference>   Store URL, deep link, or bare product id.",
            ],
            [
                new CommandOption("--locale <tag>", "Locale used for resource packages.", "Windows display language"),
            ],
            [
                "Sluice resolve https://apps.microsoft.com/detail/9nksqgp7f2nh",
                "Sluice resolve 9MSMLRH6LZF3 --json",
            ],
            [
                "URLs in the output expire within hours; resolve right before downloading.",
                "Frameworks for every architecture and runtime generation are listed, not just the ones this app needs.",
            ],
            ["Sluice help download", "Sluice help install"]),

        new CommandHelp(
            "download",
            "Download the app, its dependencies and its resources",
            ["Sluice download <store-reference> [options]"],
            [
                "Resolves the reference, picks the main package plus the newest compatible framework",
                "per dependency, downloads them with resume support, verifies the published SHA-1,",
                "and writes sluice-manifest.json next to the files.",
            ],
            [
                "<store-reference>   Store URL, deep link, or bare product id.",
            ],
            [
                new CommandOption("--dir <path>", "Destination folder.", "./downloads"),
                new CommandOption("--arch <arch>", "Target architecture.", "this machine"),
                new CommandOption("--locale <tag>", "Locale of the resource packages.", "Windows display language"),
                new CommandOption("--all-arch", "Keep every architecture, for offline archives."),
                new CommandOption("--deps-only", "Only the framework packages."),
                new CommandOption("--only <text>", "Only files whose name contains this text."),
                new CommandOption("--parallel <n>", "Concurrent downloads, 1 to 16.", "3"),
                new CommandOption("--overwrite", "Re-download files that already exist."),
                new CommandOption("--no-verify", "Skip digest verification."),
            ],
            [
                "Sluice download 9NKSQGP7F2NH",
                "Sluice download 9NKSQGP7F2NH --all-arch --locale zh-CN --dir D:\\StoreCache",
                "Sluice download 9NKSQGP7F2NH --deps-only --only VCLibs",
            ],
            [
                "Downloads only the declared dependencies of install; this command keeps every candidate.",
                "Existing files are reused when their digest still matches, so re-running is cheap.",
            ],
            ["Sluice help install", "Sluice help analyze"]),

        new CommandHelp(
            "analyze",
            "Show what is inside appx, msix and bundle files",
            ["Sluice analyze <file-or-folder> [options]"],
            [
                "Opens the container (an appx is a zip), reads AppxManifest.xml or the bundle manifest,",
                "and prints identity, publisher, target device family, dependencies and bundle children,",
                "including which child would be chosen for this architecture.",
            ],
            ["<file-or-folder>   A package file, or a folder containing package files."],
            [
                new CommandOption("--arch <arch>", "Architecture used for the bundle selection preview.", "this machine"),
            ],
            [
                "Sluice analyze .\\downloads",
                "Sluice analyze .\\downloads\\App_1.0.0.0_x64.msix --json",
            ],
            ["Never installs anything and never needs network access."],
            ["Sluice help plan", "Sluice help install"]),

        new CommandHelp(
            "plan",
            "Show the install plan without touching the machine",
            ["Sluice plan <folder> [options]"],
            [
                "Analyses every package in the folder, reads the dependencies out of the application",
                "manifest (including the manifest inside a bundle), compares them with what",
                "Get-AppxPackage reports as installed, and prints the ordered steps.",
            ],
            ["<folder>   Folder containing the downloaded packages."],
            [
                new CommandOption("--arch <arch>", "Target architecture.", "this machine"),
                new CommandOption("--locale <tag>", "Locale used to pick resource packages.", "Windows display language"),
            ],
            [
                "Sluice plan .\\downloads",
                "Sluice plan D:\\StoreCache --arch arm64 --json",
            ],
            [
                "Blocking issues (a declared dependency with no package in the folder) make it exit 1.",
                "Reads the installed inventory, so the answer depends on this machine.",
            ],
            ["Sluice help install", "Sluice help analyze"]),

        new CommandHelp(
            "doctor",
            "Check whether this machine can install sideloaded packages",
            ["Sluice doctor [--json]"],
            [
                "Reports the Windows build, architecture, sideloading policy, Developer Mode,",
                "the deployment services, free disk space and the PowerShell that would be used.",
                "Every finding that would break an install is listed as a warning with the fix.",
            ],
            [],
            [],
            ["Sluice doctor", "Sluice doctor --json"],
            [
                "Exit code 3 means warnings were found; the machine is not ready to deploy packages.",
                "ClipSVC starting on demand is expected and is not reported as a problem.",
            ],
            ["Sluice help explain", "Sluice help install"]),

        new CommandHelp(
            "explain",
            "Explain a deployment error code and how to get past it",
            ["Sluice explain <hresult>", "Sluice explain --list"],
            [
                "Looks the HRESULT up in the built-in catalogue and prints what Windows means, why it",
                "happens with Store packages, what the tool retries automatically, and the manual fix.",
            ],
            [
                "<hresult>   Code in any form Windows prints it: 0x80073CF3, 80073CF3, 2147958003 or -2147024891.",
            ],
            [
                new CommandOption("--list", "List every catalogued code with its meaning."),
            ],
            [
                "Sluice explain 0x80073CF3",
                "Sluice explain 0x80073CFF --json",
                "Sluice explain --list",
            ],
            ["Unknown but well formed codes still parse; the command then says it is not catalogued."],
            ["Sluice help doctor"]),
    ];

    /// <summary>Finds a command by name.</summary>
    /// <param name="name">Command name, case insensitive.</param>
    public static CommandHelp? Find(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : All.FirstOrDefault(command => string.Equals(command.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
}