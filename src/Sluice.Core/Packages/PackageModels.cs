namespace Sluice.Packages;

/// <summary>The shape of a package file on disk.</summary>
public enum PackageKind
{
    /// <summary>A single application package (<c>.msix</c> or <c>.appx</c>).</summary>
    App,

    /// <summary>A bundle that contains several application and resource packages.</summary>
    Bundle,

    /// <summary>A resource only package.</summary>
    Resource,

    /// <summary>A framework dependency package.</summary>
    Framework,

    /// <summary>Something that is not a readable package.</summary>
    Unknown,
}

/// <summary>Identity block of an appx/msix manifest.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Version">Package version.</param>
/// <param name="Architecture">Processor architecture, normalised.</param>
/// <param name="ResourceId">Resource id, empty for application packages.</param>
/// <param name="Publisher">Publisher distinguished name.</param>
public sealed record PackageIdentityInfo(
    string Name,
    string? Version,
    string? Architecture,
    string? ResourceId,
    string? Publisher)
{
    /// <summary>Returns <c>Name_Version_Architecture</c> for display purposes.</summary>
    public string ToDisplayString() =>
        string.Join('_', new[] { Name, Version, Architecture }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>A dependency declared by a package manifest.</summary>
/// <param name="Name">Dependency package name.</param>
/// <param name="MinVersion">Minimum acceptable version.</param>
/// <param name="Publisher">Required publisher, when declared.</param>
public sealed record PackageDependency(string Name, string? MinVersion, string? Publisher)
{
    /// <inheritdoc />
    public override string ToString() => MinVersion is null ? Name : $"{Name} >= {MinVersion}";
}

/// <summary>One child package inside a bundle.</summary>
/// <param name="FileName">Entry name inside the bundle.</param>
/// <param name="Type">Package type, for example <c>application</c> or <c>resource</c>.</param>
/// <param name="Architecture">Architecture, normalised.</param>
/// <param name="Version">Version string.</param>
/// <param name="ResourceId">Resource id for resource packages.</param>
/// <param name="SizeInBytes">Size of the child package inside the bundle.</param>
public sealed record BundleChildPackage(
    string FileName,
    string? Type,
    string? Architecture,
    string? Version,
    string? ResourceId,
    long SizeInBytes)
{
    /// <summary>True when the child is an application package.</summary>
    public bool IsApplication => string.Equals(Type, "application", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the child is a resource package.</summary>
    public bool IsResource => string.Equals(Type, "resource", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Everything the tool knows about one package file on disk.</summary>
/// <param name="FilePath">Absolute path of the analysed file.</param>
/// <param name="Kind">Shape of the package.</param>
/// <param name="Identity">Identity block, when readable.</param>
/// <param name="Dependencies">Package dependencies declared by the manifest.</param>
/// <param name="Children">Child packages when this is a bundle.</param>
/// <param name="DisplayName">Display name from the manifest, when present.</param>
/// <param name="PublisherDisplayName">Publisher display name from the manifest, when present.</param>
/// <param name="TargetDeviceFamily">Target device family, for example <c>Windows.Desktop</c>.</param>
/// <param name="MinimumWindowsVersion">Minimum Windows version required by the package.</param>
public sealed record PackageAnalysis(
    string FilePath,
    PackageKind Kind,
    PackageIdentityInfo? Identity,
    IReadOnlyList<PackageDependency> Dependencies,
    IReadOnlyList<BundleChildPackage> Children,
    string? DisplayName,
    string? PublisherDisplayName,
    string? TargetDeviceFamily,
    string? MinimumWindowsVersion)
{
    /// <summary>File name without directory.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Size of the file on disk.</summary>
    public long SizeInBytes => File.Exists(FilePath) ? new FileInfo(FilePath).Length : 0;
}
