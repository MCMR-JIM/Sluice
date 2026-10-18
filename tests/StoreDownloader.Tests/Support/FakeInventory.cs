using StoreDownloader.Windows;

namespace StoreDownloader.Tests.Support;

/// <summary>In-memory stand-in for <see cref="AppxInventory"/>.</summary>
internal sealed class FakeInventory(params InstalledPackage[] packages) : IInstalledPackageInventory
{
    public List<InstalledPackage> Packages { get; } = [.. packages];

    public Exception? Failure { get; init; }

    public Task<IReadOnlyList<InstalledPackage>> GetFrameworksAsync(CancellationToken cancellationToken = default) =>
        Failure is null
            ? Task.FromResult<IReadOnlyList<InstalledPackage>>(Packages.Where(package => package.IsFramework).ToList())
            : Task.FromException<IReadOnlyList<InstalledPackage>>(Failure);

    public Task<InstalledPackage?> FindHighestAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Packages
            .Where(package => string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(package => package.GetVersion())
            .FirstOrDefault());
}
