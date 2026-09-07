namespace CodexDoctor.Native;

public sealed record ReconnectingSelfHealPlan(
    IReadOnlyList<string> ActionIds,
    string ReasonZh)
{
    public const string ProxyEnvActionId = "codex.proxy.env";
    public const string GitProxyClearActionId = "git.proxy.clear";
    public const string NpmProxyClearActionId = "npm.proxy.clear";
    public const string DesktopRestartActionId = "codex.desktop.restart";

    public static IReadOnlySet<string> AllowedActionIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ProxyEnvActionId,
        GitProxyClearActionId,
        NpmProxyClearActionId,
        DesktopRestartActionId
    };

    public static ReconnectingSelfHealPlan Build(CodexHealthScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(scan);

        var diagnosis = scan.Diagnosis;
        if (diagnosis is null)
            return new ReconnectingSelfHealPlan([], "网络诊断未完成，不执行 Reconnecting 自动修复。");

        var actions = new List<string>();
        var repairableIssues = scan.Issues
            .Where(x => x.AutoRepairable && x.Status == CodexIssueStatus.Repairable && !string.IsNullOrWhiteSpace(x.RepairActionId))
            .ToArray();

        var directHealthy = diagnosis.DirectTls.Ok;
        var verifiedProxy = diagnosis.Proxy.Ok && !string.IsNullOrWhiteSpace(diagnosis.ProxyUrl);

        if (!directHealthy && verifiedProxy && HasAction(repairableIssues, ProxyEnvActionId))
            AddOnce(actions, ProxyEnvActionId);

        if (HasAction(repairableIssues, GitProxyClearActionId, "git.proxy.cleanup"))
            AddOnce(actions, GitProxyClearActionId);

        if (HasAction(repairableIssues, NpmProxyClearActionId, "npm.proxy.cleanup"))
            AddOnce(actions, NpmProxyClearActionId);

        var desktopDiscovered = scan.Discovery.DesktopClients.Any(x => !string.IsNullOrWhiteSpace(x.ExecutablePath));
        var restartExplicit = HasAction(repairableIssues, DesktopRestartActionId);
        var includedMutationRequiresRestart = repairableIssues.Any(issue =>
        {
            if (!issue.RequiresCodexRestart) return false;
            var normalized = NormalizeActionId(issue.RepairActionId);
            return normalized is not null && actions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        });

        if (desktopDiscovered && (restartExplicit || includedMutationRequiresRestart))
            AddOnce(actions, DesktopRestartActionId);

        actions = actions.Where(AllowedActionIds.Contains).ToList();

        var reason = actions.Count == 0
            ? directHealthy
                ? "当前直连网络可用，未发现需要执行的 Reconnecting 白名单修复。"
                : verifiedProxy
                    ? "已验证代理可用，但当前扫描没有匹配的安全自动修复动作。"
                    : "未验证到可安全用于自动修复的网络路径，不执行变更。"
            : $"已生成 {actions.Count} 个 Reconnecting 白名单修复动作；网络/配置动作优先，Desktop 重启最后执行。";

        return new ReconnectingSelfHealPlan(actions.ToArray(), reason);
    }

    private static bool HasAction(IEnumerable<CodexIssue> issues, params string[] actionIds)
        => issues.Any(issue => actionIds.Any(id => string.Equals(issue.RepairActionId, id, StringComparison.OrdinalIgnoreCase)));

    private static string? NormalizeActionId(string? actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return null;
        if (actionId.Equals("git.proxy.cleanup", StringComparison.OrdinalIgnoreCase)) return GitProxyClearActionId;
        if (actionId.Equals("npm.proxy.cleanup", StringComparison.OrdinalIgnoreCase)) return NpmProxyClearActionId;
        return AllowedActionIds.Contains(actionId) ? actionId : null;
    }

    private static void AddOnce(List<string> actions, string actionId)
    {
        if (!AllowedActionIds.Contains(actionId)) return;
        if (!actions.Contains(actionId, StringComparer.OrdinalIgnoreCase))
            actions.Add(actionId);
    }
}
