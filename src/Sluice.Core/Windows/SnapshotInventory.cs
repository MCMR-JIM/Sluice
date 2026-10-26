namespace Sluice.Windows;

/// <summary>
/// Answers inventory questions from one read, for one pipeline run.
/// </summary>
/// <remarks>
/// Planning asks whether a framework is installed for every dependency and then asks about the
/// application itself, and each question used to start its own PowerShell process - three launches for a
/// plan of one dependency, each of them half a second and a slightly different view of the machine. One
/// snapshot answers all of them and, more importantly, answers them consistently.
/// <para>
/// A snapshot must not outlive the state it describes: the installer changes the machine, so it keeps
/// asking the inventory directly, and the next run takes a new snapshot.
/// </para>
/// </remarks>
public sealed class SnapshotInventory : IInstalledPackageInventory
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<InstalledPackage>>> _load;
    private readonly Dictionary<string, Task<InstalledPackage?>> _byName = new(StringComparer.OrdinalIgnoreCase);
    private Task<IReadOnlyList<InstalledPackage>>? _packages;

    /// <summary>Creates a snapshot over a loader that reads the machine once.</summary>
    /// <param name="load">Reads every installed package the plan may care about.</param>
    public SnapshotInventory(Func<CancellationToken, Task<IReadOnlyList<InstalledPackage>>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        _load = load;
    }

    /// <summary>How many times the machine has been read.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstalledPackage>> GetFrameworksAsync(CancellationToken cancellationToken = default)
    {
        var packages = await AllAsync(cancellationToken).ConfigureAwait(false);
        return packages.Where(package => package.IsFramework).ToList();
    }

    /// <inheritdoc />
    public async Task<InstalledPackage?> FindHighestAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var packages = await AllAsync(cancellationToken).ConfigureAwait(false);
        return packages
            .Where(package => string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(package => package.GetVersion())
            .FirstOrDefault();
    }

    private Task<IReadOnlyList<InstalledPackage>> AllAsync(CancellationToken cancellationToken)
    {
        if (_packages is null)
        {
            Reads++;
            _packages = _load(cancellationToken);
        }

        return _packages;
    }
}
