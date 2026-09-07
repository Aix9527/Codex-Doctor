using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820RecoveryDecisionContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var directHealthy = BuildDiagnosis(
            dnsOk: true,
            directTlsOk: true,
            proxyOk: false,
            proxyUrl: string.Empty,
            failureClass: FailureClass.Healthy);
        var directDecision = ReconnectingRecoveryDecisionEngine.Decide(directHealthy);
        Require(directDecision.NetworkPathVerified, "直连健康时必须直接判定存在可用网络路径。");
        Require(!directDecision.ShouldWriteCodexProxy, "直连健康时不得强制写 Codex 专用代理。");
        Require(string.IsNullOrWhiteSpace(directDecision.SelectedProxyUrl), "直连健康时不应选择代理 URL。");

        var proxyHealthy = BuildDiagnosis(
            dnsOk: true,
            directTlsOk: false,
            proxyOk: true,
            proxyUrl: "http://127.0.0.1:7897",
            failureClass: FailureClass.ProxyRequired);
        var proxyDecision = ReconnectingRecoveryDecisionEngine.Decide(proxyHealthy);
        Require(proxyDecision.NetworkPathVerified, "已验证代理 HTTPS 可用时必须视为存在可用网络路径。");
        Require(proxyDecision.ShouldWriteCodexProxy, "直连失败且已验证代理可用时应允许写入 Codex 专用代理。");
        Require(proxyDecision.SelectedProxyUrl == "http://127.0.0.1:7897", "只能选择 DiagnosisResult.ProxyUrl 中已验证的代理。");

        var unverifiedProxy = BuildDiagnosis(
            dnsOk: true,
            directTlsOk: false,
            proxyOk: false,
            proxyUrl: "http://127.0.0.1:7897",
            failureClass: FailureClass.TlsFailure);
        var unverifiedDecision = ReconnectingRecoveryDecisionEngine.Decide(unverifiedProxy);
        Require(!unverifiedDecision.ShouldWriteCodexProxy, "代理 HTTPS 未验证通过时不得写入 .codex/.env。");
        Require(string.IsNullOrWhiteSpace(unverifiedDecision.SelectedProxyUrl), "未验证代理不得进入恢复决策结果。");
        Require(unverifiedDecision.FailureStatus == ReconnectingRecoveryStatus.TlsFailed, "DNS 正常、TLS 失败且无可用代理时必须映射 TLS_FAILED。");

        var dnsFailure = BuildDiagnosis(
            dnsOk: false,
            directTlsOk: false,
            proxyOk: false,
            proxyUrl: string.Empty,
            failureClass: FailureClass.DnsFailure);
        var dnsDecision = ReconnectingRecoveryDecisionEngine.Decide(dnsFailure);
        Require(dnsDecision.FailureStatus == ReconnectingRecoveryStatus.DnsFailed, "DNS 失败且无其它路径时必须映射 DNS_FAILED。");

        var missing = ReconnectingRecoveryDecisionEngine.Decide(null);
        Require(missing.FailureStatus == ReconnectingRecoveryStatus.ManualRequired, "缺少诊断事实时必须返回 MANUAL_REQUIRED。");
    }

    private static DiagnosisResult BuildDiagnosis(
        bool dnsOk,
        bool directTlsOk,
        bool proxyOk,
        string proxyUrl,
        FailureClass failureClass)
        => new(
            "8.2.0-test",
            failureClass == FailureClass.Healthy ? HealthState.Healthy : HealthState.Warning,
            failureClass,
            failureClass.ToString(),
            "test",
            new ProbeResult(dnsOk),
            new ProbeResult(directTlsOk),
            new ProbeResult(proxyOk, StatusCode: proxyOk ? 403 : null),
            proxyUrl,
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
