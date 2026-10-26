using System.IO.Compression;
using System.Text;

namespace Sluice.Tests.Support;

/// <summary>Builds throwaway appx/msix containers so manifest parsing can be tested offline.</summary>
internal static class PackageFixtureBuilder
{
    public static string CreateAppPackage(
        string directory,
        string fileName,
        string identityName,
        string version = "1.2.3.4",
        string architecture = "x64",
        string? resourceId = null,
        bool framework = false,
        bool withApplications = true,
        params (string Name, string MinVersion)[] dependencies) =>
        CreateAppPackage(directory, fileName, identityName, version, architecture, resourceId, framework, withApplications, 0, dependencies);

    public static string CreateAppPackage(
        string directory,
        string fileName,
        string identityName,
        string version,
        string architecture,
        string? resourceId,
        bool framework,
        bool withApplications,
        int padding,
        params (string Name, string MinVersion)[] dependencies)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        var dependencyXml = new StringBuilder();
        if (dependencies.Length > 0 || !framework)
        {
            dependencyXml.Append("<Dependencies>");
            dependencyXml.Append("<TargetDeviceFamily Name=\"Windows.Desktop\" MinVersion=\"10.0.17763.0\" MaxVersionTested=\"10.0.22621.0\" />");
            foreach (var dependency in dependencies)
            {
                dependencyXml.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"<PackageDependency Name=\"{dependency.Name}\" MinVersion=\"{dependency.MinVersion}\" Publisher=\"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US\" />");
            }

            dependencyXml.Append("</Dependencies>");
        }

        var applicationsXml = withApplications
            ? "<Applications><Application Id=\"App\" Executable=\"app.exe\" EntryPoint=\"App.App\" /></Applications>"
            : string.Empty;

        var manifest = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
              <Identity Name="{identityName}" Publisher="CN=Contoso" Version="{version}" ProcessorArchitecture="{architecture}"{(resourceId is null ? string.Empty : $" ResourceId=\"{resourceId}\"")}{(framework ? " Framework=\"true\"" : string.Empty)} />
              <Properties>
                <DisplayName>{identityName} display</DisplayName>
                <PublisherDisplayName>Contoso Ltd</PublisherDisplayName>
                <Logo>logo.png</Logo>
              </Properties>
              {dependencyXml}
              {applicationsXml}
            </Package>
            """;

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "AppxManifest.xml", manifest);
        WriteEntry(archive, "AppxBlockMap.xml", "<BlockMap />");
        if (padding > 0)
        {
            // Padding makes a fixture bigger than another one, which is how "the largest file wins"
            // behaviour can be exercised without shipping a real multi-megabyte package.
            WriteEntry(archive, "Padding.bin", new string('p', padding));
        }

        return path;
    }

    public static string CreateBundle(
        string directory,
        string fileName,
        string identityName,
        string version,
        params (string ChildFile, string Type, string Architecture, string? ResourceId)[] children)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        var childXml = new StringBuilder();
        foreach (var child in children)
        {
            var resourceAttribute = child.ResourceId is null ? string.Empty : $" ResourceId=\"{child.ResourceId}\"";
            childXml.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"<Package Type=\"{child.Type}\" Version=\"{version}\" Architecture=\"{child.Architecture}\"{resourceAttribute} FileName=\"{child.ChildFile}\" Offset=\"0\" Size=\"1024\" />");
        }

        var manifest = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle">
              <Identity Name="{identityName}" Publisher="CN=Contoso" Version="{version}" />
              <Packages>
                {childXml}
              </Packages>
            </Bundle>
            """;

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "AppxMetadata/AppxBundleManifest.xml", manifest);
        WriteEntry(archive, "AppxBlockMap.xml", "<BlockMap />");
        return path;
    }

    /// <summary>Creates a bundle that really contains a child package file.</summary>
    public static string CreateBundleWithPayload(
        string directory,
        string fileName,
        string identityName,
        string version,
        string childPath,
        string childEntryName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var childSize = new FileInfo(childPath).Length;

        var manifest = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle">
              <Identity Name="{identityName}" Publisher="CN=Contoso" Version="{version}" />
              <Packages>
                <Package Type="application" Version="{version}" Architecture="x64" FileName="{childEntryName}" Offset="0" Size="{childSize}" />
              </Packages>
            </Bundle>
            """;

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "AppxMetadata/AppxBundleManifest.xml", manifest);
        archive.CreateEntryFromFile(childPath, childEntryName);
        return path;
    }

    /// <summary>Creates a container whose manifest is not valid XML, to test the error path.</summary>
    /// <param name="directory">Folder to write into.</param>
    /// <param name="fileName">File name of the package.</param>
    public static string CreateMalformedPackage(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "AppxManifest.xml", "<Package><Identity Name=\"Contoso.Broken\"");
        WriteEntry(archive, "AppxBlockMap.xml", "<BlockMap />");
        return path;
    }

    public static string CreatePlainZip(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "readme.txt", "not a package");
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
