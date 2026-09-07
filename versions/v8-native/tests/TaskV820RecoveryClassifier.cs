using System.Runtime.CompilerServices;
using CodexDoctor.Native;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820RecoveryClassifier
{
    [ModuleInitializer]
    internal static void Run()
    {
        var recovered = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("network-path-ok", CodexIssueSeverity.Ok)], desktopRunning: true),
            []);
        Require(recovered == ReconnectingRecoveryStatus.Recovered, "网络路径正常且 Desktop 正在运行时必须判定为 Recovered。");

        var networkOnly = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("network-path-ok", CodexIssueSeverity.Ok)], desktopRunning: false),
            []);
        Require(networkOnly == ReconnectingRecoveryStatus.NetworkRecovered, "网络已恢复但 Desktop 未运行时必须判定为 NetworkRecovered。");

        var restartFailed = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("network-path-ok", CodexIssueSeverity.Ok)], desktopRunning: false),
            [new RepairActionResult("codex.desktop.restart", "重启 Codex Desktop", RepairActionStatus.Failed, "失败", null)]);
        Require(restartFailed == ReconnectingRecoveryStatus.DesktopRestartFailed, "网络正常但 Desktop 重启动作失败时必须判定为 DesktopRestartFailed。");

        var dnsFailed = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("dns-fail", CodexIssueSeverity.Critical), Issue("network-path-fail", CodexIssueSeverity.Critical)], desktopRunning: true),
            []);
        Require(dnsFailed == ReconnectingRecoveryStatus.DnsFailed, "DNS 故障必须稳定分类为 DnsFailed。");

        var tlsFailed = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("dns-ok", CodexIssueSeverity.Ok), Issue("tls-direct-fail", CodexIssueSeverity.Critical), Issue("network-path-fail", CodexIssueSeverity.Critical)], desktopRunning: true),
            []);
        Require(tlsFailed == ReconnectingRecoveryStatus.TlsFailed, "DNS 正常但直连 TLS 失败且无可用网络路径时必须判定为 TlsFailed。");

        var proxyFailed = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("tls-direct-blocked", CodexIssueSeverity.Info), Issue("proxy-https-none", CodexIssueSeverity.Info), Issue("network-path-fail", CodexIssueSeverity.Critical)], desktopRunning: true),
            [new RepairActionResult("codex.proxy.env", "写入 Codex 代理", RepairActionStatus.RolledBack, "代理验证失败并回滚", "backup")]);
        Require(proxyFailed == ReconnectingRecoveryStatus.ProxyFailed, "需要代理且代理动作验证失败时必须判定为 ProxyFailed。");

        var manual = ReconnectingRecoveryClassifier.Classify(
            Scan([Issue("network-path-unknown", CodexIssueSeverity.Warning, CodexIssueStatus.ManualRequired)], desktopRunning: true),
            []);
        Require(manual == ReconnectingRecoveryStatus.ManualRequired, "无法确定网络路径时必须判定为 ManualRequired。");
    }

    private static CodexHealthScanResult Scan(IReadOnlyList<CodexIssue> issues, bool desktopRunning)
    {
        var discovery = CodexDiscoveryResult.Empty();
        if (desktopRunning)
        {
            discovery = discovery with
            {
                DesktopClients = [new CodexDesktopInstallationInfo("Codex", "test", @"C:\\Apps\\Codex.exe", "1.0", true, [1234])]
            };
        }
        return new CodexHealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, issues, discovery, null);
    }

    private static CodexIssue Issue(string id, CodexIssueSeverity severity, CodexIssueStatus status = CodexIssueStatus.Detected) =>
        new(id, "network", severity, id, id, id, id, status, false, null, false, false, false, false, null, id);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
