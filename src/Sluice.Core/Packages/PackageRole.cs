using Sluice.Fe3;

namespace Sluice.Packages;

/// <summary>
/// What a package is for, in the words the command line prints. A dry run used to answer this with
/// "framework or main", which labelled every resource package as the application itself.
/// </summary>
public static class PackageRole
{
    /// <summary>Role of <paramref name="package"/>: <c>resource</c>, <c>framework</c> or <c>main</c>.</summary>
    /// <param name="package">Package to classify.</param>
    public static string Of(WuPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (PackageIdentifier.TryParse(package.InstallerSpecificIdentifier, out var parts)
            && !string.IsNullOrWhiteSpace(parts.ResourceId))
        {
            // A resource package carries a locale in its identifier; Windows installs it next to the app.
            return "resource";
        }

        return package.IsFramework ? "framework" : "main";
    }
}
