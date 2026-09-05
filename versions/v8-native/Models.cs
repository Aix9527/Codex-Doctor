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

public sealed record ProbeResult(bool Ok, string? Error = null, int? StatusCode = null, string? Protocol = null);

public sealed record ProxyEnvironmentState(bool Exists, string HttpProxy, string HttpsProxy);

public sealed record ConflictState(string Http, string Https, bool Conflict);

public sealed record TunState(bool ProcessDetected, bool AdapterDetected, string[] Processes, string[] Adapters);

public sealed record DiagnosisResult(
    string Version,
    HealthState Health,
    FailureClass FailureClass,
    string FailureNameZh,
    string RecommendationZh,
    ProbeResult Dns,
    ProbeResult DirectTls,
    ProbeResult Proxy,
    string ProxyUrl,
    ProxyEnvironmentState Env,
    ConflictState Git,
    ConflictState Npm,
    TunState Tun,
    int CodexProcesses);
