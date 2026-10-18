using System.IO.Compression;
using System.Xml.Linq;

namespace StoreDownloader.Packages;

/// <summary>
/// Reads <c>.msix</c>, <c>.appx</c>, <c>.msixbundle</c> and <c>.appxbundle</c> files: both are
/// ZIP containers, so the manifest can be read without installing anything or using the
/// Windows deployment APIs.
/// </summary>
public static class PackageAnalyzer
{
    /// <summary>Entry name of a single package manifest.</summary>
    public const string AppManifestEntryName = "AppxManifest.xml";

    /// <summary>Entry name of a bundle manifest.</summary>
    public const string BundleManifestEntryName = "AppxMetadata/AppxBundleManifest.xml";

    /// <summary>
    /// Analyses a package file.
    /// </summary>
    /// <param name="filePath">Path to a package, bundle or block map free package file.</param>
    /// <returns>What the file contains.</returns>
    /// <exception cref="StoreDownloaderException">The file is missing, encrypted or not a package.</exception>
    public static PackageAnalysis Analyze(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new StoreDownloaderException($"Package file '{filePath}' does not exist.");
        }

        var extension = Path.GetExtension(filePath);
        if (extension is ".eappx" or ".emsix" or ".eappxbundle" or ".emsixbundle")
        {
            throw new StoreDownloaderException(
                $"'{Path.GetFileName(filePath)}' is an encrypted Store package. It can only be installed with a "
                + "license obtained from the Store for an entitled account, so this tool cannot sideload it.");
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var bundleEntry = FindEntry(archive, BundleManifestEntryName);
            if (bundleEntry is not null)
            {
                using var bundleStream = bundleEntry.Open();
                return ParseBundleManifest(filePath, bundleStream);
            }

            var appEntry = FindEntry(archive, AppManifestEntryName);
            if (appEntry is not null)
            {
                using var appStream = appEntry.Open();
                return ParseAppManifest(filePath, appStream);
            }

            throw new StoreDownloaderException(
                $"'{Path.GetFileName(filePath)}' contains neither {AppManifestEntryName} nor {BundleManifestEntryName}; it is not an appx/msix package.");
        }
        catch (InvalidDataException ex)
        {
            throw new StoreDownloaderException($"'{Path.GetFileName(filePath)}' is not a readable ZIP container.", ex);
        }
    }

    /// <summary>Analyses a file, returning <c>null</c> instead of throwing.</summary>
    /// <param name="filePath">Path to a package file.</param>
    public static PackageAnalysis? TryAnalyze(string filePath)
    {
        try
        {
            return Analyze(filePath);
        }
        catch (StoreDownloaderException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the manifest of one child package inside a bundle, without extracting the bundle.
    /// </summary>
    /// <param name="bundlePath">Path to the bundle.</param>
    /// <param name="childFileName">Entry name of the child package inside the bundle.</param>
    /// <returns>Analysis of the child package; <see cref="PackageAnalysis.FilePath"/> carries the bundle path.</returns>
    public static PackageAnalysis AnalyzeChild(string bundlePath, string childFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundlePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(childFileName);

        using var stream = File.OpenRead(bundlePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = FindEntry(archive, childFileName)
            ?? throw new StoreDownloaderException($"Bundle '{Path.GetFileName(bundlePath)}' has no entry '{childFileName}'.");

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        buffer.Position = 0;

        using var childArchive = new ZipArchive(buffer, ZipArchiveMode.Read);
        var manifestEntry = FindEntry(childArchive, AppManifestEntryName)
            ?? throw new StoreDownloaderException($"'{childFileName}' inside '{Path.GetFileName(bundlePath)}' has no {AppManifestEntryName}.");

        using var manifestStream = manifestEntry.Open();
        return ParseAppManifest(bundlePath, manifestStream);
    }

    /// <summary>Reads one entry out of a package as text.</summary>
    /// <param name="filePath">Path to a package file.</param>
    /// <param name="entryName">Entry name inside the container.</param>
    public static string? ReadEntryText(string filePath, string entryName)
    {
        using var stream = File.OpenRead(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = FindEntry(archive, entryName);
        if (entry is null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        return reader.ReadToEnd();
    }

    private static PackageAnalysis ParseAppManifest(string filePath, Stream manifestStream)
    {
        var document = XDocument.Load(manifestStream);
        var package = document.Root ?? throw NotAPackage(filePath);

        var identityElement = Child(package, "Identity");
        var identity = identityElement is null
            ? null
            : new PackageIdentityInfo(
                identityElement.Attribute("Name")?.Value ?? Path.GetFileNameWithoutExtension(filePath),
                identityElement.Attribute("Version")?.Value,
                NormaliseArchitecture(identityElement.Attribute("ProcessorArchitecture")?.Value),
                Empty(identityElement.Attribute("ResourceId")?.Value),
                identityElement.Attribute("Publisher")?.Value);

        var properties = Child(package, "Properties");
        var dependenciesElement = Child(package, "Dependencies");
        var dependencies = new List<PackageDependency>();
        string? deviceFamily = null;
        string? minimumVersion = null;

        if (dependenciesElement is not null)
        {
            foreach (var dependency in dependenciesElement.Elements().Where(element => element.Name.LocalName == "PackageDependency"))
            {
                var name = dependency.Attribute("Name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                dependencies.Add(new PackageDependency(
                    name,
                    dependency.Attribute("MinVersion")?.Value,
                    dependency.Attribute("Publisher")?.Value));
            }

            var targetDevice = dependenciesElement.Elements().FirstOrDefault(element => element.Name.LocalName == "TargetDeviceFamily");
            deviceFamily = targetDevice?.Attribute("Name")?.Value;
            minimumVersion = targetDevice?.Attribute("MinVersion")?.Value;
        }

        var hasApplications = package.Elements().Any(element => element.Name.LocalName == "Applications");
        var isFramework = string.Equals(identityElement?.Attribute("Framework")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            || !hasApplications;

        var kind = identity?.ResourceId is not null
            ? PackageKind.Resource
            : isFramework ? PackageKind.Framework : PackageKind.App;

        return new PackageAnalysis(
            filePath,
            kind,
            identity,
            dependencies,
            [],
            Text(Child(properties, "DisplayName")),
            Text(Child(properties, "PublisherDisplayName")),
            deviceFamily,
            minimumVersion);
    }

    private static PackageAnalysis ParseBundleManifest(string filePath, Stream manifestStream)
    {
        var document = XDocument.Load(manifestStream);
        var bundle = document.Root ?? throw NotAPackage(filePath);

        var identityElement = Child(bundle, "Identity");
        var identity = identityElement is null
            ? null
            : new PackageIdentityInfo(
                identityElement.Attribute("Name")?.Value ?? Path.GetFileNameWithoutExtension(filePath),
                identityElement.Attribute("Version")?.Value,
                NormaliseArchitecture(identityElement.Attribute("ProcessorArchitecture")?.Value),
                Empty(identityElement.Attribute("ResourceId")?.Value),
                identityElement.Attribute("Publisher")?.Value);

        var children = new List<BundleChildPackage>();
        var packagesElement = Child(bundle, "Packages");
        if (packagesElement is not null)
        {
            foreach (var child in packagesElement.Elements().Where(element => element.Name.LocalName == "Package"))
            {
                var fileName = child.Attribute("FileName")?.Value;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                children.Add(new BundleChildPackage(
                    fileName,
                    child.Attribute("Type")?.Value,
                    NormaliseArchitecture(child.Attribute("Architecture")?.Value),
                    child.Attribute("Version")?.Value,
                    Empty(child.Attribute("ResourceId")?.Value),
                    long.TryParse(child.Attribute("Size")?.Value, out var size) ? size : 0));
            }
        }

        return new PackageAnalysis(
            filePath,
            PackageKind.Bundle,
            identity,
            [],
            children,
            null,
            null,
            null,
            null);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryName) =>
        archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), entryName, StringComparison.OrdinalIgnoreCase));

    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string? Text(XElement? element) => string.IsNullOrWhiteSpace(element?.Value) ? null : element.Value;

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? NormaliseArchitecture(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WindowsArchitecture.Normalize(value);

    private static StoreDownloaderException NotAPackage(string filePath) =>
        new($"'{Path.GetFileName(filePath)}' has no Package element in its manifest.");
}
