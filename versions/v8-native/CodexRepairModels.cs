using System.Text.Json.Serialization;

namespace CodexDoctor.Native;

public enum RepairActionStatus
{
    Pending,
    Succeeded,
    RolledBack,
    Failed,
    RollbackFailed,
    Skipped
}

public sealed record RepairActionExecution(
    [property: JsonPropertyName("备份路径")] string? BackupPath,
    [property: JsonPropertyName("执行摘要")] string SummaryZh);

public interface IRepairAction
{
    string ActionId { get; }
    string TitleZh { get; }
    bool RequiresRestart { get; }
    bool BackupRequired { get; }
    Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken);
    Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken);
    Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken);
}

public sealed record RepairPlan(
    [property: JsonPropertyName("计划ID")] Guid PlanId,
    [property: JsonPropertyName("创建时间")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("来源扫描ID")] Guid SourceScanId,
    [property: JsonIgnore] IReadOnlyList<IRepairAction> Actions,
    [property: JsonPropertyName("需要重启Codex")] bool RequiresRestart,
    [property: JsonPropertyName("人工处理问题")] IReadOnlyList<CodexIssue> ManualIssues,
    [property: JsonPropertyName("外部处理问题")] IReadOnlyList<CodexIssue> ExternalIssues)
{
    [JsonPropertyName("修复动作")]
    public IReadOnlyList<string> ActionIds => Actions.Select(x => x.ActionId).ToArray();
}

public sealed record RepairActionResult(
    [property: JsonPropertyName("动作ID")] string ActionId,
    [property: JsonPropertyName("动作名称")] string TitleZh,
    [property: JsonIgnore] RepairActionStatus Status,
    [property: JsonPropertyName("摘要")] string SummaryZh,
    [property: JsonPropertyName("备份路径")] string? BackupPath)
{
    [JsonPropertyName("状态")]
    public string StatusZh => Status switch
    {
        RepairActionStatus.Succeeded => "成功",
        RepairActionStatus.RolledBack => "已回滚",
        RepairActionStatus.Failed => "失败",
        RepairActionStatus.RollbackFailed => "回滚失败",
        RepairActionStatus.Skipped => "已跳过",
        _ => "待执行"
    };
}

public sealed record RepairSessionResult(
    [property: JsonPropertyName("计划ID")] Guid PlanId,
    [property: JsonPropertyName("动作结果")] IReadOnlyList<RepairActionResult> Actions);

public sealed record RepairAndRescanResult(
    [property: JsonPropertyName("修复前")] CodexHealthScanResult BeforeScan,
    [property: JsonPropertyName("修复执行")] RepairSessionResult Repair,
    [property: JsonPropertyName("修复后")] CodexHealthScanResult AfterScan);
