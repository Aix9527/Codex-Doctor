namespace CodexDoctor.Native;

public enum ReconnectingSelfHealStage
{
    BeforeScan,
    PlanBuilt,
    Executing,
    AfterScan,
    Completed
}

public sealed record ReconnectingSelfHealProgress(
    ReconnectingSelfHealStage Stage,
    string MessageZh);

public interface IReconnectingRepairExecutor
{
    Task<IReadOnlyList<RepairActionResult>> ExecuteAsync(
        ReconnectingSelfHealPlan plan,
        CodexHealthScanResult beforeScan,
        CancellationToken cancellationToken);
}

public interface IReconnectingRepairActionFactory
{
    IRepairAction? Create(string actionId, CodexHealthScanResult scan);
}

public sealed class ReconnectingSelfHealService
{
    private readonly IHealthScanner _scanner;
    private readonly IReconnectingRepairExecutor _executor;

    public ReconnectingSelfHealService(IHealthScanner scanner, IReconnectingRepairExecutor executor)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<ReconnectingRecoveryResult> RecoverAsync(
        IProgress<ReconnectingSelfHealProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ReconnectingSelfHealProgress(ReconnectingSelfHealStage.BeforeScan, "正在执行 Reconnecting 修复前全量扫描。"));
        var before = await _scanner.ScanAsync(null, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = ReconnectingSelfHealPlan.Build(before);
        progress?.Report(new ReconnectingSelfHealProgress(
            ReconnectingSelfHealStage.PlanBuilt,
            plan.ActionIds.Count == 0 ? "未生成可安全执行的专项修复动作。" : $"已生成 {plan.ActionIds.Count} 个专项白名单动作。"));
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<RepairActionResult> actions = [];
        if (plan.ActionIds.Count > 0)
        {
            progress?.Report(new ReconnectingSelfHealProgress(ReconnectingSelfHealStage.Executing, "正在执行并验证专项修复动作；验证失败将回滚。"));
            actions = await _executor.ExecuteAsync(plan, before, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new ReconnectingSelfHealProgress(ReconnectingSelfHealStage.Executing, "没有安全自动修复动作，跳过变更阶段。"));
        }
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new ReconnectingSelfHealProgress(ReconnectingSelfHealStage.AfterScan, "正在执行独立的修复后全量复检。"));
        var after = await _scanner.ScanAsync(null, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var status = ReconnectingRecoveryClassifier.Classify(after, actions);
        var summary = Describe(status, actions);
        progress?.Report(new ReconnectingSelfHealProgress(ReconnectingSelfHealStage.Completed, summary));

        return new ReconnectingRecoveryResult(
            status,
            before.ScanId,
            after.ScanId,
            actions,
            summary,
            before.Diagnosis,
            after.Diagnosis);
    }

    private static string Describe(ReconnectingRecoveryStatus status, IReadOnlyList<RepairActionResult> actions)
    {
        var succeeded = actions.Count(x => x.Status == RepairActionStatus.Succeeded);
        var rolledBack = actions.Count(x => x.Status == RepairActionStatus.RolledBack);
        var failed = actions.Count(x => x.Status is RepairActionStatus.Failed or RepairActionStatus.RollbackFailed);
        var suffix = $"动作：成功 {succeeded}，已回滚 {rolledBack}，失败 {failed}。";

        return status switch
        {
            ReconnectingRecoveryStatus.Recovered => "RECOVERED：网络路径与 Codex Desktop 均通过修复后复检。" + suffix,
            ReconnectingRecoveryStatus.NetworkRecovered => "NETWORK_RECOVERED：网络路径已恢复，但 Desktop 尚未满足完整恢复条件。" + suffix,
            ReconnectingRecoveryStatus.ProxyFailed => "PROXY_FAILED：代理链路或 Codex 专用代理修复未通过验证。" + suffix,
            ReconnectingRecoveryStatus.DnsFailed => "DNS_FAILED：修复后 DNS 仍不可用。" + suffix,
            ReconnectingRecoveryStatus.TlsFailed => "TLS_FAILED：修复后没有可用 TLS 网络路径。" + suffix,
            ReconnectingRecoveryStatus.DesktopRestartFailed => "DESKTOP_RESTART_FAILED：网络已可用，但 Desktop 重启验证失败。" + suffix,
            _ => "MANUAL_REQUIRED：自动修复无法证明 Reconnecting 已恢复，需要人工处理。" + suffix
        };
    }
}

public sealed class ReconnectingRepairExecutor : IReconnectingRepairExecutor
{
    private readonly IReconnectingRepairActionFactory _factory;

