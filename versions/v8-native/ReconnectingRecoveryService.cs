namespace CodexDoctor.Native;

public interface IReconnectingRepairExecutor
{
    RepairPlan BuildPlan(CodexHealthScanResult scan);
    Task<RepairSessionResult> ExecuteAsync(RepairPlan plan, CancellationToken cancellationToken = default);
}

public sealed class CodexRepairEngineReconnectingExecutor : IReconnectingRepairExecutor
{
    private readonly CodexRepairEngine _engine;

    public CodexRepairEngineReconnectingExecutor(CodexRepairEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public RepairPlan BuildPlan(CodexHealthScanResult scan) => _engine.BuildPlan(scan);

    public Task<RepairSessionResult> ExecuteAsync(RepairPlan plan, CancellationToken cancellationToken = default) =>
        _engine.ExecuteAsync(plan, cancellationToken);
}

public sealed class ReconnectingRecoveryService
{
    private readonly IHealthScanner _healthScanner;
    private readonly IReconnectingRepairExecutor _repairExecutor;
    private readonly IDesktopRestartBackend _restartBackend;
    private readonly Func<CancellationToken, Task<CodexDiscoveryResult>> _discoveryRefresh;

    public ReconnectingRecoveryService(
        IHealthScanner healthScanner,
        IReconnectingRepairExecutor repairExecutor,
        IDesktopRestartBackend restartBackend,
        Func<CancellationToken, Task<CodexDiscoveryResult>> discoveryRefresh)
    {
        _healthScanner = healthScanner ?? throw new ArgumentNullException(nameof(healthScanner));
        _repairExecutor = repairExecutor ?? throw new ArgumentNullException(nameof(repairExecutor));
        _restartBackend = restartBackend ?? throw new ArgumentNullException(nameof(restartBackend));
        _discoveryRefresh = discoveryRefresh ?? throw new ArgumentNullException(nameof(discoveryRefresh));
    }

