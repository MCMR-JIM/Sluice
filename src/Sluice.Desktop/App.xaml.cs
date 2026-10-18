using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Sluice.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A binding mistake must never be silent: it goes to a file next to the temp folder so it
        // can be read without a debugger, and the user sees a short message.
        var path = Path.Combine(Path.GetTempPath(), "Sluice-UI-crash.txt");
        File.WriteAllText(path, e.Exception.ToString());

        var text = new Localization().Format("CrashDetails", e.Exception.Message, path);
        MessageBox.Show(
            text,
            "Sluice",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
