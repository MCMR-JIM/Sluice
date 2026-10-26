using System.Collections.Specialized;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sluice.Desktop.Interop;
using Sluice.Desktop.ViewModels;
using Sluice.Windows;

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
        Loaded += (_, _) => RestorePlacement();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Log.CollectionChanged += OnLogChanged;
        Closing += (_, _) => RememberPlacement();
        Closed += (_, _) =>
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
            _viewModel.Dispose();
        };
    }

    /// <summary>
    /// Restores the remembered size and position, clamped into the work area: a window remembered on a
    /// bigger screen or on a monitor that is no longer attached used to open with its status row below
    /// the bottom edge.
    /// </summary>
    private void RestorePlacement()
    {
        if (WindowPlacementStore.Load() is not { } remembered)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var clamped = WindowPlacement.Clamp(
            remembered,
            new WindowBox((int)workArea.Left, (int)workArea.Top, (int)workArea.Width, (int)workArea.Height));

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = clamped.Left;
        Top = clamped.Top;
        Width = clamped.Width;
        Height = clamped.Height;
    }

    /// <summary>Remembers the size and position, using the restored bounds when maximized.</summary>
    private void RememberPlacement()
    {
        // RestoreBounds is the only useful value while the window is maximized or minimized.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        if (bounds.Width <= 0 || bounds.Height <= 0 || double.IsNaN(bounds.Width))
        {
            return;
        }

        WindowPlacementStore.Save(new WindowBox(
            (int)bounds.Left,
            (int)bounds.Top,
            (int)bounds.Width,
            (int)bounds.Height));
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
