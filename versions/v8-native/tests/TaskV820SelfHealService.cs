using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820SelfHealService
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        await ProvesEndToEndSequenceAndFreshRescan();
        await ProvesVerifyFailureUsesExistingRollbackPath();
        await ProvesCancellationStopsLaterMutation();
    }

    private static async Task ProvesEndToEndSequenceAndFreshRescan()
    {
        var before = Scan(
            directOk: false,
            proxyOk: true,
            desktopRunning: true,
            issues: [Repairable("proxy-env", "codex.proxy.env", requiresRestart: true)]);
        var after = Scan(
            directOk: false,
            proxyOk: true,
            desktopRunning: true,
            issues: [OkIssue("network-path-ok", "network")]);

        var scanner = new QueueScanner([before, after]);
        var executor = new RecordingExecutor([
            new RepairActionResult("codex.proxy.env", "修复 Codex 专用代理配置", RepairActionStatus.Succeeded, "ok", "backup.env"),
            new RepairActionResult("codex.desktop.restart", "重启 Codex Desktop", RepairActionStatus.Succeeded, "ok", null)
        ]);
        var stages = new List<ReconnectingSelfHealStage>();
        var service = new ReconnectingSelfHealService(scanner, executor);

        var result = await service.RecoverAsync(new InlineProgress(p => stages.Add(p.Stage)), CancellationToken.None);

        Require(scanner.Calls == 2, "专项自愈必须执行修复前扫描和独立的修复后新扫描。");
        Require(before.ScanId != after.ScanId && result.BeforeScanId == before.ScanId && result.AfterScanId == after.ScanId, "恢复结果必须保留两个不同的 ScanId，不能复用修复前快照。");
        Require(executor.Calls == 1, "安全计划应只执行一次。");
        Require(executor.LastPlan is not null && executor.LastPlan.ActionIds.SequenceEqual(["codex.proxy.env", "codex.desktop.restart"]), "端到端服务必须使用安全计划生成器并保持 Desktop 重启最后。");
        Require(result.Status == ReconnectingRecoveryStatus.Recovered, "修复后网络可用且 Desktop 正在运行时应判定 Recovered。");
        Require(stages.SequenceEqual([
            ReconnectingSelfHealStage.BeforeScan,
            ReconnectingSelfHealStage.PlanBuilt,
            ReconnectingSelfHealStage.Executing,
            ReconnectingSelfHealStage.AfterScan,
            ReconnectingSelfHealStage.Completed
        ]), "专项自愈进度阶段顺序不正确。");
    }

    private static async Task ProvesVerifyFailureUsesExistingRollbackPath()
    {
        var scan = Scan(
            directOk: false,
            proxyOk: true,
            desktopRunning: false,
            issues: [Repairable("proxy-env", "codex.proxy.env", requiresRestart: true)]);
        var action = new FakeAction("codex.proxy.env", verify: false);
        var factory = new FakeActionFactory(action);
        var executor = new ReconnectingRepairExecutor(factory);
        var plan = ReconnectingSelfHealPlan.Build(scan);

        var results = await executor.ExecuteAsync(plan, scan, CancellationToken.None);

        Require(action.Executed, "代理修复动作必须被执行。");
        Require(action.Verified, "执行后必须走 VerifyAsync，而不是把执行成功等同于修复成功。");
        Require(action.RolledBack, "验证失败必须复用现有 RepairEngine 的 RollbackAsync。");
        Require(results.Count == 1 && results[0].Status == RepairActionStatus.RolledBack, "验证失败后审计结果必须标记 RolledBack。");
    }

    private static async Task ProvesCancellationStopsLaterMutation()
    {
        using var cts = new CancellationTokenSource();
        var before = Scan(
            directOk: false,
            proxyOk: true,
            desktopRunning: true,
            issues: [Repairable("proxy-env", "codex.proxy.env", requiresRestart: true)]);
        var scanner = new CancellingScanner(before, cts);
        var executor = new RecordingExecutor([]);
        var service = new ReconnectingSelfHealService(scanner, executor);

        var cancelled = false;
        try
        {
            await service.RecoverAsync(null, cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Require(cancelled, "取消后 RecoverAsync 必须抛出 OperationCanceledException。");
        Require(scanner.Calls == 1, "取消发生在修复前扫描后时不得继续执行修复后扫描。");
        Require(executor.Calls == 0, "取消后不得执行任何后续变更动作。");
    }

    private static CodexHealthScanResult Scan(
        bool directOk,
        bool proxyOk,
        bool desktopRunning,
        IReadOnlyList<CodexIssue> issues)
    {
        var discovery = CodexDiscoveryResult.Empty() with
        {
            DesktopClients = desktopRunning
                ? [new CodexDesktopInstallationInfo("Codex Desktop", "test", @"C:\Apps\Codex.exe", "8.2.0", true, [1234])]
                : []
        };

        var diagnosis = new DiagnosisResult(
            "8.2.0",
            directOk || proxyOk ? HealthState.Warning : HealthState.Error,
            directOk ? FailureClass.Healthy : proxyOk ? FailureClass.ProxyRequired : FailureClass.TlsFailure,
            "test",
            "test",
            new ProbeResult(true),
            new ProbeResult(directOk, directOk ? null : "direct failed"),
            new ProbeResult(proxyOk, proxyOk ? null : "proxy failed", proxyOk ? 403 : null),
            proxyOk ? "http://127.0.0.1:7897" : string.Empty,
            new ProxyEnvironmentState(false, string.Empty, string.Empty),
            new ConflictState(string.Empty, string.Empty, false),
            new ConflictState(string.Empty, string.Empty, false),
            new TunState(false, false, [], []),
            desktopRunning ? 1 : 0);

        return new CodexHealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, issues, discovery, diagnosis);
    }

    private static CodexIssue Repairable(string id, string actionId, bool requiresRestart = false) =>
        new(id, "network", CodexIssueSeverity.Warning, id, id, id, id, CodexIssueStatus.Repairable, true, actionId, true, requiresRestart, false, true, actionId + ".verify", id);

    private static CodexIssue OkIssue(string id, string category) =>
        new(id, category, CodexIssueSeverity.Ok, id, id, id, string.Empty, CodexIssueStatus.Detected, false, null, false, false, false, false, null, id);

    private sealed class QueueScanner(IEnumerable<CodexHealthScanResult> scans) : IHealthScanner
    {
        private readonly Queue<CodexHealthScanResult> _scans = new(scans);
        public int Calls { get; private set; }
        public Task<CodexHealthScanResult> ScanAsync(IProgress<HealthScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(_scans.Dequeue());
        }
    }

    private sealed class CancellingScanner(CodexHealthScanResult result, CancellationTokenSource source) : IHealthScanner
    {
        public int Calls { get; private set; }
        public Task<CodexHealthScanResult> ScanAsync(IProgress<HealthScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            source.Cancel();
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingExecutor(IReadOnlyList<RepairActionResult> results) : IReconnectingRepairExecutor
    {
        public int Calls { get; private set; }
        public ReconnectingSelfHealPlan? LastPlan { get; private set; }
        public Task<IReadOnlyList<RepairActionResult>> ExecuteAsync(ReconnectingSelfHealPlan plan, CodexHealthScanResult beforeScan, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastPlan = plan;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeActionFactory(IRepairAction action) : IReconnectingRepairActionFactory
    {
        public IRepairAction? Create(string actionId, CodexHealthScanResult scan) =>
            action.ActionId.Equals(actionId, StringComparison.OrdinalIgnoreCase) ? action : null;
    }

    private sealed class FakeAction(string actionId, bool verify) : IRepairAction
    {
        public string ActionId { get; } = actionId;
        public string TitleZh => actionId;
        public bool RequiresRestart => false;
        public bool BackupRequired => false;
        public bool Executed { get; private set; }
        public bool Verified { get; private set; }
        public bool RolledBack { get; private set; }

        public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Executed = true;
            return Task.FromResult(new RepairActionExecution(null, "executed"));
        }

        public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verified = true;
            return Task.FromResult(verify);
        }

        public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }
    }

    private sealed class InlineProgress(Action<ReconnectingSelfHealProgress> callback) : IProgress<ReconnectingSelfHealProgress>
    {
        public void Report(ReconnectingSelfHealProgress value) => callback(value);
    }
}
