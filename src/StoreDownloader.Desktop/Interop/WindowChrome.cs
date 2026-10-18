using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StoreDownloader.Desktop.Interop;

/// <summary>Which kind of see-through background the window asks DWM for.</summary>
public enum BackdropKind
{
    /// <summary>Mica: the desktop wallpaper, blurred and tinted. Subtle by design.</summary>
    Mica,

    /// <summary>Acrylic: a stronger blur of whatever is behind the window right now.</summary>
    Acrylic,

    /// <summary>No backdrop at all, a plain dark canvas.</summary>
    Solid,
}

/// <summary>
/// Gives the window the treatment Windows 11 apps have: a backdrop, a dark title bar and rounded
/// corners. Every call is best effort, and if the system cannot do it the window falls back to a
/// solid canvas instead of showing nothing.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int CornerRound = 2;

    private const int BackdropNone = 1;
    private const int BackdropMica = 2;
    private const int BackdropAcrylic = 3;

    public static void Apply(Window window, BackdropKind kind)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));

        var corners = CornerRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corners, sizeof(int));

        var backdrop = kind switch
        {
            BackdropKind.Mica => BackdropMica,
            BackdropKind.Acrylic => BackdropAcrylic,
            _ => BackdropNone,
        };

        var result = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));

        if (result != 0 || kind == BackdropKind.Solid)
        {
            // No compositor support: paint the canvas instead of leaving a hole behind the content.
            window.Background = window.TryFindResource("SolidCanvasBrush") as Brush ?? Brushes.Black;
            return;
        }

        window.Background = Brushes.Transparent;

        // Let the backdrop cover the whole client area instead of only the frame.
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }
}
