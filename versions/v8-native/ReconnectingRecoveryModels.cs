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

public sealed record ReconnectingRecoveryResult(
    [property: JsonIgnore] ReconnectingRecoveryStatus Status,
    [property: JsonPropertyName("修复前扫描ID")] Guid BeforeScanId,
    [property: JsonPropertyName("修复后扫描ID")] Guid AfterScanId,
    [property: JsonPropertyName("动作结果")] IReadOnlyList<RepairActionResult> Actions,
    [property: JsonPropertyName("摘要")] string SummaryZh,
    [property: JsonPropertyName("修复前网络诊断")] DiagnosisResult? DiagnosisBefore,
    [property: JsonPropertyName("修复后网络诊断")] DiagnosisResult? DiagnosisAfter)
{
    [JsonPropertyName("状态")]
    public string StatusName => Status.ToString();
}
