using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820ReportContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var beforeId = Guid.NewGuid();
        var afterId = Guid.NewGuid();
        var profile = @"C:\Users\ReportUser";
        var recovery = new ReconnectingRecoveryResult(
            ReconnectingRecoveryStatus.NetworkRecovered,
            beforeId,
            afterId,
            [new RepairActionResult(
                "codex.proxy.env",
                "修复 Codex 专用代理配置",
                RepairActionStatus.RolledBack,
                "OPENAI_API_KEY=sk-report-secret 验证失败并回滚",
                profile + @"\.codex\.env.backup")],
            "NETWORK_RECOVERED：网络已恢复，Desktop 仍需处理。",
            null,
            null);

        ReconnectingRecoveryContext.Clear();
        ReconnectingRecoveryContext.Record(recovery);
        var exporter = new HealthReportExporter(profile, () => true);
        var json = exporter.BuildJson(CodexHealthScanResult.ForTest([]));

        Require(json.Contains("\"版本\": \"8.2.0\"", StringComparison.Ordinal), "V8.2 完整报告必须标记版本 8.2.0。");
        Require(json.Contains("\"Reconnecting专项恢复\"", StringComparison.Ordinal), "完整报告必须包含 Reconnecting 专项恢复字段。");
        Require(json.Contains("NetworkRecovered", StringComparison.Ordinal), "完整报告必须包含专项恢复终态。");
        Require(json.Contains(beforeId.ToString(), StringComparison.OrdinalIgnoreCase), "完整报告必须包含修复前 ScanId。");
        Require(json.Contains(afterId.ToString(), StringComparison.OrdinalIgnoreCase), "完整报告必须包含修复后 ScanId。");
        Require(json.Contains("codex.proxy.env", StringComparison.Ordinal), "完整报告必须包含专项动作 ID。");
        Require(json.Contains("已回滚", StringComparison.Ordinal), "完整报告必须包含动作验证/回滚状态。");
        Require(!json.Contains("sk-report-secret", StringComparison.OrdinalIgnoreCase), "专项恢复审计进入报告后仍必须执行密钥脱敏。");
        Require(!json.Contains(profile, StringComparison.OrdinalIgnoreCase), "专项恢复备份路径进入报告后仍必须隐藏用户目录。");
        Require(json.Contains("%USERPROFILE%", StringComparison.OrdinalIgnoreCase), "用户目录必须替换为 %USERPROFILE%。");

        ReconnectingRecoveryContext.Clear();
        var withoutRecovery = exporter.BuildJson(CodexHealthScanResult.ForTest([]));
        Require(withoutRecovery.Contains("\"Reconnecting专项恢复\": null", StringComparison.Ordinal), "没有专项恢复历史时报告必须明确输出 null，而不是伪造恢复结果。");
    }
}