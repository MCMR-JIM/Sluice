namespace StoreDownloader.Desktop.ViewModels;

/// <summary>A resolved package as the grid shows it.</summary>
public sealed class PackageRow
{
    public required string FileName { get; init; }

    public required string Architecture { get; init; }

    public required string Size { get; init; }

    public required string Role { get; init; }

    public required string Link { get; init; }

    public bool HasUrl => Link == "url";
}
