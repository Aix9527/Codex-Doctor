namespace CodexDoctor.Native;

public sealed record MainDashboardState(
    bool ShowScanPrimary,
    bool CanStart,
    bool CanRestart,
    bool CanRepair,
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
                true, false, false, false, false,
                0, 0, 0, 0, 0, 0,
                "智能迁移/恢复",
                "一键中文");
        }

        var issues = scan.Issues;
        var hasDesktop = scan.Discovery.DesktopClients.Any(x => !string.IsNullOrWhiteSpace(x.ExecutablePath));
        var repairable = issues.Count(x =>
            x.Status == CodexIssueStatus.Repairable &&
            x.AutoRepairable &&
            !string.IsNullOrWhiteSpace(x.RepairActionId));

        var language = scan.Discovery.LanguageState;
        var languageAction = language.Applied || language.UiLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "已是中文"
            : language.NeedsUserAction ? "一键中文" : "一键中文";

        return new MainDashboardState(
            false,
            hasDesktop,
            hasDesktop,
            repairable > 0,
            true,
            repairable,
            issues.Count(x => x.Severity == CodexIssueSeverity.Critical),
            issues.Count(x => x.Severity == CodexIssueSeverity.Urgent),
            issues.Count(x => x.Severity == CodexIssueSeverity.Warning),
            issues.Count(x => x.Severity == CodexIssueSeverity.Info),
            issues.Count(x => x.Severity == CodexIssueSeverity.Ok),
            "智能迁移/恢复",
            languageAction);
    }
}