    public ReconnectingRepairExecutor(IReconnectingRepairActionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<RepairActionResult>> ExecuteAsync(
        ReconnectingSelfHealPlan plan,
        CodexHealthScanResult beforeScan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(beforeScan);

        var actions = new List<IRepairAction>();
        var unresolved = new Dictionary<string, RepairActionResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var actionId in plan.ActionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReconnectingSelfHealPlan.AllowedActionIds.Contains(actionId))
                throw new InvalidOperationException($"专项修复计划包含非白名单动作：{actionId}");

            var action = _factory.Create(actionId, beforeScan);
            if (action is null)
            {
                unresolved[actionId] = new RepairActionResult(
                    actionId,
                    actionId,
                    RepairActionStatus.Failed,
                    "无法根据修复前扫描快照安全实例化该动作，已拒绝执行。",
                    null);
                continue;
            }

            if (!ReconnectingSelfHealPlan.AllowedActionIds.Contains(action.ActionId))
                throw new InvalidOperationException($"动作工厂返回非白名单动作：{action.ActionId}");
            actions.Add(action);
        }

        var engine = new CodexRepairEngine(new RepairActionCatalog(actions));
        var repairPlan = new RepairPlan(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            beforeScan.ScanId,
            actions,
            actions.Any(x => x.RequiresRestart),
            [],
            []);
        var session = actions.Count == 0
            ? new RepairSessionResult(repairPlan.PlanId, [])
            : await engine.ExecuteAsync(repairPlan, cancellationToken).ConfigureAwait(false);

        var executed = session.Actions.ToDictionary(x => x.ActionId, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<RepairActionResult>(plan.ActionIds.Count);
        foreach (var actionId in plan.ActionIds)
        {
            if (executed.TryGetValue(actionId, out var result)) ordered.Add(result);
            else if (unresolved.TryGetValue(actionId, out var missing)) ordered.Add(missing);
        }
        return ordered;
    }
}

public sealed class DefaultReconnectingRepairActionFactory : IReconnectingRepairActionFactory
{
    private readonly string _userProfile;
    private readonly string _backupRoot;
    private readonly IProxyToolRepairBackend _gitBackend;
    private readonly IProxyToolRepairBackend _npmBackend;
    private readonly IDesktopRestartBackend _desktopBackend;

    public DefaultReconnectingRepairActionFactory(
        string? userProfile = null,
        string? backupRoot = null,
        IProxyToolRepairBackend? gitBackend = null,
        IProxyToolRepairBackend? npmBackend = null,
        IDesktopRestartBackend? desktopBackend = null)
    {
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexDoctorV8",
            "repair-backups");
        _gitBackend = gitBackend ?? new GitProxyToolBackend();
        _npmBackend = npmBackend ?? new NpmProxyToolBackend();
        _desktopBackend = desktopBackend ?? new DesktopRestartBackend();
    }

    public IRepairAction? Create(string actionId, CodexHealthScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (!ReconnectingSelfHealPlan.AllowedActionIds.Contains(actionId)) return null;

        if (actionId.Equals(ReconnectingSelfHealPlan.ProxyEnvActionId, StringComparison.OrdinalIgnoreCase))
        {
            var diagnosis = scan.Diagnosis;
            if (diagnosis?.Proxy.Ok != true || string.IsNullOrWhiteSpace(diagnosis.ProxyUrl) || diagnosis.DirectTls.Ok)
                return null;
            return new ProxyEnvRepairAction(_userProfile, diagnosis.ProxyUrl);
        }

        if (actionId.Equals(ReconnectingSelfHealPlan.GitProxyClearActionId, StringComparison.OrdinalIgnoreCase))
            return new GitProxyRepairAction(_gitBackend, _backupRoot);

        if (actionId.Equals(ReconnectingSelfHealPlan.NpmProxyClearActionId, StringComparison.OrdinalIgnoreCase))
            return new NpmProxyRepairAction(_npmBackend, _backupRoot);

        if (actionId.Equals(ReconnectingSelfHealPlan.DesktopRestartActionId, StringComparison.OrdinalIgnoreCase))
        {
            var desktop = scan.Discovery.DesktopClients
                .Where(x => !string.IsNullOrWhiteSpace(x.ExecutablePath))
                .OrderByDescending(x => x.IsRunning)
                .ThenByDescending(x => x.ProcessIds.Count)
                .FirstOrDefault();
            if (desktop is null) return null;
            return new CodexRestartRepairAction(_desktopBackend, desktop.ExecutablePath, desktop.ProcessIds);
        }

        return null;
    }
}
