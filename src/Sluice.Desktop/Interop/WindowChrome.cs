using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Sluice.Desktop.Interop;

/// <summary>
/// Gives the window what Windows 11 apps look like: an acrylic backdrop (the desktop behind it is
/// blurred, not hidden), a dark title bar and rounded corners. If the compositor refuses, the window
/// falls back to a solid canvas instead of showing a hole.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int CornerRound = 2;
    private const int BackdropAcrylic = 3;

    public static void Apply(Window window)
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

        var backdrop = BackdropAcrylic;
        var result = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));

        if (result != 0)
        {
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
