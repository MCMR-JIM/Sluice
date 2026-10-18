using System.Collections.Specialized;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sluice.Desktop.Interop;
using Sluice.Desktop.ViewModels;

namespace Sluice.Desktop;

public partial class MainWindow : Window, IDisposable
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // The title bar icon comes from the embedded mark; the exe icon is set at build time by
        // ApplicationIcon, and both are generated from assets/logo.png.
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.png", UriKind.Absolute));

        SourceInitialized += OnSourceInitialized;

        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Log.CollectionChanged += OnLogChanged;
        Closed += (_, _) =>
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
            _viewModel.Dispose();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Without this the WPF composition surface is cleared with an opaque colour and the acrylic
        // behind the window is covered, no matter how transparent the brushes are.
        if (PresentationSource.FromVisual(this) is HwndSource source && source.CompositionTarget is { } target)
        {
            target.BackgroundColor = Colors.Transparent;
        }

        WindowChrome.Apply(this);
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            LogScroll.ScrollToEnd();
        }
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
