namespace CodexDoctor.Native;

public sealed class CodexRepairEngine
{
    private readonly RepairActionCatalog _catalog;

    public CodexRepairEngine(RepairActionCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public RepairPlan BuildPlan(CodexHealthScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var actions = new List<IRepairAction>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in scan.Issues)
        {
            if (!issue.AutoRepairable || issue.Status != CodexIssueStatus.Repairable || string.IsNullOrWhiteSpace(issue.RepairActionId))
                continue;
            if (!_catalog.TryGet(issue.RepairActionId, out var action) || action is null)
                continue;
            if (action.ActionId.Equals("windows.user.proxy.write", StringComparison.OrdinalIgnoreCase))
                continue;
            if (seen.Add(action.ActionId)) actions.Add(action);
        }

        var manual = scan.Issues.Where(x => x.Status == CodexIssueStatus.ManualRequired).ToArray();
        var external = scan.Issues.Where(x => x.Status == CodexIssueStatus.ExternalRequired).ToArray();

        return new RepairPlan(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            scan.ScanId,
            actions,
            actions.Any(x => x.RequiresRestart),
            manual,
            external);
    }

    public async Task<RepairSessionResult> ExecuteAsync(RepairPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var results = new List<RepairActionResult>();

        foreach (var action in plan.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepairActionExecution? execution = null;
            try
            {
                execution = await action.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                if (action.BackupRequired && string.IsNullOrWhiteSpace(execution.BackupPath))
                    throw new InvalidOperationException("该修复动作要求备份，但未生成可审计备份路径。");

                bool verified;
                try
                {
                    verified = await action.VerifyAsync(execution, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    verified = false;
                }

                if (verified)
                {
                    results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.Succeeded, "执行并验证成功。", execution.BackupPath));
                    continue;
                }

                try
                {
                    await action.RollbackAsync(execution, cancellationToken).ConfigureAwait(false);
                    results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.RolledBack, "验证失败，已执行回滚。", execution.BackupPath));
                }
                catch
                {
                    results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.RollbackFailed, "验证失败且回滚失败，需要人工处理。", execution.BackupPath));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (execution is not null)
                {
                    try
                    {
                        await action.RollbackAsync(execution, CancellationToken.None).ConfigureAwait(false);
                        results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.RolledBack, $"执行异常后已回滚：{ex.GetType().Name}", execution.BackupPath));
                        continue;
                    }
                    catch
                    {
                        results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.RollbackFailed, $"执行异常且回滚失败：{ex.GetType().Name}", execution.BackupPath));
                        continue;
                    }
                }
                results.Add(new RepairActionResult(action.ActionId, action.TitleZh, RepairActionStatus.Failed, $"执行失败：{ex.GetType().Name}", null));
            }
        }

        return new RepairSessionResult(plan.PlanId, results);
    }
}
