namespace CodexDoctor.Native;

public sealed record MainDashboardState(
    bool ShowScanPrimary,
    bool CanStart,
    bool CanRestart,
    bool CanRepair,
    bool CanRecoverReconnecting,
    bool CanExportReport,
    int RepairableCount,
    int CriticalCount,
    int UrgentCount,
    int WarningCount,
    int InfoCount,
    int OkCount,
    string MigrationActionZh,
    string LanguageActionZh)
{
    public static MainDashboardState From(CodexHealthScanResult? scan)
    {
        if (scan is null)
        {
            return new MainDashboardState(
                true, false, false, false, false, false,
                0, 0, 0, 0, 0, 0,
                "智能迁移/恢复",
                "一键中文");
        }

        var issues = scan.Issues;
        var hasDesktop = CodexDesktopSelector.SelectPreferred(scan.Discovery.DesktopClients) is not null;
        var repairable = issues.Count(x =>
            x.Status == CodexIssueStatus.Repairable &&
            x.AutoRepairable &&
            !string.IsNullOrWhiteSpace(x.RepairActionId));
        var hasBlockingRecoveryGate = issues.Any(x =>
            x.Status is CodexIssueStatus.ManualRequired or CodexIssueStatus.ExternalRequired &&
            x.Severity is CodexIssueSeverity.Critical or CodexIssueSeverity.Urgent);

        var languageAction = LanguageActionResolver.Resolve(scan.Discovery.LanguageState).ActionZh;
        var migrationAction = MigrationActionResolver.Resolve(
            scan.Discovery.DataDirectory,
            hasMigrationState: false,
            canRecoverInterrupted: false).ActionZh;

        return new MainDashboardState(
            false,
            hasDesktop,
            hasDesktop,
            repairable > 0,
            hasDesktop && !hasBlockingRecoveryGate,
            true,
            repairable,
            issues.Count(x => x.Severity == CodexIssueSeverity.Critical),
            issues.Count(x => x.Severity == CodexIssueSeverity.Urgent),
            issues.Count(x => x.Severity == CodexIssueSeverity.Warning),
            issues.Count(x => x.Severity == CodexIssueSeverity.Info),
            issues.Count(x => x.Severity == CodexIssueSeverity.Ok),
            migrationAction,
            languageAction);
    }
}
