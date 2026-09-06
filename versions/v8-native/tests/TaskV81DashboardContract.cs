using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81DashboardContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var before = MainDashboardState.From(null);
        Require(before.ShowScanPrimary, "扫描前必须突出显示“一键扫描 Codex”。");
        Require(!before.CanStart, "扫描前不得启用启动 Codex。");
        Require(!before.CanRestart, "扫描前不得启用重启 Codex。");
        Require(!before.CanRepair, "扫描前不得启用一键修复。");
        Require(!before.CanExportReport, "扫描前不得启用导出完整报告。");
        Require(before.RepairableCount == 0, "扫描前可修复数量必须为 0。");

        var repairable = new CodexIssue(
            "proxy", "network", CodexIssueSeverity.Warning,
            "代理配置需要修复", "代理配置需要修复", "test", "test",
            CodexIssueStatus.Repairable, true, "codex.proxy.env",
            true, true, true, true, "verify.proxy", "proxy");
        var desktop = new CodexDesktopInstallationInfo(
            "Codex Desktop", "test", @"C:\Apps\Codex.exe", "1.0", true, [42]);
        var discovery = CodexDiscoveryResult.Empty() with { DesktopClients = [desktop] };
        var scan = new CodexHealthScanResult(
            Guid.NewGuid(), DateTimeOffset.UtcNow, [repairable], discovery, null);
        var after = MainDashboardState.From(scan);
        Require(!after.ShowScanPrimary, "扫描完成后不应继续停留在仅扫描状态。");
        Require(after.CanStart && after.CanRestart, "发现 Desktop 后必须允许启动/重启。");
        Require(after.CanRepair && after.RepairableCount == 1, "扫描后必须根据 Repairable 问题启用一键修复。");
        Require(after.CanExportReport, "有扫描结果后必须允许导出完整报告。");
        Require(after.WarningCount == 1, "首页必须保留严重度摘要计数。");

        var source = File.ReadAllText(Path.Combine(SourceRoot().FullName, "MainForm.cs"));
        foreach (var token in new[]
        {
            "一键扫描 Codex",
            "启动 Codex",
            "重启 Codex",
            "一键修复",
            "智能迁移/恢复",
            "一键中文",
            "导出完整报告",
            "软件作者：Aix",
            "QQ：976936105",
            "抖音：xch03209527"
        })
            Require(source.Contains(token, StringComparison.Ordinal), $"V8.1 首页缺少：{token}");
    }
}
