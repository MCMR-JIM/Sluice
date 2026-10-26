namespace Sluice.Download;

/// <summary>What a folder of downloads actually contains, according to its manifest.</summary>
/// <param name="Files">Package files to plan against, in manifest order.</param>
/// <param name="Missing">Files the manifest names that are no longer on disk.</param>
/// <param name="FromManifest">True when a manifest decided the list.</param>
public sealed record FolderSelection(IReadOnlyList<string> Files, IReadOnlyList<string> Missing, bool FromManifest)
{
    /// <summary>All package files the folder holds, without a manifest to narrow them down.</summary>
    /// <param name="files">Every package file found.</param>
    public static FolderSelection Everything(IReadOnlyList<string> files) => new(files, [], false);
}
