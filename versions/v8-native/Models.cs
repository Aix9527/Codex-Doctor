using System.Text.Json.Serialization;

namespace CodexDoctor.Native;

public enum FailureClass
{
    Healthy,
    ProxyRequired,
    ProxyMisconfigured,
    DirectNetworkBlocked,
    DnsFailure,
    TlsFailure,
    EnvironmentConflict
}

public enum HealthState
{
    Healthy,
    Warning,
    Error
}

public sealed record ProbeResult(
    [property: JsonPropertyName("正常")] bool Ok,
    [property: JsonPropertyName("错误信息")] string? Error = null,
    [property: JsonPropertyName("HTTP状态码")] int? StatusCode = null,
    [property: JsonPropertyName("TLS协议")] string? Protocol = null);

public sealed record ProxyEnvironmentState(
    [property: JsonPropertyName("文件存在")] bool Exists,
    [property: JsonPropertyName("HTTP代理")] string HttpProxy,
    [property: JsonPropertyName("HTTPS代理")] string HttpsProxy);

public sealed record ConflictState(
    [property: JsonPropertyName("HTTP代理")] string Http,
    [property: JsonPropertyName("HTTPS代理")] string Https,
    [property: JsonPropertyName("存在冲突")] bool Conflict);

public sealed record TunState(
    [property: JsonPropertyName("检测到相关进程")] bool ProcessDetected,
    [property: JsonPropertyName("检测到TUN网卡")] bool AdapterDetected,
    [property: JsonPropertyName("相关进程")] string[] Processes,
    [property: JsonPropertyName("相关网卡")] string[] Adapters);

public sealed record DiagnosisResult(
    [property: JsonPropertyName("版本")] string Version,
    [property: JsonIgnore] HealthState Health,
    [property: JsonIgnore] FailureClass FailureClass,
    [property: JsonPropertyName("故障分类")] string FailureNameZh,
    [property: JsonPropertyName("诊断建议")] string RecommendationZh,
    [property: JsonPropertyName("DNS诊断")] ProbeResult Dns,
    [property: JsonPropertyName("直连TLS诊断")] ProbeResult DirectTls,
    [property: JsonPropertyName("代理HTTPS诊断")] ProbeResult Proxy,
    [property: JsonPropertyName("代理地址")] string ProxyUrl,
    [property: JsonPropertyName("Codex专用环境文件")] ProxyEnvironmentState Env,
    [property: JsonPropertyName("Git代理")] ConflictState Git,
    [property: JsonPropertyName("npm代理")] ConflictState Npm,
    [property: JsonPropertyName("TUN状态")] TunState Tun,
    [property: JsonPropertyName("Codex进程数量")] int CodexProcesses)
{
    [JsonPropertyName("健康状态")]
    public string HealthNameZh => Health switch
    {
        HealthState.Healthy => "健康",
        HealthState.Warning => "需要检查",
        _ => "连接故障"
    };
}
