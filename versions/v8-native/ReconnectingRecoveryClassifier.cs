namespace CodexDoctor.Native;

public static class ReconnectingRecoveryClassifier
{
    public static ReconnectingRecoveryStatus Classify(
        CodexHealthScanResult after,
        IReadOnlyList<RepairActionResult> actions)
    {
        ArgumentNullException.ThrowIfNull(after);
        actions ??= [];

        var issues = after.Issues ?? [];
        var networkOk = HasIssue(issues, "network-path-ok");

        if (networkOk)
        {
            var restartFailed = actions.Any(action =>
                action.ActionId.Equals("codex.desktop.restart", StringComparison.OrdinalIgnoreCase) &&
                action.Status is RepairActionStatus.Failed or RepairActionStatus.RollbackFailed);
            if (restartFailed)
                return ReconnectingRecoveryStatus.DesktopRestartFailed;

            var desktopRunning = after.Discovery.DesktopClients.Any(client => client.IsRunning);
            var blockingNetworkIssue = issues.Any(issue =>
                issue.Category.Equals("network", StringComparison.OrdinalIgnoreCase) &&
                issue.Severity is CodexIssueSeverity.Critical or CodexIssueSeverity.Urgent &&
                !issue.Id.Equals("network-path-ok", StringComparison.OrdinalIgnoreCase));

            return desktopRunning && !blockingNetworkIssue
                ? ReconnectingRecoveryStatus.Recovered
                : ReconnectingRecoveryStatus.NetworkRecovered;
        }

        if (HasIssue(issues, "dns-fail"))
            return ReconnectingRecoveryStatus.DnsFailed;

        var proxyActionFailed = actions.Any(action =>
            action.ActionId.Equals("codex.proxy.env", StringComparison.OrdinalIgnoreCase) &&
            action.Status is RepairActionStatus.Failed or RepairActionStatus.RollbackFailed or RepairActionStatus.RolledBack);
        var proxyWasRequiredButUnavailable =
            HasIssue(issues, "tls-direct-blocked") && HasIssue(issues, "proxy-https-none");
        if (proxyActionFailed || proxyWasRequiredButUnavailable)
            return ReconnectingRecoveryStatus.ProxyFailed;

        if (HasIssue(issues, "tls-direct-fail"))
            return ReconnectingRecoveryStatus.TlsFailed;

        return ReconnectingRecoveryStatus.ManualRequired;
    }

    private static bool HasIssue(IEnumerable<CodexIssue> issues, string id) =>
        issues.Any(issue => issue.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
