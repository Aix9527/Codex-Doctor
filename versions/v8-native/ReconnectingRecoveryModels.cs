using System.Text.Json.Serialization;

namespace CodexDoctor.Native;

public enum ReconnectingRecoveryStatus
{
    Recovered,
    NetworkRecovered,
    ProxyFailed,
    DnsFailed,
    TlsFailed,
    DesktopRestartFailed,
    ManualRequired
}

public sealed record ReconnectingRecoveryDecision(
    bool NetworkPathVerified,
    bool ShouldWriteCodexProxy,
    string? SelectedProxyUrl,
    ReconnectingRecoveryStatus? FailureStatus,
    string SummaryZh);

public sealed record ReconnectingRecoveryResult(
    [property: JsonIgnore] ReconnectingRecoveryStatus Status,
    [property: JsonPropertyName("摘要")] string SummaryZh,
    [property: JsonPropertyName("修复前扫描ID")] Guid BeforeScanId,
    [property: JsonPropertyName("修复后扫描ID")] Guid? AfterScanId,
    [property: JsonPropertyName("选中代理地址")] string? SelectedProxyUrl,
    [property: JsonPropertyName("网络已验证")] bool NetworkVerified,
    [property: JsonPropertyName("Desktop已验证")] bool DesktopVerified,
    [property: JsonPropertyName("修复动作")] IReadOnlyList<RepairActionResult> Actions)
{
    [JsonPropertyName("状态")]
    public string StatusZh => Status switch
    {
        ReconnectingRecoveryStatus.Recovered => "RECOVERED",
        ReconnectingRecoveryStatus.NetworkRecovered => "NETWORK_RECOVERED",
        ReconnectingRecoveryStatus.ProxyFailed => "PROXY_FAILED",
        ReconnectingRecoveryStatus.DnsFailed => "DNS_FAILED",
        ReconnectingRecoveryStatus.TlsFailed => "TLS_FAILED",
        ReconnectingRecoveryStatus.DesktopRestartFailed => "DESKTOP_RESTART_FAILED",
        _ => "MANUAL_REQUIRED"
    };
}

public static class ReconnectingRecoveryDecisionEngine
{
    public static ReconnectingRecoveryDecision Decide(DiagnosisResult? diagnosis)
    {
        if (diagnosis is null)
        {
            return new ReconnectingRecoveryDecision(
                false,
                false,
                null,
                ReconnectingRecoveryStatus.ManualRequired,
                "未获得可审计的网络诊断快照，不能执行自动自愈。");
        }

        if (diagnosis.DirectTls.Ok)
        {
            return new ReconnectingRecoveryDecision(
                true,
                false,
                null,
                null,
                "直连 TLS 已验证可用，不需要写入 Codex 专用代理。");
        }

        if (diagnosis.Proxy.Ok && !string.IsNullOrWhiteSpace(diagnosis.ProxyUrl))
        {
            var envMatches = diagnosis.Env.Exists &&
                string.Equals(diagnosis.Env.HttpProxy, diagnosis.ProxyUrl, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(diagnosis.Env.HttpsProxy, diagnosis.ProxyUrl, StringComparison.OrdinalIgnoreCase);

            return new ReconnectingRecoveryDecision(
                true,
                !envMatches,
                diagnosis.ProxyUrl,
                null,
                envMatches
                    ? "本地代理 HTTPS 已验证可用，Codex 专用代理配置已经匹配。"
                    : "本地代理 HTTPS 已验证可用，可安全写入 Codex 专用代理配置。");
        }

        if (!diagnosis.Dns.Ok || diagnosis.FailureClass == FailureClass.DnsFailure)
        {
            return new ReconnectingRecoveryDecision(
                false,
                false,
                null,
                ReconnectingRecoveryStatus.DnsFailed,
                "DNS 诊断失败，并且没有其它已验证网络路径。");
        }

        if (diagnosis.FailureClass == FailureClass.ProxyMisconfigured)
        {
            return new ReconnectingRecoveryDecision(
                false,
                false,
                null,
                ReconnectingRecoveryStatus.ProxyFailed,
                "检测到代理配置异常，但没有通过 HTTPS 验证的代理可供自动写入。");
        }

        return new ReconnectingRecoveryDecision(
            false,
            false,
            null,
            ReconnectingRecoveryStatus.TlsFailed,
            "DNS 正常，但直接 TLS 失败且没有可验证的代理网络路径。");
    }
}