    public async Task<ReconnectingRecoveryResult> RecoverAsync(
        CodexHealthScanResult beforeScan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(beforeScan);
        cancellationToken.ThrowIfCancellationRequested();

        var decision = ReconnectingRecoveryDecisionEngine.Decide(beforeScan.Diagnosis);
        if (!decision.NetworkPathVerified && decision.FailureStatus is not null)
        {
            return Result(
                decision.FailureStatus.Value,
                decision.SummaryZh,
                beforeScan,
                afterScan: null,
                decision.SelectedProxyUrl,
                networkVerified: false,
                desktopVerified: false,
                actions: []);
        }

        var desktop = SelectTrustedDesktop(beforeScan.Discovery);
        if (desktop is null)
        {
            return Result(
                ReconnectingRecoveryStatus.ManualRequired,
                "没有发现可由扫描事实确认的 Codex/ChatGPT Desktop，已拒绝对未知程序执行自愈。",
                beforeScan,
                afterScan: null,
                decision.SelectedProxyUrl,
                networkVerified: decision.NetworkPathVerified,
                desktopVerified: false,
                actions: []);
        }

        var plan = _repairExecutor.BuildPlan(beforeScan);
        if (HasBlockingSafetyGate(plan))
        {
            return Result(
                ReconnectingRecoveryStatus.ManualRequired,
                "当前扫描包含严重的人工/外部处理安全门，自动 Reconnecting 自愈已停止。",
                beforeScan,
                afterScan: null,
                decision.SelectedProxyUrl,
                networkVerified: decision.NetworkPathVerified,
                desktopVerified: false,
                actions: []);
        }

        var repair = await _repairExecutor.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
        var restartAttempted = false;
        var restartFailed = false;

        if (desktop.IsRunning || PlanChangesCodexNetwork(plan))
        {
            restartAttempted = true;
            try
            {
                _restartBackend.Restart(desktop.ExecutablePath, desktop.ProcessIds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                restartFailed = true;
            }
        }

        CodexDiscoveryResult freshDiscovery;
        try
        {
            freshDiscovery = await _discoveryRefresh(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            freshDiscovery = CodexDiscoveryResult.Empty();
        }

        var afterScan = await _healthScanner.ScanAsync(null, cancellationToken).ConfigureAwait(false);
        var afterDecision = ReconnectingRecoveryDecisionEngine.Decide(afterScan.Diagnosis);
        var networkVerified = afterDecision.NetworkPathVerified;
        var desktopVerified = IsSameDesktopRunning(desktop, freshDiscovery);

        ReconnectingRecoveryStatus status;
        string summary;

        if (networkVerified && desktopVerified && !restartFailed)
        {
            status = ReconnectingRecoveryStatus.Recovered;
            summary = "网络路径与扫描确认的 Desktop 均已通过修复后验证，Reconnecting 自愈闭环完成。";
        }
        else if (networkVerified && restartFailed)
        {
            status = ReconnectingRecoveryStatus.DesktopRestartFailed;
            summary = "网络路径已经恢复，但扫描确认的 Desktop 重启失败；不能把当前状态报告为 RECOVERED。";
        }
        else if (networkVerified)
        {
            status = ReconnectingRecoveryStatus.NetworkRecovered;
            summary = restartAttempted
                ? "网络路径已经恢复，但 fresh Desktop discovery 未能证明目标客户端正在运行。"
                : "网络路径已经恢复，但 Desktop 运行状态仍未通过 fresh discovery 验证。";
        }
        else
        {
            status = afterDecision.FailureStatus ?? ReconnectingRecoveryStatus.ManualRequired;
            summary = afterDecision.SummaryZh;
        }

        return Result(
            status,
            summary,
            beforeScan,
            afterScan,
            decision.SelectedProxyUrl ?? afterDecision.SelectedProxyUrl,
            networkVerified,
            desktopVerified,
            repair.Actions);
    }

    private static CodexDesktopInstallationInfo? SelectTrustedDesktop(CodexDiscoveryResult discovery) =>
        discovery.DesktopClients
            .Where(x => !string.IsNullOrWhiteSpace(x.ExecutablePath))
            .OrderByDescending(x => x.IsRunning)
            .FirstOrDefault();

    private static bool HasBlockingSafetyGate(RepairPlan plan) =>
        plan.ManualIssues.Any(IsBlockingIssue) || plan.ExternalIssues.Any(IsBlockingIssue);

    private static bool IsBlockingIssue(CodexIssue issue) =>
        issue.Severity is CodexIssueSeverity.Critical or CodexIssueSeverity.Urgent;

    private static bool PlanChangesCodexNetwork(RepairPlan plan) =>
        plan.Actions.Any(x =>
            x.ActionId.Equals("codex.proxy.env", StringComparison.OrdinalIgnoreCase) ||
            x.ActionId.Equals("git.proxy.clear", StringComparison.OrdinalIgnoreCase) ||
            x.ActionId.Equals("git.proxy.cleanup", StringComparison.OrdinalIgnoreCase) ||
            x.ActionId.Equals("npm.proxy.clear", StringComparison.OrdinalIgnoreCase) ||
            x.ActionId.Equals("npm.proxy.cleanup", StringComparison.OrdinalIgnoreCase));

    private static bool IsSameDesktopRunning(
        CodexDesktopInstallationInfo expected,
        CodexDiscoveryResult freshDiscovery)
    {
        string expectedPath;
        try { expectedPath = Path.GetFullPath(expected.ExecutablePath); }
        catch { return false; }

        foreach (var candidate in freshDiscovery.DesktopClients)
        {
            if (!candidate.IsRunning || string.IsNullOrWhiteSpace(candidate.ExecutablePath)) continue;
            try
            {
                if (string.Equals(
                    Path.GetFullPath(candidate.ExecutablePath),
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
        }
        return false;
    }

    private static ReconnectingRecoveryResult Result(
        ReconnectingRecoveryStatus status,
        string summaryZh,
        CodexHealthScanResult beforeScan,
        CodexHealthScanResult? afterScan,
        string? selectedProxyUrl,
        bool networkVerified,
        bool desktopVerified,
        IReadOnlyList<RepairActionResult> actions) =>
        new(
            status,
            summaryZh,
            beforeScan.ScanId,
            afterScan?.ScanId,
            selectedProxyUrl,
            networkVerified,
            desktopVerified,
            actions);
}
