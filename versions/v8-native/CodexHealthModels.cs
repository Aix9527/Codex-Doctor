using System.Text.Json.Serialization;

namespace CodexDoctor.Native;

public enum CodexIssueSeverity
{
    Critical,
    Urgent,
    Warning,
    Info,
    Ok
}

public enum CodexIssueStatus
{
    Detected,
    Repairable,
    Repairing,
    Fixed,
    RepairFailed,
    ManualRequired,
    ExternalRequired,
    NotApplicable
}

public sealed record CodexIssue(
    [property: JsonPropertyName("问题ID")] string Id,
    [property: JsonPropertyName("分类")] string Category,
    [property: JsonIgnore] CodexIssueSeverity Severity,
    [property: JsonPropertyName("标题")] string TitleZh,
    [property: JsonPropertyName("摘要")] string SummaryZh,
    [property: JsonPropertyName("证据")] string EvidenceZh,
    [property: JsonPropertyName("影响")] string ImpactZh,
    [property: JsonIgnore] CodexIssueStatus Status,
    [property: JsonPropertyName("可自动修复")] bool AutoRepairable,
    [property: JsonPropertyName("修复动作ID")] string? RepairActionId,
    [property: JsonPropertyName("需要管理员权限")] bool RequiresAdmin,
    [property: JsonPropertyName("需要重启Codex")] bool RequiresCodexRestart,
    [property: JsonPropertyName("需要用户确认")] bool RequiresUserConfirmation,
    [property: JsonPropertyName("需要备份")] bool BackupRequired,
    [property: JsonPropertyName("验证ID")] string? VerificationId,
    [property: JsonPropertyName("来源检查ID")] string SourceCheckId)
{
    [JsonPropertyName("严重度")]
    public string SeverityZh => Severity switch
    {
        CodexIssueSeverity.Critical => "严重",
        CodexIssueSeverity.Urgent => "紧急",
        CodexIssueSeverity.Warning => "警告",
        CodexIssueSeverity.Info => "提示",
        _ => "正常"
    };

    [JsonPropertyName("状态")]
    public string StatusZh => Status switch
    {
        CodexIssueStatus.Detected => "已发现",
        CodexIssueStatus.Repairable => "可修复",
        CodexIssueStatus.Repairing => "修复中",
        CodexIssueStatus.Fixed => "已修复",
        CodexIssueStatus.RepairFailed => "修复失败",
        CodexIssueStatus.ManualRequired => "需要人工处理",
        CodexIssueStatus.ExternalRequired => "需要外部处理",
        _ => "不适用"
    };

    public static CodexIssue ForTest(string id, CodexIssueSeverity severity) =>
        new(
            id,
            "test",
            severity,
            id,
            id,
            "",
            "",
            CodexIssueStatus.Detected,
            false,
            null,
            false,
            false,
            false,
            false,
            null,
            id);
}

public sealed record CodexHealthScanResult(
    [property: JsonPropertyName("扫描ID")] Guid ScanId,
    [property: JsonPropertyName("扫描时间")] DateTimeOffset ScannedAt,
    [property: JsonPropertyName("问题")] IReadOnlyList<CodexIssue> Issues,
    [property: JsonPropertyName("本机发现")] CodexDiscoveryResult Discovery,
    [property: JsonPropertyName("网络诊断")] DiagnosisResult? Diagnosis)
{
    public static CodexHealthScanResult ForTest(IReadOnlyList<CodexIssue> issues) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, issues, CodexDiscoveryResult.Empty(), null);
}
