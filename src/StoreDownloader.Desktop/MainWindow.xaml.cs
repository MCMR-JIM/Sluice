using System.Collections.Specialized;
using System.Windows;
using StoreDownloader.Desktop.Interop;
using StoreDownloader.Desktop.ViewModels;

namespace StoreDownloader.Desktop;

public partial class MainWindow : Window, IDisposable
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();

        SourceInitialized += (_, _) => WindowChrome.Apply(this, _viewModel.Backdrop);
        _viewModel.BackdropChanged += (_, kind) => WindowChrome.Apply(this, kind);

        DataContext = _viewModel;
        _viewModel.Log.CollectionChanged += OnLogChanged;
        Closed += (_, _) =>
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
            _viewModel.Dispose();
        };
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
