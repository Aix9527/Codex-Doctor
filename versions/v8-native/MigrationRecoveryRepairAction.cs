using System.Text.Json;

namespace CodexDoctor.Native;

public sealed record MigrationRecoverySnapshot(
    string Source,
    string Target,
    string? BackupPath);

public interface IMigrationRecoveryBackend
{
    bool CanRecover();
    MigrationRecoverySnapshot Capture();
    void Recover(MigrationRecoverySnapshot snapshot);
    bool VerifyRecovered(MigrationRecoverySnapshot snapshot);
    void Restore(MigrationRecoverySnapshot snapshot);
}

public sealed class MigrationRecoveryRepairAction : IRepairAction
{
    private readonly IMigrationRecoveryBackend _backend;
    private readonly string _backupDirectory;

    public MigrationRecoveryRepairAction(IMigrationRecoveryBackend backend, string backupDirectory)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _backupDirectory = string.IsNullOrWhiteSpace(backupDirectory)
            ? throw new ArgumentException("迁移恢复备份目录不能为空。", nameof(backupDirectory))
            : backupDirectory;

        if (!_backend.CanRecover())
            throw new InvalidOperationException("无法证明当前迁移事务可安全恢复，已拒绝自动修复。请查看详情后人工处理。");
    }

    public string ActionId => "codex.migration.recover";
    public string TitleZh => "恢复 Codex 迁移事务";
    public bool RequiresRestart => false;
    public bool BackupRequired => true;

    public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_backend.CanRecover())
            throw new InvalidOperationException("迁移事务状态已变化，无法继续安全恢复。");

        var snapshot = _backend.Capture();
        ValidateSnapshot(snapshot);

        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(
            _backupDirectory,
            $"migration-recovery-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            backupPath,
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

        _backend.Recover(snapshot);
        return Task.FromResult(new RepairActionExecution(
            backupPath,
            "已按可审计迁移快照执行恢复事务，等待执行后验证。"));
    }

    public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = ReadSnapshot(execution);
        return Task.FromResult(_backend.VerifyRecovered(snapshot));
    }

    public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = ReadSnapshot(execution);
        _backend.Restore(snapshot);
        return Task.CompletedTask;
    }

    private static MigrationRecoverySnapshot ReadSnapshot(RepairActionExecution execution)
    {
        if (string.IsNullOrWhiteSpace(execution.BackupPath) || !File.Exists(execution.BackupPath))
            throw new InvalidOperationException("迁移恢复快照不存在，无法执行验证或回滚。");

        var snapshot = JsonSerializer.Deserialize<MigrationRecoverySnapshot>(File.ReadAllText(execution.BackupPath))
            ?? throw new InvalidOperationException("迁移恢复快照无效。");
        ValidateSnapshot(snapshot);
        return snapshot;
    }

    private static void ValidateSnapshot(MigrationRecoverySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Source) || string.IsNullOrWhiteSpace(snapshot.Target))
            throw new InvalidOperationException("迁移恢复快照缺少源目录或目标目录，拒绝执行。");
    }
}
