using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using StoreDownloader.Download;
using StoreDownloader.Install;
using StoreDownloader.Packages;
using StoreDownloader.Resolution;
using StoreDownloader.Store;
using StoreDownloader.Windows;
using StoreDownloader.Diagnostics;

namespace StoreDownloader.Desktop.ViewModels;

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
    private string _stage = "空闲";
    private double _progress;
    private bool _progressIndeterminate;
    private string _statusLine = "粘贴一个 Store 链接，或直接输入产品 ID。";
    private string _productTitle = "还没有解析任何产品";
    private string _productDetail = string.Empty;
    private string _resultHeadline = string.Empty;
    private string _resultDetail = string.Empty;
    private string _explainInput = "0x80073CF3";
    private string _explainTitle = string.Empty;
    private string _explainBody = string.Empty;
    private bool _explainVisible;
    private bool _showLog;
    private string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "storedl", "packages");

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

        _ = RefreshEnvironmentAsync();
    }

    public string ToolVersion { get; } = $"{BuildInfo.ToolName} {BuildInfo.Version}";

    public ObservableCollection<PackageRow> Packages { get; } = [];

    public ObservableCollection<PlanRow> PlanRows { get; } = [];

    public ObservableCollection<LogEntry> Log { get; } = [];

    public ObservableCollection<EnvironmentChip> Chips { get; } = [];

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

    public string Stage
    {
        get => _stage;
        private set => SetProperty(ref _stage, value);
    }

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
            AddLog(LogLevel.Error, error ?? "无法识别的链接。");
            StatusLine = error ?? "无法识别的链接。";
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
            AddLog(LogLevel.Warning, "已取消。");
            StatusLine = "已取消。";
        }
        catch (StoreDownloaderException ex)
        {
            AddLog(LogLevel.Error, ex.Message);
            StatusLine = ex.Message;
            ResultHeadline = "未完成";
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
            Stage = "空闲";
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task ExecuteAsync(PipelineMode mode, StoreRequest request, CancellationToken token)
    {
        Stage = "解析";
        StatusLine = $"正在解析 {request.ProductId}…";
        AddLog(LogLevel.Command, $"{ModeLabel(mode)} · {Reference}");

        using var resolver = new StorePackageResolver();
        var resolution = await resolver.ResolveAsync(request, Locale, token).ConfigureAwait(true);

        ProductTitle = resolution.Product.Title;
        ProductDetail = $"{resolution.Product.ProductId} · {resolution.Product.Publisher ?? "未知发布者"} · "
            + $"{resolution.Product.Market}/{resolution.Product.Locale} · {resolution.Product.WuCategoryIds.Count} 个分类";

        foreach (var package in resolution.Packages)
        {
            var identity = Fe3.PackageIdentifier.TryParse(package.Package.InstallerSpecificIdentifier, out var parsed)
                ? parsed
                : null;
            Packages.Add(new PackageRow
            {
                FileName = package.Package.ReadableFileName,
                Architecture = package.Package.Architecture ?? "neutral",
                Size = UiFormat.Size(package.Package.SizeInBytes),
                Role = identity?.ResourceId is not null ? "资源包" : package.Package.IsFramework ? "框架" : "主程序",
                Link = package.IsDownloadable ? "url" : "无链接",
            });
        }

        AddLog(LogLevel.Info, $"解析到 {resolution.Packages.Count} 个包，合计 {UiFormat.Size(resolution.TotalSizeInBytes)}。");

        if (mode == PipelineMode.Resolve)
        {
            StatusLine = "解析完成。";
            ResultHeadline = "解析完成";
            ResultDetail = $"{resolution.Product.Title} · {resolution.Packages.Count} 个包";
            return;
        }

        if (!OperatingSystem.IsWindows() && mode == PipelineMode.Install)
        {
            throw new StoreDownloaderException("安装需要 Windows。");
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
            Stage = "下载";
            ProgressIndeterminate = false;
            Progress = 0;
            var preparation = await new PackagePreparer(downloader)
                .PrepareAsync(resolution, selection, CacheDirectory, Architecture, Locale, downloadOptions, token)
                .ConfigureAwait(true);

            foreach (var note in preparation.Notes)
            {
                AddLog(LogLevel.Info, note);
            }

            files = preparation.Downloaded.Select(result => result.Path).ToList();
            AddLog(LogLevel.Success,
                $"已下载 {files.Count} 个包，{UiFormat.Size(preparation.Downloaded.Sum(r => r.SizeInBytes))}。");
        }
        else
        {
            Stage = "下载";
            ProgressIndeterminate = false;
            Progress = 0;
            var results = await downloader
                .DownloadManyAsync(selection.All, CacheDirectory, downloadOptions, token)
                .ConfigureAwait(true);

            files = results.Select(result => result.Path).ToList();
            AddLog(LogLevel.Success,
                $"已下载 {results.Count} 个包，{UiFormat.Size(results.Sum(r => r.SizeInBytes))}。");
        }

        Stage = "规划";
        ProgressIndeterminate = true;
        var planner = _shell.IsAvailable ? new InstallPlanner(_inventory) : new InstallPlanner();
        var plan = await planner
            .CreatePlanAsync(files, new InstallPlannerOptions(Architecture, Locale), token)
            .ConfigureAwait(true);

        PlanRows.Clear();
        var order = 1;
        foreach (var step in plan.Steps)
        {
            PlanRows.Add(new PlanRow { Order = order++, Step = step });
        }

        foreach (var issue in plan.Issues)
        {
            var level = issue.Severity switch
            {
                PlanIssueSeverity.Error => LogLevel.Error,
                PlanIssueSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Info,
            };
            AddLog(level, issue.Remediation is null ? issue.Message : $"{issue.Message} → {issue.Remediation}");
        }

        if (mode == PipelineMode.Download)
        {
            StatusLine = "下载完成。";
            ResultHeadline = "下载完成";
            ResultDetail = $"{files.Count} 个包在 {CacheDirectory}";
            return;
        }

        if (!plan.CanProceed)
        {
            StatusLine = "计划有阻断问题，未安装。";
            ResultHeadline = "计划被阻断";
            ResultDetail = "查看日志里的 error 项，通常是缺依赖。";
            return;
        }

        if (mode == PipelineMode.Plan)
        {
            StatusLine = "计划已生成，未做任何更改。";
            ResultHeadline = "计划预览";
            ResultDetail = $"{plan.Steps.Count} 步，主程序 {plan.MainPackageName} {plan.MainVersion}";
            return;
        }

        Stage = "安装";
        ProgressIndeterminate = true;
        var installer = new AppxInstaller(
            _shell,
            _inventory,
            new AppxInstallerOptions(AllUsers: AllUsers, ForceShutdown: true, AllowRegisterFallback: true));

        var progress = new Progress<InstallStepResult>(result => OnInstallStep(result, plan));
        var outcome = await installer.InstallAsync(plan, progress, token).ConfigureAwait(true);

        if (outcome.Succeeded)
        {
            Stage = "完成";
            ProgressIndeterminate = false;
            Progress = 100;
            ResultHeadline = "安装完成";
            ResultDetail = outcome.Registered is null
                ? $"{plan.MainPackageName} 已安装。"
                : $"{outcome.Registered.PackageFullName} · 状态 {outcome.Registered.Status}";
            AddLog(LogLevel.Success, ResultDetail);
            StatusLine = "安装完成，可以从开始菜单启动。";
        }
        else
        {
            Stage = "失败";
            ProgressIndeterminate = false;
            var failure = outcome.StepResults.FirstOrDefault(result => !result.Succeeded);
            ResultHeadline = "安装失败";
            ResultDetail = failure?.Error is null
                ? failure?.Message ?? "未知错误。"
                : $"{failure.Error.FormattedCode} {failure.Error.Name} · {failure.Error.Meaning}";
            AddLog(LogLevel.Error, ResultDetail);
            if (failure?.Error is not null)
            {
                AddLog(LogLevel.Info, $"原因：{failure.Error.TypicalCause}");
                AddLog(LogLevel.Info, $"处理：{failure.Error.ManualSteps}");
            }

            StatusLine = "安装失败，日志里有错误码解释。";
        }
    }

    private void OnDownloadProgress(DownloadProgress progress)
    {
        void Apply()
        {
            Progress = Math.Round(progress.Fraction * 100, 1);
            StatusLine = $"正在下载 {progress.FileName} · {UiFormat.Size(progress.BytesReceived)} / {UiFormat.Size(progress.TotalBytes)}";
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
            AddLog(level, $"{(result.Succeeded ? "ok" : "failed")} · {result.Step.FileName}"
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

            Chips.Clear();
            Chips.Add(new EnvironmentChip($"{report.Architecture}", "neutral"));
            Chips.Add(new EnvironmentChip(
                report.ProductName is null ? "Windows" : $"{report.ProductName} {report.Build}",
                "neutral"));
            Chips.Add(new EnvironmentChip(
                report.SideloadingEnabled switch { true => "侧载已开启", false => "侧载已关闭", null => "侧载未知" },
                report.SideloadingEnabled switch { true => "ok", false => "bad", null => "warn" }));
            Chips.Add(new EnvironmentChip(
                report.DeveloperModeEnabled switch { true => "开发者模式开", false => "开发者模式关", null => "开发者模式未知" },
                report.DeveloperModeEnabled switch { true => "ok", false => "warn", null => "neutral" }));

            foreach (var warning in report.Warnings)
            {
                AddLog(LogLevel.Warning, warning);
            }

            StatusLine = report.Warnings.Count == 0
                ? "环境就绪：可以安装。"
                : $"环境有 {report.Warnings.Count} 条提示，见日志。";
        }
        catch (Exception ex)
        {
            AddLog(LogLevel.Warning, $"环境检查失败：{ex.Message}");
        }
    }

    private void Explain()
    {
        if (!AppxErrorCatalog.TryLookup(ExplainInput, out var info))
        {
            if (AppxErrorCatalog.TryParse(ExplainInput, out var code))
            {
                ExplainTitle = AppxErrorCatalog.Format(code);
                ExplainBody = "这个错误码还不在目录里。";
            }
            else
            {
                ExplainTitle = "无法解析";
                ExplainBody = "输入形如 0x80073CF3、80073CF3、2147958003 的错误码。";
            }

            ExplainVisible = true;
            return;
        }

        ExplainTitle = $"{info.FormattedCode} · {info.Name}";
        ExplainBody = $"是什么：{info.Meaning}\n\n为什么：{info.TypicalCause}\n\n怎么办：{info.ManualSteps}";
        ExplainVisible = true;
    }

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
            StatusLine = "日志已复制。";
        }
    }

    private void Paste()
    {
        if (Clipboard.ContainsText())
        {
            Reference = Clipboard.GetText().Trim();
        }
    }

    private void AddLog(LogLevel level, string text) =>
        Log.Add(new LogEntry(DateTimeOffset.Now, level, text));

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

    private static string ModeLabel(PipelineMode mode) => mode switch
    {
        PipelineMode.Resolve => "解析",
        PipelineMode.Download => "下载",
        PipelineMode.Plan => "计划",
        _ => "安装",
    };

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
public sealed record EnvironmentChip(string Text, string Tone);

internal enum PipelineMode
{
    Resolve,
    Download,
    Plan,
    Install,
}
