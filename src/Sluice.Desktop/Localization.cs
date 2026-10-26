using System.Globalization;
using Sluice.Desktop.ViewModels;

namespace Sluice.Desktop;

/// <summary>Languages the window speaks.</summary>
public enum UiLanguage
{
    Chinese,
    English,
}

/// <summary>
/// Two dictionaries and an indexer. The XAML binds @@@{Binding Loc[Key]}@@@ and everything refreshes
/// with one notification when the language flips, so no view model property has to know about it.
/// </summary>
public sealed class Localization : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, string> ChineseText = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // header
        ["RefreshEnvironment"] = "重新检查环境",
        ["LanguageLabel"] = "语言",
        // source rail
        ["Source"] = "来源",
        ["Paste"] = "粘贴",
        ["Arch"] = "架构",
        ["ArchThisMachine"] = "{0} · 本机",
        ["PrepareOtherArch"] = "为另一台机器准备",
        ["Locale"] = "资源语言",
        ["IncludeAllArch"] = "含全部架构",
        ["AllUsers"] = "为所有用户",
        ["InstallToMachine"] = "安装到本机",
        ["Resolve"] = "解析",
        ["Download"] = "下载",
        ["Plan"] = "计划",
        ["CancelTask"] = "取消当前任务",
        ["Cache"] = "存放位置",
        ["OpenFolder"] = "打开文件夹",
        ["Explain"] = "错误码速查",
        ["Query"] = "查询",
        // tabs and list chrome
        ["TabPackages"] = "包与计划",
        ["TabLog"] = "活动日志",
        ["CopyLog"] = "复制日志",
        ["PackageCount"] = "{0} 个包",
        ["ColumnRole"] = "角色",
        ["ColumnFile"] = "文件",
        ["ColumnArch"] = "架构",
        ["ColumnSize"] = "大小",
        ["ColumnLink"] = "链接",
        // plan
        ["PlanSection"] = "安装计划",
        ["PlanFormula"] = "框架 → 主程序 → 校验",
        ["PlanEmpty"] = "点「计划」或「安装到本机」后，这里列出依赖顺序与每一步结果。",
        // empty state
        ["EmptyTitle"] = "还没有解析任何链接",
        ["EmptyBody"] = "粘贴一个 Store 链接后点「解析」：会列出后端真实的下发包、架构、大小，以及每个包的签名下载地址是否可用。",
        ["EmptyHint"] = "也可以直接「安装到本机」：只下载清单里声明的依赖，框架先行，装完回读注册再报结果。",
        // roles and states
        ["RoleMain"] = "主程序",
        ["RoleFramework"] = "框架",
        ["RoleResource"] = "资源包",
        ["KindFramework"] = "框架",
        ["KindMain"] = "主程序",
        ["KindResource"] = "资源包",
        ["KindVerify"] = "校验",
        ["StatePending"] = "等待",
        ["StateRunning"] = "进行中",
        ["StateOk"] = "完成",
        ["StateFailed"] = "失败",
        ["StateSkipped"] = "未执行",
        ["LogNotAttempted"] = "还有 {0} 步未执行：上一步失败后流程就停了。",
        // stages and status
        ["StageIdle"] = "空闲",
        ["StageResolve"] = "解析",
        ["StageDownload"] = "下载",
        ["StagePlan"] = "规划",
        ["StageInstall"] = "安装",
        ["StageDone"] = "完成",
        ["StageFailed"] = "失败",
        ["StatusReady"] = "粘贴一个 Store 链接，或直接输入产品 ID。",
        ["StatusResolving"] = "正在解析 {0}…",
        ["StatusDownloading"] = "正在下载 {0} · {1} / {2}",
        ["StatusResolved"] = "解析完成。",
        ["StatusDownloaded"] = "下载完成。",
        ["StatusPlanned"] = "计划已生成，未做任何更改。",
        ["StatusPlanBlocked"] = "计划有阻断问题，未安装。",
        ["StatusInstalling"] = "正在安装第 {0}/{1} 步：{2}",
        ["StatusInstalled"] = "安装完成，可以从开始菜单启动。",
        ["StatusInstallFailed"] = "安装失败，日志里有错误码解释。",
        ["StatusCancelled"] = "已取消。",
        ["StatusCopied"] = "日志已复制。",
        ["StatusCopyFailed"] = "复制失败：{0}",
        ["StatusEnvironment"] = "环境就绪：可以安装。",
        ["StatusEnvironmentWarnings"] = "环境有 {0} 条提示，见日志。",
        // headlines and details
        ["ResultResolved"] = "解析完成",
        ["ResultDownloaded"] = "下载完成",
        ["ResultPlanned"] = "计划预览",
        ["ResultBlocked"] = "计划被阻断",
        ["ResultInstalled"] = "安装完成",
        ["ResultInstallFailed"] = "安装失败",
        ["ResultUnfinished"] = "未完成",
        ["DetailPackageCount"] = "{0} 个包",
        ["DetailFilesIn"] = "{0} 个包在 {1}",
        ["DetailPlanSteps"] = "{0} 步，主程序 {1} {2}",
        ["DetailBlocked"] = "查看日志里的 error 项，通常是缺依赖。",
        ["DetailRegistered"] = "{0} · 状态 {1}",
        ["DetailNothingParsed"] = "还没有解析任何产品",
        // runtime log
        ["LogUnrecognised"] = "无法识别的链接。",
        ["LogCancelled"] = "已取消。",
        ["LogParsed"] = "解析到 {0} 个包，合计 {1}。",
        ["LogDownloaded"] = "已下载 {0} 个包，{1}。",
        ["LogAlreadyInstalled"] = "{0} {1} 已安装，跳过 {2}。",
        ["LogNotDeclared"] = "{0} 不在应用清单里，不下载。",
        ["LogCarries"] = "{0} 内含适用于 {1} 的 {2}。",
        ["LogNeedsWindows"] = "安装需要 Windows。",
        // explain card
        ["ExplainWhat"] = "是什么",
        ["ExplainWhy"] = "为什么",
        ["ExplainHow"] = "怎么办",
        ["ExplainBody"] = "是什么：{0}\n\n为什么：{1}\n\n怎么办：{2}",
        ["ExplainUnparsableTitle"] = "无法解析",
        ["UnknownPublisher"] = "未知发布者",
        ["LinkNone"] = "无链接",
        ["UnknownError"] = "未知错误。",
        ["DetailInstalled"] = "{0} 已安装。",
        ["DetailInstalledUnverified"] = "{0} 应该装好了，但回读注册失败：{1}",
        ["ProductCategories"] = "{0} 个分类",
        ["DownloadedPackages"] = "已下载 {0} 个包，{1}。",
        ["RegisteredStatus"] = "{0} · 状态 {1}",
        ["ChipSideloadOn"] = "侧载已开启",
        ["ChipSideloadOff"] = "侧载已关闭",
        ["ChipSideloadUnknown"] = "侧载未知",
        ["ChipDevOn"] = "开发者模式开",
        ["ChipDevOff"] = "开发者模式关",
        ["ChipDevUnknown"] = "开发者模式未知",
        ["EnvCheckFailed"] = "环境检查失败：{0}",
        ["CrashDetails"] = "崩溃详情已写入：",
        // diagnostics the core layer emits
        ["Diag_EnvNotWindows"] = "当前不是 Windows：可以解析和下载，但无法安装。",
        ["Diag_EnvProbeFailed"] = "读取 Windows 环境失败：{0}",
        ["Diag_EnvSideloadingOff"] = "侧载已禁用：部署会以 0x80073CFF 失败。请打开开发者模式，或把 HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock\\AllowAllTrustedApps 设为 1。",
        ["Diag_EnvDevModeOff"] = "开发者模式已关闭：注册失败时的 -Register 兜底用不了。",
        ["Diag_EnvServiceDown"] = "{0} 服务当前是 {1}；部署需要它处于运行状态（Start-Service {0}）。",
        ["Diag_EnvLowDisk"] = "系统盘只剩 {0} GiB；大的安装包可能在预置阶段失败。",
        ["Diag_BundleNoApplication"] = "这个 bundle 不含应用程序包，可能只带资源。",
        ["Diag_BundleNoMatchingArchitecture"] = "这个 bundle 里没有适用于 {0} 的应用程序包。",
        ["Diag_BundleSelected"] = "已为 {1} 选中 {0}。",
        ["Diag_BundleKeptResources"] = "为 {1} 保留了 {0} 个资源包。",
        ["Diag_PrepareNoApplication"] = "bundle {0} 没有适用于 {1} 的应用程序包。",
        ["Diag_PrepareInnerPackage"] = "{0} 内含 {2} 用的 {1}。",
        ["Diag_PrepareUnreadableBundle"] = "读不出 {0} 内部的清单：{1}",
        ["Diag_PrepareNotDeclared"] = "{0} 没有被应用清单声明，不下载。",
        ["Diag_PrepareForeignResource"] = "{0} 属于别的包家族，不下载。",
        ["Diag_PlanSkippedFile"] = "跳过「{0}」：它不是可读的 appx/msix 包。",
        ["Diag_PlanSkippedFileReason"] = "跳过「{0}」：{1}",
        ["Diag_PlanNoMatchingArchitecture"] = "这个文件夹里没有给 {0} 编译的应用程序包；计划会用现有的那个，Windows 大概率会拒绝安装。",
        ["Diag_PlanBundleNoApplication"] = "bundle「{0}」没有适用于 {1} 的应用程序包。",
        ["Diag_PlanBundleNoApplicationHint"] = "重新下载这个产品；后端可能不提供这个架构。",
        ["Diag_PlanBundleManifestUnreadable"] = "读不出 bundle 内部的清单（{0}）；改用文件夹里的文件。",
        ["Diag_PlanDependencyInstalled"] = "{0} {1} 已安装，不需要再装包。",
        ["Diag_PlanDependencySkipped"] = "{0} {1} 已安装，跳过 {2}。",
        ["Diag_PlanMissingDependency"] = "{1} 声明的依赖「{0}」缺失。",
        ["Diag_PlanMissingDependencyHint"] = "重新下载时不要带 --only/--deps-only，让框架包一起下来；或直接从 Microsoft Store 装这个框架。",
        ["Diag_PlanUnusedFile"] = "{0} 不是这个应用需要的，不会安装。",
        ["Diag_PlanMainAlreadyInstalled"] = "{0} {1} 已经装过；要重装加 --force。",
        ["Diag_PlanStepFramework"] = "{0} 需要：{1}",
        ["Diag_PlanStepMain"] = "应用程序包",
        ["Diag_PlanStepMainBundle"] = "应用 bundle；架构和资源由 Windows 自行选择",
        ["Diag_PlanStepResource"] = "{0} 的资源包",
        ["Diag_PlanStepVerify"] = "回读注册，确认真的装上了",
        ["Diag_PlanInventoryUnreadable"] = "读取已安装清单失败（{0}）；文件夹里的依赖都会被安装。",
        ["Diag_PlanInventoryCheckFailed"] = "无法确认 {0} 是否已安装（{1}）。",
        ["LogLevelInfo"] = "信息",
        ["LogLevelSuccess"] = "完成",
        ["LogLevelWarning"] = "提示",
        ["LogLevelError"] = "错误",
        ["LogLevelCommand"] = "命令",
        ["ExplainUnknownCode"] = "这个错误码还不在目录里。",
        ["ExplainUnparsable"] = "输入形如 0x80073CF3、80073CF3、2147958003 的错误码。",
    };

    private static readonly IReadOnlyDictionary<string, string> EnglishText = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RefreshEnvironment"] = "Re-check this machine",
        ["LanguageLabel"] = "Language",
        ["Source"] = "Source",
        ["Paste"] = "Paste",
        ["Arch"] = "Architecture",
        ["ArchThisMachine"] = "{0} · this machine",
        ["PrepareOtherArch"] = "For another machine",
        ["Locale"] = "Resource language",
        ["IncludeAllArch"] = "All architectures",
        ["AllUsers"] = "All users",
        ["InstallToMachine"] = "Install on this machine",
        ["Resolve"] = "Resolve",
        ["Download"] = "Download",
        ["Plan"] = "Plan",
        ["CancelTask"] = "Cancel the running task",
        ["Cache"] = "Packages folder",
        ["OpenFolder"] = "Open folder",
        ["Explain"] = "Look up an error code",
        ["Query"] = "Look up",
        ["TabPackages"] = "Packages and plan",
        ["TabLog"] = "Activity log",
        ["CopyLog"] = "Copy log",
        ["PackageCount"] = "{0} packages",
        ["ColumnRole"] = "Role",
        ["ColumnFile"] = "File",
        ["ColumnArch"] = "Arch",
        ["ColumnSize"] = "Size",
        ["ColumnLink"] = "Link",
        ["PlanSection"] = "Install plan",
        ["PlanFormula"] = "frameworks → app → verify",
        ["PlanEmpty"] = "Press Plan or Install to see the dependency order and the result of every step here.",
        ["EmptyTitle"] = "Nothing resolved yet",
        ["EmptyBody"] = "Paste a Store link and press Resolve: the packages the delivery service actually offers are listed with architecture, size and whether a signed download URL was issued.",
        ["EmptyHint"] = "Or press Install: only the dependencies the manifest declares are downloaded, frameworks first, then the registration is read back.",
        ["RoleMain"] = "app",
        ["RoleFramework"] = "framework",
        ["RoleResource"] = "resource",
        ["KindFramework"] = "framework",
        ["KindMain"] = "app",
        ["KindResource"] = "resource",
        ["KindVerify"] = "verify",
        ["StatePending"] = "waiting",
        ["StateRunning"] = "running",
        ["StateOk"] = "done",
        ["StateFailed"] = "failed",
        ["StateSkipped"] = "not run",
        ["LogNotAttempted"] = "{0} step(s) were not attempted: the run stopped at the previous failure.",
        ["StageIdle"] = "idle",
        ["StageResolve"] = "resolve",
        ["StageDownload"] = "download",
        ["StagePlan"] = "plan",
        ["StageInstall"] = "install",
        ["StageDone"] = "done",
        ["StageFailed"] = "failed",
        ["StatusReady"] = "Paste a Store link, or type a product id.",
        ["StatusResolving"] = "Resolving {0}…",
        ["StatusDownloading"] = "Downloading {0} · {1} / {2}",
        ["StatusResolved"] = "Resolved.",
        ["StatusDownloaded"] = "Downloaded.",
        ["StatusPlanned"] = "Plan ready, nothing was changed.",
        ["StatusPlanBlocked"] = "The plan is blocked, nothing was installed.",
        ["StatusInstalling"] = "Installing step {0}/{1}: {2}",
        ["StatusInstalled"] = "Installed. Launch it from the Start menu.",
        ["StatusInstallFailed"] = "The install failed, the log explains the error code.",
        ["StatusCancelled"] = "Cancelled.",
        ["StatusCopied"] = "Log copied.",
        ["StatusCopyFailed"] = "Could not copy to the clipboard: {0}",
        ["StatusEnvironment"] = "Ready: this machine can install packages.",
        ["StatusEnvironmentWarnings"] = "{0} environment warnings, see the log.",
        ["ResultResolved"] = "Resolved",
        ["ResultDownloaded"] = "Downloaded",
        ["ResultPlanned"] = "Plan preview",
        ["ResultBlocked"] = "Plan blocked",
        ["ResultInstalled"] = "Installed",
        ["ResultInstallFailed"] = "Install failed",
        ["ResultUnfinished"] = "Not finished",
        ["DetailPackageCount"] = "{0} packages",
        ["DetailFilesIn"] = "{0} packages in {1}",
        ["DetailPlanSteps"] = "{0} steps, app {1} {2}",
        ["DetailBlocked"] = "Look at the error lines in the log; usually a missing dependency.",
        ["DetailRegistered"] = "{0} · status {1}",
        ["DetailNothingParsed"] = "Nothing resolved yet",
        ["LogUnrecognised"] = "That is not a Store link.",
        ["LogCancelled"] = "Cancelled.",
        ["LogParsed"] = "Resolved {0} packages, {1} in total.",
        ["LogDownloaded"] = "Downloaded {0} packages, {1}.",
        ["LogAlreadyInstalled"] = "{0} {1} is already installed, skipping {2}.",
        ["LogNotDeclared"] = "{0} is not declared by the app, not downloading it.",
        ["LogCarries"] = "{0} carries {2} for {1}.",
        ["LogNeedsWindows"] = "Installing needs Windows.",
        ["ExplainWhat"] = "what",
        ["ExplainWhy"] = "why",
        ["ExplainHow"] = "fix",
        ["ExplainBody"] = "what: {0}\n\nwhy: {1}\n\nfix: {2}",
        ["ExplainUnparsableTitle"] = "Not a code",
        ["UnknownPublisher"] = "unknown publisher",
        ["LinkNone"] = "no url",
        ["UnknownError"] = "Unknown error.",
        ["DetailInstalled"] = "{0} is installed.",
        ["DetailInstalledUnverified"] = "{0} looks installed, but the registration could not be read back: {1}",
        ["ProductCategories"] = "{0} categories",
        ["DownloadedPackages"] = "Downloaded {0} packages, {1}.",
        ["RegisteredStatus"] = "{0} · status {1}",
        ["ChipSideloadOn"] = "sideloading on",
        ["ChipSideloadOff"] = "sideloading off",
        ["ChipSideloadUnknown"] = "sideloading unknown",
        ["ChipDevOn"] = "developer mode on",
        ["ChipDevOff"] = "developer mode off",
        ["ChipDevUnknown"] = "developer mode unknown",
        ["EnvCheckFailed"] = "Environment check failed: {0}",
        ["CrashDetails"] = "Crash details were written to ",
        ["LogLevelInfo"] = "info",
        ["LogLevelSuccess"] = "done",
        ["LogLevelWarning"] = "warn",
        ["LogLevelError"] = "error",
        ["LogLevelCommand"] = "cmd",
        ["ExplainUnknownCode"] = "That code is not in the catalogue yet.",
        ["ExplainUnparsable"] = "Enter a code such as 0x80073CF3, 80073CF3 or 2147958003.",
    };

    private UiLanguage _language;

    public Localization(UiLanguage language = UiLanguage.Chinese) => _language = language;

    public UiLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            Raise(nameof(Language));
            Raise(nameof(IsChinese));
            Raise(nameof(IsEnglish));
            Raise("Item[]");
        }
    }

    public bool IsChinese
    {
        get => Language == UiLanguage.Chinese;
        set { if (value) { Language = UiLanguage.Chinese; } }
    }

    public bool IsEnglish
    {
        get => Language == UiLanguage.English;
        set { if (value) { Language = UiLanguage.English; } }
    }

    /// <summary>Text for a key in the current language; the key itself when it is missing.</summary>
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var dictionary = Language == UiLanguage.English ? EnglishText : ChineseText;
        return dictionary.TryGetValue(key, out var text) ? text : key;
    }

    /// <summary>True when a key exists in the current language.</summary>
    /// <param name="key">Message key.</param>
    public bool Has(string key) => (Language == UiLanguage.English ? EnglishText : ChineseText).ContainsKey(key);

    /// <summary>
    /// Renders a diagnostic the core layer produced. Codes that have a Diag_ key come out in the
    /// chosen language; anything else keeps the text the core already formatted.
    /// </summary>
    /// <param name="message">Diagnostic from the core layer.</param>
    public string Text(Diagnostics.DiagnosticMessage message)
    {
        var key = "Diag_" + message.Code;
        return Has(key)
            ? string.Format(CultureInfo.CurrentCulture, Get(key), message.Arguments.ToArray())
            : message.Text;
    }

    /// <summary>Formatted text for a key.</summary>
    public string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
