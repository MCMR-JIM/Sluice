namespace Sluice.Desktop;

/// <summary>
/// Chinese rendering of the deployment errors the core catalogue documents in English. The core keeps
/// the knowledge base language neutral; the window adds a translation for the codes it can explain.
/// </summary>
internal static class ErrorExplainText
{
    private static readonly Dictionary<uint, (string Meaning, string Cause, string Steps)> Chinese = new()
    {
        [0x80073CF3] = (
            "这个包和已安装的包冲突，装不上。",
            "基本都是依赖问题：应用需要的框架（VCLibs、WindowsAppRuntime、.NET Native）缺失、版本低于清单要求，或者装的是别的架构。",
            "先装对应的框架包（sluice 用 --deps-only 会自动下载），或者用 Get-AppxPackage -Name <名称> | Remove-AppxPackage 去掉冲突版本。"),
        [0x80073CF6] = (
            "包已经预置，但没能为当前用户注册。",
            "常见于手工解开的 bundle、做到一半的部署，或清单和已预置的文件对不上。",
            "打开开发者模式再装一次即可：这次失败之后 sluice 会自己解包并注册清单（不想用这个兜底就加 --no-register）；旧注册还留着的话，先 Get-AppxPackage <名称> | Remove-AppxPackage 清掉再装。"),
        [0x80073CF9] = (
            "AppX 部署服务没能完成这次操作。",
            "AppXSvc 或 StateRepository 被禁用、卡死，或者部署缓存损坏。",
            "确认 AppXSvc、ClipSVC、StateRepository 在运行（Get-Service AppXSvc,ClipSVC,StateRepository），必要时重启；状态损坏时运行 wsreset.exe 或 DISM /Online /Cleanup-Image /RestoreHealth。"),
        [0x80073D02] = (
            "需要被替换的包正在使用中。",
            "应用本身，或正在更新的框架包，还在运行。",
            "关掉应用和占用它的进程，再装一次。"),
        [0x80073CFB] = (
            "这个版本（或更新的版本）已经装过了。",
            "Store 下发的版本和本机已有的一致，常见于升级中途断掉之后。",
            "不用管；或者先卸载已有的包再装。"),
        [0x80073CFF] = (
            "部署策略拦下了这个包的安装。",
            "侧载被关（“从任意来源安装应用”），或开发者模式没开，而这个包对本机来说不是 Store 包。",
            "设置 > 系统 > 开发者选项里打开开发者模式；或把 HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock\\AllowAllTrustedApps 设为 1。"),
        [0x800B0109] = (
            "包的签名链指向本机不信任的根证书。",
            "包被重新签过名，或 Microsoft 根证书不在受信任存储里。",
            "把签名用的证书装进“受信任的根证书颁发机构”，或改用微软原签名的包（sluice 下载的一直是原件）。"),
        [0x800B0100] = (
            "包没有签名，Windows 拒绝部署。",
            "未签名或重新打包的文件，包括没有有效签名的文件夹注册。",
            "用 Store 的原签名包；手工解开的散文件必须先打开开发者模式，Windows 才肯注册。"),
        [0x800B010A] = (
            "包签名的证书链无效。",
            "签名是给别的包做的，或者下载损坏。",
            "核对下载文件的摘要（sluice 会自动校验），然后重新下载。"),
        [0x80070005] = (
            "部署时被拒绝访问。",
            "包需要为所有用户安装，或它需要的文件被另一个账户占用。",
            "用管理员权限运行；或者只为当前用户安装（不带 -AllUsers）。"),
        [0x80073CF0] = (
            "包打不开。",
            "文件被截断、还在下载中，或者根本不是 appx/msix 容器。",
            "对照解析结果核对大小和摘要，重新下载。"),
        [0x80073CF1] = (
            "在给定路径找不到包文件。",
            "下载和安装之间路径变了，或相对路径解析到了别处。",
            "用和下载时相同的 --dir 重新安装。"),
        [0x80073CFD] = (
            "这个包不适用于本设备。",
            "架构不对、设备家族不对（例如 Xbox 或 Holographic 包），或者包要求更新的 Windows。",
            "装本机架构的那一版（sluice 会自动选），或升级 Windows。"),
        [0x80070490] = (
            "部署找不到它期望的组件。",
            "StateRepository 数据库损坏，或旧注册指向已经不存在的文件。",
            "先 DISM /Online /Cleanup-Image /RestoreHealth，再重新注册或重装受影响的包。"),
        [0x8007000B] = (
            "包的载荷镜像无效。",
            "包的架构和本机不匹配，或者下载损坏。",
            "装本机实际运行的架构，并让 sluice 校验摘要。"),
    };

    /// <summary>Looks up the Chinese rendering of a catalogued error.</summary>
    /// <param name="code">HRESULT of the failure.</param>
    /// <param name="meaning">What Windows is complaining about.</param>
    public static bool TryGet(uint code, out (string Meaning, string Cause, string Steps) meaning) =>
        Chinese.TryGetValue(code, out meaning);
}
