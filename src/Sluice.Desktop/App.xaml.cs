using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Sluice.Desktop;

public partial class App : Application
{
    /// <summary>Identity the shell groups, pins and labels this window with.</summary>
    private const string AppUserModelId = "MCMR-JIM.Sluice";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Without an explicit id the shell names the taskbar entry after the executable file and
        // resolves the icon per file path, which is how a stale cache ended up drawing the generic
        // window glyph instead of the mark.
        if (SetCurrentProcessExplicitAppUserModelID(AppUserModelId) < 0)
        {
            // Cosmetic only: the window still works when the shell refuses the id.
        }

        DispatcherUnhandledException += OnUnhandledException;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A binding mistake must never be silent, but reporting it must not become a second failure
        // either: every step here is guarded, because an exception raised inside this handler used to
        // take the process down without a dialog and without a log. The message is assembled from a
        // template without placeholders for the same reason - a template edit could otherwise throw
        // a FormatException exactly where everything is already going wrong.
        var path = Path.Combine(Path.GetTempPath(), "Sluice-UI-crash.txt");
        try
        {
            File.WriteAllText(path, e.Exception.ToString());
        }
        catch (Exception)
        {
            // A read only or locked temp folder; the dialog below still names the failure.
        }

        try
        {
            var text = e.Exception.Message
                + Environment.NewLine
                + Environment.NewLine
                + new Localization().Get("CrashDetails")
                + path;
            MessageBox.Show(text, "Sluice", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception)
        {
            // Nothing better is left to do than let the process end.
        }

        e.Handled = true;
    }
}
