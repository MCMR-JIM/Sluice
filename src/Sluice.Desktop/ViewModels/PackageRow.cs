namespace Sluice.Desktop.ViewModels;

/// <summary>What a resolved package is for, in the language the window is speaking.</summary>
public enum PackageRole
{
    Main,
    Framework,
    Resource,
}

/// <summary>A resolved package as the grid shows it.</summary>
public sealed class PackageRow(Localization loc, PackageRole role)
{
    public required string FileName { get; init; }

    public required string Architecture { get; init; }

    public required string Size { get; init; }

    public required string Link { get; init; }

    public bool HasUrl => Link == "url";

    public PackageRole Role { get; } = role;

    public string RoleText => loc.Get(Role switch
    {
        PackageRole.Main => "RoleMain",
        PackageRole.Framework => "RoleFramework",
        _ => "RoleResource",
    });
}
