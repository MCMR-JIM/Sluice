using System.Text.Json;

namespace Sluice.Windows;

/// <summary>Window geometry in device independent pixels.</summary>
/// <param name="Left">Distance of the left edge from the left of the desktop.</param>
/// <param name="Top">Distance of the top edge from the top of the desktop.</param>
/// <param name="Width">Width of the window.</param>
/// <param name="Height">Height of the window.</param>
public readonly record struct WindowBox(int Left, int Top, int Width, int Height);

/// <summary>
/// Keeps a window inside the area it has to live in. A remembered position can belong to a monitor that
/// is no longer attached, or a remembered size can be taller than the screen it is restored on, which is
/// how a window ends up with its status bar below the bottom edge.
/// </summary>
public static class WindowPlacement
{
    /// <summary>Smallest width worth restoring.</summary>
    public const int MinimumWidth = 900;

    /// <summary>Smallest height worth restoring.</summary>
    public const int MinimumHeight = 560;

    /// <summary>
    /// Fits a remembered box into the work area: the size is capped to what fits, the position is pulled
    /// back inside, and a box that would be unusably small is raised to the minimum when the area allows.
    /// </summary>
    /// <param name="desired">Remembered or requested geometry.</param>
    /// <param name="workArea">Area the window may occupy, excluding the taskbar.</param>
    public static WindowBox Clamp(WindowBox desired, WindowBox workArea)
    {
        var width = Math.Clamp(desired.Width, Math.Min(MinimumWidth, workArea.Width), workArea.Width);
        var height = Math.Clamp(desired.Height, Math.Min(MinimumHeight, workArea.Height), workArea.Height);

        var left = Math.Clamp(desired.Left, workArea.Left, Math.Max(workArea.Left, workArea.Left + workArea.Width - width));
        var top = Math.Clamp(desired.Top, workArea.Top, Math.Max(workArea.Top, workArea.Top + workArea.Height - height));

        return new WindowBox(left, top, width, height);
    }
}

/// <summary>
/// Remembers where the window was, next to the other files the tool writes. A corrupt or unreadable file
/// is treated as "no memory", because failing to reopen the window over a settings file would be absurd.
/// </summary>
public static class WindowPlacementStore
{
    private const string FolderName = "sluice";
    private const string FileName = "window.json";

    /// <summary>Where the window geometry is kept.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName,
        FileName);

    /// <summary>Reads the remembered geometry.</summary>
    /// <param name="path">File to read; the default location when omitted.</param>
    /// <returns>The remembered box, or <c>null</c> when there is nothing usable.</returns>
    public static WindowBox? Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var json = File.ReadAllText(file);
            var box = JsonSerializer.Deserialize<WindowBox>(json);
            return box.Width > 0 && box.Height > 0 ? box : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Remembers the geometry.</summary>
    /// <param name="box">Geometry to write.</param>
    /// <param name="path">File to write; the default location when omitted.</param>
    public static void Save(WindowBox box, string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, JsonSerializer.Serialize(box));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Remembering the position is a convenience; a read only profile must not stop the window.
        }
    }
}
