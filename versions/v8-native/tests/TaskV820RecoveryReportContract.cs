using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820RecoveryReportContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var beforeId = Guid.NewGuid();
        var afterId = Guid.NewGuid();
        var scan = new CodexHealthScanResult(
            afterId,
            DateTimeOffset.UtcNow,
            [],
            CodexDiscoveryResult.Empty(),
            null);
        var recovery = new ReconnectingRecoveryResult(
            ReconnectingRecoveryStatus.NetworkRecovered,
            "网络已恢复，Desktop 尚未验证。",
            beforeId,
            afterId,
            "http://user:sk-report-secret@127.0.0.1:7897",
            true,
            false,
            [new RepairActionResult("codex.proxy.env", "修复 Codex 代理", RepairActionStatus.Succeeded, "ok", null)]);

        var exporter = new HealthReportExporter(
            userProfile: Path.GetTempPath(),
            isAdministrator: () => true);
        var json = exporter.BuildJson(scan, reconnectingRecovery: recovery);

        Require(json.Contains("\"Reconnecting自愈\"", StringComparison.Ordinal), "完整报告必须包含 Reconnecting 自愈区段。");
        Require(json.Contains(beforeId.ToString(), StringComparison.OrdinalIgnoreCase), "报告必须记录修复前 ScanId。");
        Require(json.Contains(afterId.ToString(), StringComparison.OrdinalIgnoreCase), "报告必须记录修复后 ScanId。");
        Require(json.Contains("NETWORK_RECOVERED", StringComparison.Ordinal), "报告必须记录结构化自愈状态。");
        Require(json.Contains("\"网络已验证\": true", StringComparison.Ordinal), "报告必须记录网络验证事实。");
        Require(json.Contains("\"Desktop已验证\": false", StringComparison.Ordinal), "报告必须记录 Desktop 验证事实。");
        Require(!json.Contains("sk-report-secret", StringComparison.OrdinalIgnoreCase), "自愈报告中的代理地址仍必须经过敏感值脱敏。");
        Require(json.Contains("[已脱敏]", StringComparison.Ordinal), "敏感代理内容必须显示为已脱敏占位符。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
