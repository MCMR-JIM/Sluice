using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StoreDownloader.Desktop.Interop;

/// <summary>
/// Gives the window the treatment Windows 11 apps have: the Mica backdrop (the blurred surface the
/// desktop shows through), a dark title bar and rounded corners. Every call is best effort, and if
/// the system does not support the backdrop the window falls back to a solid dark canvas.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DWM_WINDOW_CORNER_PREFERENCE_ROUND = 2;
    private const int DWM_SYSTEMBACKDROP_TYPE_MAINWINDOW = 2; // Mica

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));

        var corners = DWM_WINDOW_CORNER_PREFERENCE_ROUND;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corners, sizeof(int));

        var backdrop = DWM_SYSTEMBACKDROP_TYPE_MAINWINDOW;
        var backdropResult = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));

        if (backdropResult != 0)
        {
            // Pre Windows 11 22H2: no Mica, so paint the canvas ourselves instead of showing nothing.
            window.Background = window.TryFindResource("SolidCanvasBrush") as Brush ?? Brushes.Black;
            return;
        }

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
