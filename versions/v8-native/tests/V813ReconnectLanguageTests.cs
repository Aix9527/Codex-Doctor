using CodexDoctor.Native;
using System.Runtime.CompilerServices;

internal static class V813ReconnectLanguageTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        TestReconnectUsesValidatedProxyAndRestarts();
        TestReconnectClearsBrokenCodexProxyWhenDirectWorks();
        TestReconnectRefusesMutationWithoutHealthyRoute();
        TestUiAutomationRootFallback();
        TestLanguageReportsRootFailureAfterWindowBinding();
        TestUiUpgradeExposesReconnectButtonAndHandler();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("V8.1.3: " + message);
    }

    private static CodexDesktopInstallationInfo Desktop() =>
        new("ChatGPT Desktop", "test", @"C:\\Apps\\ChatGPT.exe", "1.0", true, [21028]);

    private static void TestReconnectUsesValidatedProxyAndRestarts()
    {
        var desktop = Desktop();
        var discovery = CodexDiscoveryResult.Empty() with { DesktopClients = [desktop] };
        var probe = new QueueReconnectProbe(
            new ReconnectHealthSnapshot(true, false, true, "http://127.0.0.1:7897", false, "", ""),
            new ReconnectHealthSnapshot(true, false, true, "http://127.0.0.1:7897", true, "http://127.0.0.1:7897", "http://127.0.0.1:7897"));
        var backend = new RecordingReconnectBackend();
        var service = new ReconnectRepairService(probe, backend);

        var result = service.RepairAsync(discovery).GetAwaiter().GetResult();

        Assert(result.Success && result.Verified, "可用代理场景必须修复后验证成功。");
        Assert(result.ProxyChanged, "缺失 Codex 专用代理时必须写入已验证代理。");
        Assert(backend.AppliedProxy == "http://127.0.0.1:7897", "只能写入诊断已验证的代理地址。");
        Assert(backend.RestartCount == 1, "修复代理后必须通过已确认 Desktop 重启一次。");
    }

    private static void TestReconnectClearsBrokenCodexProxyWhenDirectWorks()
    {
        var desktop = Desktop();
        var discovery = CodexDiscoveryResult.Empty() with { DesktopClients = [desktop] };
        var probe = new QueueReconnectProbe(
            new ReconnectHealthSnapshot(true, true, false, "", true, "http://127.0.0.1:1111", "http://127.0.0.1:1111"),
            new ReconnectHealthSnapshot(true, true, false, "", true, "", ""));
        var backend = new RecordingReconnectBackend();
        var service = new ReconnectRepairService(probe, backend);

        var result = service.RepairAsync(discovery).GetAwaiter().GetResult();

        Assert(result.Success && result.Verified, "直连健康但 Codex 代理失效时应清理专用代理并验证成功。");
        Assert(backend.ClearProxyCount == 1, "必须清理失效的 Codex 专用代理，而不是修改 Windows 用户代理。");
        Assert(backend.RestartCount == 1, "清理失效代理后必须重启 Desktop。");
    }

    private static void TestReconnectRefusesMutationWithoutHealthyRoute()
    {
        var discovery = CodexDiscoveryResult.Empty() with { DesktopClients = [Desktop()] };
        var probe = new QueueReconnectProbe(
            new ReconnectHealthSnapshot(true, false, false, "", false, "", ""));
        var backend = new RecordingReconnectBackend();
        var service = new ReconnectRepairService(probe, backend);

        var result = service.RepairAsync(discovery).GetAwaiter().GetResult();

        Assert(!result.Success, "直连和代理都不可用时不得伪报修复成功。");
        Assert(backend.RestartCount == 0 && backend.AppliedProxy is null && backend.ClearProxyCount == 0,
            "没有健康网络路径时不得写配置或无意义重启。");
    }

    private static void TestUiAutomationRootFallback()
    {
        var fallbackRoot = new object();
        var result = UiAutomationRootResolver.Resolve(
            () => throw new InvalidOperationException("ElementFromHandle failed"),
            () => fallbackRoot);

        Assert(result.Success && ReferenceEquals(result.Root, fallbackRoot),
            "ElementFromHandle 失败时必须允许 NativeWindowHandle 树搜索回退成功。");
        Assert(result.Method == UiAutomationRootBindingMethod.NativeWindowHandleTree,
            "回退成功必须记录真实绑定方式。");
    }

    private static void TestLanguageReportsRootFailureAfterWindowBinding()
    {
        var desktop = Desktop();
        var locator = new FixedWindowLocator(new DesktopWindowBindingResult(
            true, (nint)0x40A90, 21028, DesktopWindowBindingMethod.ExactScannedPid,
            "已通过扫描 PID 绑定。"));
        var roots = new FixedRootProvider(UiAutomationRootBinding.Failed("ElementFromHandle 与 NativeWindowHandle 树搜索均失败。"));
        var backend = new WindowsUiAutomationLanguageBackend(locator, roots);

        var result = backend.SetAsync(desktop, "en-US", CancellationToken.None).GetAwaiter().GetResult();

        Assert(!result.Applied, "根元素创建失败时不得报告语言已应用。");
        Assert(result.SummaryZh.Contains("窗口已绑定", StringComparison.OrdinalIgnoreCase),
            "窗口已经定位成功时诊断必须明确写出窗口已绑定。");
        Assert(result.SummaryZh.Contains("UI Automation 根元素", StringComparison.OrdinalIgnoreCase),
            "应准确报告 UI Automation 根元素创建失败。");
        Assert(!result.SummaryZh.Contains("没有找到与扫描结果安全匹配的 Desktop 主窗口", StringComparison.OrdinalIgnoreCase),
            "不得再把 UIA 根元素失败误报成没有找到 Desktop 主窗口。");
    }

    private static void TestUiUpgradeExposesReconnectButtonAndHandler()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj")))
            sourceRoot = sourceRoot.Parent;
        Assert(sourceRoot is not null, "无法定位 V8 源码目录。");

        var source = File.ReadAllText(Path.Combine(sourceRoot!.FullName, "V811UiUpgrade.cs"));
        Assert(source.Contains("\"修复重连\"", StringComparison.Ordinal), "主操作区必须出现“修复重连”按钮。");
        Assert(source.Contains("RepairReconnectAsync", StringComparison.Ordinal), "修复重连按钮必须绑定独立处理器。");
        Assert(source.Contains("ReconnectRepairService", StringComparison.Ordinal), "主界面必须调用经过验证的 ReconnectRepairService，而不是只做重启。");
    }

    private sealed class QueueReconnectProbe : IReconnectHealthProbe
    {
        private readonly Queue<ReconnectHealthSnapshot> _items;
        public QueueReconnectProbe(params ReconnectHealthSnapshot[] items) => _items = new Queue<ReconnectHealthSnapshot>(items);
        public Task<ReconnectHealthSnapshot> ProbeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_items.Count == 0) throw new InvalidOperationException("没有更多测试诊断快照。");
            return Task.FromResult(_items.Dequeue());
        }
    }

    private sealed class RecordingReconnectBackend : IReconnectRepairBackend
    {
        public string? AppliedProxy { get; private set; }
        public int ClearProxyCount { get; private set; }
        public int RestartCount { get; private set; }
        public string ApplyValidatedProxy(string proxyUrl) { AppliedProxy = proxyUrl; return "backup"; }
        public string ClearCodexProxy() { ClearProxyCount++; return "backup"; }
        public void RestartDesktop(CodexDesktopInstallationInfo desktop) => RestartCount++;
    }

    private sealed class FixedWindowLocator : IDesktopWindowLocator
    {
        private readonly DesktopWindowBindingResult _result;
        public FixedWindowLocator(DesktopWindowBindingResult result) => _result = result;
        public DesktopWindowBindingResult Locate(CodexDesktopInstallationInfo desktop) => _result;
    }

    private sealed class FixedRootProvider : IUiAutomationRootProvider
    {
        private readonly UiAutomationRootBinding _result;
        public FixedRootProvider(UiAutomationRootBinding result) => _result = result;
        public UiAutomationRootBinding Bind(nint handle) => _result;
    }
}
