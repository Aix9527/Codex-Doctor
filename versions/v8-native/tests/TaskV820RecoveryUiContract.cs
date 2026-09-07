using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820RecoveryUiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var emptyState = MainDashboardState.From(null);
        Require(!emptyState.CanRecoverReconnecting, "未完成扫描时 Reconnecting 自愈必须禁用。");

        var healthyScan = new CodexHealthScanResult(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [],
            Desktop(running: true),
            Diagnosis());
        var readyState = MainDashboardState.From(healthyScan);
        Require(readyState.CanRecoverReconnecting, "完成扫描且存在可信 Desktop、无阻断安全门时应允许 Reconnecting 自愈。");

        var blockedIssue = new CodexIssue(
            "manual-critical",
            "test",
            CodexIssueSeverity.Critical,
            "需要人工处理",
            "test",
            "test",
            "test",
            CodexIssueStatus.ManualRequired,
            false,
            null,
            true,
            false,
            true,
            false,
            null,
            "test");
        var blockedScan = healthyScan with { Issues = [blockedIssue] };
        Require(!MainDashboardState.From(blockedScan).CanRecoverReconnecting, "存在 Critical/ManualRequired 安全门时必须禁用专项自愈。");

        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");

        var upgrade = File.ReadAllText(Path.Combine(sourceRoot.FullName, "V811UiUpgrade.cs"));
        var mainForm = File.ReadAllText(Path.Combine(sourceRoot.FullName, "MainForm.cs"));
        Require(upgrade.Contains("Reconnecting 自愈", StringComparison.Ordinal), "主操作区必须新增 Reconnecting 自愈按钮。");
        Require(upgrade.Contains("RunReconnectingRecoveryAsync", StringComparison.Ordinal), "Reconnecting 自愈按钮必须调用主窗体专项自愈入口。");
        Require(mainForm.Contains("RunReconnectingRecoveryAsync", StringComparison.Ordinal), "MainForm 必须提供专项自愈 async handler。");
        Require(mainForm.Contains("_lastRecovery", StringComparison.Ordinal), "MainForm 必须保留最近一次自愈结果用于报告证据链。");
        Require(mainForm.Contains("_lastRecovery)", StringComparison.Ordinal), "导出完整报告必须携带最近一次 Reconnecting 自愈结果。");
    }

    private static CodexDiscoveryResult Desktop(bool running) => new(
        [new CodexDesktopInstallationInfo("Codex Desktop", "test", @"C:\Apps\Codex.exe", "1.0", running, running ? [1234] : [])],
        new CodexCliInfo(false, null, false, null, false),
        new CodexDataDirectoryInfo(@"C:\Users\X\.codex", true, false, null, 0, 0),
        [],
        new CodexLanguageState("未知", "未知", "未知", false, true, "test"));

    private static DiagnosisResult Diagnosis() => new(
        "8.2.0-test",
        HealthState.Healthy,
        FailureClass.Healthy,
        "健康",
        "test",
        new ProbeResult(true),
        new ProbeResult(true),
        new ProbeResult(false),
        string.Empty,
        new ProxyEnvironmentState(false, string.Empty, string.Empty),
        new ConflictState(string.Empty, string.Empty, false),
        new ConflictState(string.Empty, string.Empty, false),
        new TunState(false, false, [], []),
        1);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
