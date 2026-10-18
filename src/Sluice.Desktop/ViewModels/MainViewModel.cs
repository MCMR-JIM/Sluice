using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Sluice.Download;
using Sluice.Install;
using Sluice.Packages;
using Sluice.Resolution;
using Sluice.Store;
using Sluice.Windows;
using Sluice.Diagnostics;

namespace Sluice.Desktop.ViewModels;

/// <summary>
/// Everything the window does. The pipeline mirrors the command line tool on purpose: resolve,
/// select, download, plan, install, verify, so the UI cannot drift from the CLI behaviour.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly PowerShellHost _shell = new();
    private readonly AppxInventory _inventory;
    private CancellationTokenSource? _cancellation;

    private string _reference = "https://apps.microsoft.com/detail/9nksqgp7f2nh";
    private string _architecture = WindowsArchitecture.Current;
    private string _locale = string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name) ? "en-US" : CultureInfo.CurrentUICulture.Name;
    private bool _allArchitectures;
    private bool _allUsers;
    private bool _busy;
    private string _stageKey = "StageIdle";
    private EnvironmentReport? _environment;
    private double _progress;
    private bool _progressIndeterminate;
    private string _statusLine = string.Empty;
    private string _productTitle = string.Empty;
    private string _productDetail = string.Empty;
    private StoreProduct? _product;
    private InstalledPackage? _registered;
    private string? _installedMainName;
    private bool _installSucceeded;
    private string _resultHeadline = string.Empty;
    private string _resultDetail = string.Empty;
    private Func<Localization, string>? _statusRender;
    private string? _headlineKey;
    private Func<Localization, string>? _detailRender;
    private uint? _explainCode;
    private string _explainInput = "0x80073CF3";
    private string _explainTitle = string.Empty;
    private string _explainBody = string.Empty;
    private bool _explainVisible;
    private bool _showLog;
    private string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sluice", "packages");

    public MainViewModel()
    {
        _inventory = new AppxInventory(_shell);
        ResolveCommand = new RelayCommand(() => RunAsync(PipelineMode.Resolve), () => CanRun);
        PlanCommand = new RelayCommand(() => RunAsync(PipelineMode.Plan), () => CanRun);
        DownloadCommand = new RelayCommand(() => RunAsync(PipelineMode.Download), () => CanRun);
        InstallCommand = new RelayCommand(() => RunAsync(PipelineMode.Install), () => CanRun);
        CancelCommand = new RelayCommand(Cancel, () => Busy);
        ExplainCommand = new RelayCommand(Explain, () => !string.IsNullOrWhiteSpace(ExplainInput));
        OpenFolderCommand = new RelayCommand(OpenFolder);
        CopyLogCommand = new RelayCommand(CopyLog);
        PasteCommand = new RelayCommand(Paste);
        RefreshEnvironmentCommand = new RelayCommand(() => RefreshEnvironmentAsync());

        Packages.CollectionChanged += (_, _) => Raise(nameof(PackageCountText));
        ProductTitle = Loc.Get("DetailNothingParsed");
        SetStatus(loc => loc.Get("StatusReady"));
        Loc.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(Localization.Language))
            {
                return;
            }

            Raise(nameof(Stage));
            Raise(nameof(PackageCountText));
            RebuildChips();
            RebuildProductDetail();
            RebuildRegisteredDetail();
            RebuildStatusLine();
            RebuildResultTexts();
            if (_explainCode is { } explained && AppxErrorCatalog.TryLookup(explained, out var info))
            {
                ExplainBody = DescribeError(info);
            }

            foreach (var entry in Log)
            {
                entry.RefreshLanguage();
            }
            foreach (var row in PlanRows)
            {
                row.RefreshLanguage();
            }

            RefreshPackageRows();
        };

        _ = RefreshEnvironmentAsync();
    }

    public string ToolVersion { get; } = BuildInfo.Version;

    /// <summary>Everything the window says, in the language the user picked.</summary>
    public Localization Loc { get; } = new();

    /// <summary>Sets the status line from a key so a language change can re-render it.</summary>
    /// <param name="render">Recipe for the line.</param>
    private void SetStatus(Func<Localization, string> render)
    {
        _statusRender = render;
        StatusLine = render(Loc);
    }

    /// <summary>Sets the status line to text that is already final.</summary>
    /// <param name="text">Text to show.</param>
    private void SetStatus(string text)
    {
        _statusRender = null;
        StatusLine = text;
    }

    /// <summary>Sets the headline and the detail from keys the language change re-renders.</summary>
    /// <param name="headlineKey">Key of the headline.</param>
    /// <param name="detail">Recipe for the detail, when there is one.</param>
    private void SetResult(string headlineKey, Func<Localization, string>? detail = null)
    {
        _headlineKey = headlineKey;
        _detailRender = detail;
        ResultHeadline = Loc.Get(headlineKey);
        ResultDetail = detail is null ? string.Empty : detail(Loc);
    }

    private void SetStage(string key)
    {
        if (_stageKey == key)
        {
            return;
        }

        _stageKey = key;
        Raise(nameof(Stage));
    }

    public ObservableCollection<PackageRow> Packages { get; } = [];

    public ObservableCollection<PlanRow> PlanRows { get; } = [];

    public ObservableCollection<LogEntry> Log { get; } = [];

    public ObservableCollection<EnvironmentChip> Chips { get; } = [];

    /// <summary>The package counter in the list header, in the current language.</summary>
    public string PackageCountText => Loc.Format("PackageCount", Packages.Count);

    public RelayCommand ResolveCommand { get; }

    public RelayCommand PlanCommand { get; }

    public RelayCommand DownloadCommand { get; }

    public RelayCommand InstallCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand ExplainCommand { get; }

    public RelayCommand OpenFolderCommand { get; }

    public RelayCommand CopyLogCommand { get; }

    public RelayCommand PasteCommand { get; }

    public RelayCommand RefreshEnvironmentCommand { get; }

    public IReadOnlyList<string> Architectures { get; } = ["x64", "x86", "arm64"];

    public string Reference
    {
        get => _reference;
        set
        {
            if (SetProperty(ref _reference, value))
            {
                RaiseCommands();
            }
        }
    }

    public string Architecture
    {
        get => _architecture;
        set
        {
            if (SetProperty(ref _architecture, value))
            {
                Raise(nameof(IsX64));
                Raise(nameof(IsX86));
                Raise(nameof(IsArm64));
            }
        }
    }

    public bool IsX64
    {
        get => Architecture == "x64";
        set { if (value) { Architecture = "x64"; } }
    }

    public bool IsX86
    {
        get => Architecture == "x86";
        set { if (value) { Architecture = "x86"; } }
    }

    public bool IsArm64
    {
        get => Architecture == "arm64";
        set { if (value) { Architecture = "arm64"; } }
    }

    public string Locale
    {
        get => _locale;
        set => SetProperty(ref _locale, value);
    }

    public bool AllArchitectures
    {
        get => _allArchitectures;
        set => SetProperty(ref _allArchitectures, value);
    }

    public bool AllUsers
    {
        get => _allUsers;
        set => SetProperty(ref _allUsers, value);
    }

    public bool ShowLog
    {
        get => _showLog;
        set
        {
            if (SetProperty(ref _showLog, value))
            {
                Raise(nameof(ShowPackages));
            }
        }
    }

    public bool ShowPackages
    {
        get => !_showLog;
        set
        {
            if (value)
            {
                ShowLog = false;
            }
        }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetProperty(ref _busy, value))
            {
                Raise(nameof(CanRun));
                RaiseCommands();
            }
        }
    }

    public bool CanRun => !Busy;

    public string Stage => Loc.Get(_stageKey);

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        private set => SetProperty(ref _progressIndeterminate, value);
    }

    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    public string ProductTitle
    {
        get => _productTitle;
        private set => SetProperty(ref _productTitle, value);
    }

    public string ProductDetail
    {
        get => _productDetail;
        private set => SetProperty(ref _productDetail, value);
    }

    public string ResultHeadline
    {
        get => _resultHeadline;
        private set => SetProperty(ref _resultHeadline, value);
    }

    public string ResultDetail
    {
        get => _resultDetail;
        private set => SetProperty(ref _resultDetail, value);
    }

    public string CacheDirectory
    {
        get => _cacheDirectory;
        private set => SetProperty(ref _cacheDirectory, value);
    }

    public string ExplainInput
    {
        get => _explainInput;
        set
        {
            if (SetProperty(ref _explainInput, value))
            {
                ExplainCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ExplainTitle
    {
        get => _explainTitle;
        private set => SetProperty(ref _explainTitle, value);
    }

    public string ExplainBody
    {
        get => _explainBody;
        private set => SetProperty(ref _explainBody, value);
    }

    public bool ExplainVisible
    {
        get => _explainVisible;
        private set => SetProperty(ref _explainVisible, value);
    }

    private async Task RunAsync(PipelineMode mode)
    {
        if (!StoreUrlParser.TryParse(Reference, out var request, out var error))
        {
            AddLog(LogLevel.Error, error ?? Loc.Get("LogUnrecognised"));
            StatusLine = error ?? Loc.Get("LogUnrecognised");
            return;
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        Busy = true;
        ProgressIndeterminate = true;
        ResultHeadline = string.Empty;
        ResultDetail = string.Empty;
        PlanRows.Clear();
        Packages.Clear();

        try
        {
            await ExecuteAsync(mode, request, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            AddLog(LogLevel.Warning, loc => loc.Get("LogCancelled"));
            SetStatus(loc => loc.Get("StatusCancelled"));
        }
        catch (SluiceException ex)
        {
            AddLog(LogLevel.Error, ex.Message);
            StatusLine = ex.Message;
            ResultHeadline = Loc.Get("ResultUnfinished");
            ResultDetail = ex.Message;
        }
        catch (Exception ex)
        {
            AddLog(LogLevel.Error, ex.Message);
            StatusLine = ex.Message;
        }
        finally
        {
            Busy = false;
            ProgressIndeterminate = false;
            SetStage("StageIdle");
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task ExecuteAsync(PipelineMode mode, StoreRequest request, CancellationToken token)
    {
        SetStage("StageResolve");
        SetStatus(loc => loc.Format("StatusResolving", request.ProductId));
        AddLog(LogLevel.Command, loc => $"{ModeLabel(mode)} · {Reference}");

        using var resolver = new StorePackageResolver();
        var resolution = await resolver.ResolveAsync(request, Locale, token).ConfigureAwait(true);

        _product = resolution.Product;
        ProductTitle = resolution.Product.Title;
        RebuildProductDetail();

        foreach (var package in resolution.Packages)
        {
            var identity = Fe3.PackageIdentifier.TryParse(package.Package.InstallerSpecificIdentifier, out var parsed)
                ? parsed
                : null;
            Packages.Add(new PackageRow(Loc, identity?.ResourceId is not null
                ? PackageRole.Resource
                : package.Package.IsFramework ? PackageRole.Framework : PackageRole.Main)
            {
                FileName = package.Package.ReadableFileName,
                Architecture = package.Package.Architecture ?? "neutral",
                Size = UiFormat.Size(package.Package.SizeInBytes),
                Link = package.IsDownloadable ? "url" : Loc.Get("LinkNone"),
            });
        }

        AddLog(LogLevel.Info, loc => loc.Format("LogParsed", resolution.Packages.Count, UiFormat.Size(resolution.TotalSizeInBytes)));

        if (mode == PipelineMode.Resolve)
        {
            SetStatus(loc => loc.Get("StatusResolved"));
            SetResult(
                "ResultResolved",
                loc => $"{resolution.Product.Title} · {loc.Format("DetailPackageCount", resolution.Packages.Count)}");
            return;
        }

        if (!OperatingSystem.IsWindows() && mode == PipelineMode.Install)
        {
            throw new SluiceException(Loc.Get("LogNeedsWindows"));
        }

        var selection = PackageSelector.Select(
            resolution,
            new PackageSelectionOptions(Architecture, Locale, IncludeAllArchitectures: AllArchitectures));

        Directory.CreateDirectory(CacheDirectory);
        using var downloader = new PackageDownloader();
        var downloadOptions = new DownloadOptions(
            MaxParallelism: 3,
            Progress: OnDownloadProgress);

        List<string> files;
        if (mode is PipelineMode.Plan or PipelineMode.Install)
        {
            SetStage("StageDownload");
            ProgressIndeterminate = false;
            Progress = 0;
            var preparation = await new PackagePreparer(downloader)
                .PrepareAsync(resolution, selection, CacheDirectory, Architecture, Locale, downloadOptions, token)
                .ConfigureAwait(true);

            foreach (var note in preparation.Notes)
            {
                AddLog(LogLevel.Info, loc => loc.Text(note));
            }

            files = preparation.Downloaded.Select(result => result.Path).ToList();
            AddLog(LogLevel.Success, loc => loc.Format(
                "DownloadedPackages", files.Count, UiFormat.Size(preparation.Downloaded.Sum(r => r.SizeInBytes))));
        }
        else
        {
            SetStage("StageDownload");
            ProgressIndeterminate = false;
            Progress = 0;
            var results = await downloader
                .DownloadManyAsync(selection.All, CacheDirectory, downloadOptions, token)
                .ConfigureAwait(true);

            files = results.Select(result => result.Path).ToList();
            AddLog(LogLevel.Success, loc => loc.Format(
                "DownloadedPackages", files.Count, UiFormat.Size(results.Sum(r => r.SizeInBytes))));
        }

        SetStage("StagePlan");
        ProgressIndeterminate = true;
        var planner = _shell.IsAvailable ? new InstallPlanner(_inventory) : new InstallPlanner();
        var plan = await planner
            .CreatePlanAsync(files, new InstallPlannerOptions(Architecture, Locale), token)
            .ConfigureAwait(true);

        PlanRows.Clear();
        var order = 1;
        foreach (var step in plan.Steps)
        {
            PlanRows.Add(new PlanRow(Loc) { Order = order++, Step = step });
        }

        foreach (var issue in plan.Issues)
        {
            var level = issue.Severity switch
            {
                PlanIssueSeverity.Error => LogLevel.Error,
                PlanIssueSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Info,
            };
            AddLog(
                level,
                loc => issue.Remediation is null
                    ? loc.Text(issue.Message)
                    : $"{loc.Text(issue.Message)} → {loc.Text(issue.Remediation)}");
        }

        if (mode == PipelineMode.Download)
        {
            SetStatus(loc => loc.Get("StatusDownloaded"));
            SetResult("ResultDownloaded", loc => loc.Format("DetailFilesIn", files.Count, CacheDirectory));
            return;
        }

        if (!plan.CanProceed)
        {
            SetStatus(loc => loc.Get("StatusPlanBlocked"));
            SetResult("ResultBlocked", loc => loc.Get("DetailBlocked"));
            return;
        }

        if (mode == PipelineMode.Plan)
        {
            SetStatus(loc => loc.Get("StatusPlanned"));
            SetResult(
                "ResultPlanned",
                loc => loc.Format("DetailPlanSteps", plan.Steps.Count, plan.MainPackageName, plan.MainVersion));
            return;
        }

        SetStage("StageInstall");
        ProgressIndeterminate = true;
        var installer = new AppxInstaller(
            _shell,
            _inventory,
            new AppxInstallerOptions(AllUsers: AllUsers, ForceShutdown: true, AllowRegisterFallback: true));

        var progress = new Progress<InstallStepResult>(result => OnInstallStep(result, plan));
        var outcome = await installer.InstallAsync(plan, progress, token).ConfigureAwait(true);

        if (outcome.Succeeded)
        {
            SetStage("StageDone");
            ProgressIndeterminate = false;
            Progress = 100;
            SetResult("ResultInstalled");
            _registered = outcome.Registered;
            _installedMainName = plan.MainPackageName;
            _installSucceeded = true;
            RebuildRegisteredDetail();
            AddLog(LogLevel.Success, ResultDetail);
            SetStatus(loc => loc.Get("StatusInstalled"));
        }
        else
        {
            SetStage("StageFailed");
            ProgressIndeterminate = false;
            _installSucceeded = false;
            var failure = outcome.StepResults.FirstOrDefault(result => !result.Succeeded);
            SetResult("ResultInstallFailed");
            ResultDetail = failure?.Error is null
                ? failure?.Message ?? Loc.Get("UnknownError")
                : $"{failure.Error.FormattedCode} {failure.Error.Name} · {failure.Error.Meaning}";
            AddLog(LogLevel.Error, ResultDetail);
            if (failure?.Error is not null)
            {
                var error = failure.Error;
                (string Cause, string Steps) explain = Loc.Language == UiLanguage.Chinese
                    && ErrorExplainText.TryGet(error.Code, out var translated)
                        ? (translated.Cause, translated.Steps)
                        : (error.TypicalCause, error.ManualSteps);
                AddLog(LogLevel.Info, loc => $"{loc.Get("ExplainWhy")}: {explain.Cause}");
                AddLog(LogLevel.Info, loc => $"{loc.Get("ExplainHow")}: {explain.Steps}");
            }

            SetStatus(loc => loc.Get("StatusInstallFailed"));
        }
    }

    private void OnDownloadProgress(DownloadProgress progress)
    {
        void Apply()
        {
            Progress = Math.Round(progress.Fraction * 100, 1);
            StatusLine = Loc.Format("StatusDownloading", progress.FileName, UiFormat.Size(progress.BytesReceived), UiFormat.Size(progress.TotalBytes));
        }

        Dispatcher(Apply);
    }

    private void OnInstallStep(InstallStepResult result, InstallPlan plan)
    {
        void Apply()
        {
            var row = PlanRows.FirstOrDefault(candidate => candidate.Step == result.Step);
            if (row is not null)
            {
                row.State = result.Succeeded ? "ok" : "failed";
                row.Detail = result.HResult is null
                    ? result.Message
                    : AppxErrorCatalog.Format(result.HResult.Value);
            }

            var level = result.Succeeded ? LogLevel.Success : LogLevel.Error;
            AddLog(
                level,
                loc => $"{loc.Get(result.Succeeded ? "StateOk" : "StateFailed")} · {result.Step.FileName}"
                    + (result.HResult is null ? string.Empty : $" · {AppxErrorCatalog.Format(result.HResult.Value)}"));

            var done = PlanRows.Count(row2 => row2.State is "ok" or "failed");
            Progress = PlanRows.Count == 0 ? 0 : Math.Round(done * 100d / PlanRows.Count, 1);
        }

        Dispatcher(Apply);
    }

    private async Task RefreshEnvironmentAsync()
    {
        try
        {
            var report = await WindowsEnvironmentProbe.ProbeAsync(_shell).ConfigureAwait(true);
            _environment = report;
            RebuildChips();

            foreach (var warning in report.Warnings)
            {
                AddLog(LogLevel.Warning, loc => loc.Text(warning));
            }

            StatusLine = report.Warnings.Count == 0
                ? Loc.Get("StatusEnvironment")
                : Loc.Format("StatusEnvironmentWarnings", report.Warnings.Count);
        }
        catch (Exception ex)
        {
            AddLog(LogLevel.Warning, loc => loc.Format("EnvCheckFailed", ex.Message));
        }
    }

    /// <summary>Re-renders the product subtitle in the current language.</summary>
    private void RebuildProductDetail()
    {
        if (_product is not { } product)
        {
            ProductTitle = Loc.Get("DetailNothingParsed");
            return;
        }

        ProductDetail = $"{product.ProductId} · {product.Publisher ?? Loc.Get("UnknownPublisher")} · "
            + $"{product.Market}/{product.Locale} · {Loc.Format("ProductCategories", product.WuCategoryIds.Count)}";
    }

    /// <summary>Re-renders the status line in the current language.</summary>
    private void RebuildStatusLine()
    {
        if (Busy)
        {
            return;
        }

        if (_statusRender is not null)
        {
            StatusLine = _statusRender(Loc);
            return;
        }

        StatusLine = _environment is { } report
            ? report.Warnings.Count == 0
                ? Loc.Get("StatusEnvironment")
                : Loc.Format("StatusEnvironmentWarnings", report.Warnings.Count)
            : Loc.Get("StatusReady");
    }

    /// <summary>Re-renders the headline and the detail in the current language.</summary>
    private void RebuildResultTexts()
    {
        if (_headlineKey is not null)
        {
            ResultHeadline = Loc.Get(_headlineKey);
        }

        if (_installSucceeded)
        {
            RebuildRegisteredDetail();
            return;
        }

        if (_detailRender is not null)
        {
            ResultDetail = _detailRender(Loc);
        }
    }

    /// <summary>Re-renders the install verdict in the current language.</summary>
    private void RebuildRegisteredDetail()
    {
        if (!_installSucceeded)
        {
            return;
        }

        ResultDetail = _registered is null
            ? Loc.Format("DetailInstalled", _installedMainName ?? string.Empty)
            : Loc.Format("RegisteredStatus", _registered.PackageFullName, _registered.Status);
    }

    /// <summary>
    /// Refreshes the header chips in the current language. The four chips are created once and
    /// updated in place: clearing and repopulating the collection in a single pass makes WPF drop
    /// the first containers, which is why the machine name used to disappear after a language flip.
    /// </summary>
    private void RebuildChips()
    {
        if (_environment is not { } report)
        {
            return;
        }

        while (Chips.Count < 4)
        {
            Chips.Add(new EnvironmentChip(string.Empty, "neutral"));
        }

        Chips[0].Text = report.Architecture;
        Chips[0].Tone = "neutral";
        Chips[1].Text = report.ProductName is null ? "Windows" : $"{report.ProductName} {report.Build}";
        Chips[1].Tone = "neutral";
        Chips[2].Text = Loc.Get(report.SideloadingEnabled switch { true => "ChipSideloadOn", false => "ChipSideloadOff", null => "ChipSideloadUnknown" });
        Chips[2].Tone = report.SideloadingEnabled switch { true => "ok", false => "bad", null => "warn" };
        Chips[3].Text = Loc.Get(report.DeveloperModeEnabled switch { true => "ChipDevOn", false => "ChipDevOff", null => "ChipDevUnknown" });
        Chips[3].Tone = report.DeveloperModeEnabled switch { true => "ok", false => "warn", null => "neutral" };
    }

    private void Explain()
    {
        if (!AppxErrorCatalog.TryLookup(ExplainInput, out var info))
        {
            if (AppxErrorCatalog.TryParse(ExplainInput, out var code))
            {
                ExplainTitle = AppxErrorCatalog.Format(code);
                ExplainBody = Loc.Get("ExplainUnknownCode");
            }
            else
            {
                ExplainTitle = Loc.Get("ExplainUnparsableTitle");
                ExplainBody = Loc.Get("ExplainUnparsable");
            }

            ExplainVisible = true;
            return;
        }

        _explainCode = info.Code;
        ExplainTitle = $"{info.FormattedCode} · {info.Name}";
        ExplainBody = DescribeError(info);
        ExplainVisible = true;
    }

    /// <summary>Explains a catalogued error in the language the window speaks.</summary>
    /// <param name="info">Catalogued error.</param>
    private string DescribeError(AppxErrorInfo info) =>
        Loc.Language == UiLanguage.Chinese && ErrorExplainText.TryGet(info.Code, out var chinese)
            ? Loc.Format("ExplainBody", chinese.Meaning, chinese.Cause, chinese.Steps)
            : Loc.Format("ExplainBody", info.Meaning, info.TypicalCause, info.ManualSteps);

    private void Cancel() => _cancellation?.Cancel();

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private void OpenFolder()
    {
        Directory.CreateDirectory(CacheDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CacheDirectory}\"") { UseShellExecute = true });
    }

    private void CopyLog()
    {
        var text = string.Join(Environment.NewLine, Log.Select(entry => $"{entry.Time}  {entry.Level,-7} {entry.Text}"));
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            SetStatus(loc => loc.Get("StatusCopied"));
        }
    }

    private void Paste()
    {
        if (Clipboard.ContainsText())
        {
            Reference = Clipboard.GetText().Trim();
        }
    }

    /// <summary>Re-reads the link state of every row after the language changed.</summary>
    private void RefreshPackageRows()
    {
        for (var index = 0; index < Packages.Count; index++)
        {
            var row = Packages[index];
            Packages[index] = new PackageRow(Loc, row.Role)
            {
                FileName = row.FileName,
                Architecture = row.Architecture,
                Size = row.Size,
                Link = row.Link,
            };
        }
    }

    private void AddLog(LogLevel level, string text) =>
        Log.Add(LogEntry.Plain(Loc, level, text));

    /// <summary>Adds a line that is re-rendered whenever the language changes.</summary>
    /// <param name="level">Severity shown in the gutter.</param>
    /// <param name="render">Recipe for the text, given the current localization.</param>
    private void AddLog(LogLevel level, Func<Localization, string> render) =>
        Log.Add(LogEntry.Rendered(Loc, level, render));

    private static void Dispatcher(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private string ModeLabel(PipelineMode mode) => Loc.Get(mode switch
    {
        PipelineMode.Resolve => "StageResolve",
        PipelineMode.Download => "StageDownload",
        PipelineMode.Plan => "StagePlan",
        _ => "StageInstall",
    });

    private void RaiseCommands()
    {
        ResolveCommand.RaiseCanExecuteChanged();
        PlanCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
        InstallCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ExplainCommand.RaiseCanExecuteChanged();
    }
}

/// <summary>A short status token shown in the header.</summary>
public sealed class EnvironmentChip : ObservableObject
{
    private string _text;
    private string _tone;

    public EnvironmentChip(string text, string tone)
    {
        _text = text;
        _tone = tone;
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string Tone
    {
        get => _tone;
        set => SetProperty(ref _tone, value);
    }
}

internal enum PipelineMode
{
    Resolve,
    Download,
    Plan,
    Install,
}