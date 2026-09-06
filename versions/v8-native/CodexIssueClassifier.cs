namespace CodexDoctor.Native;

public static class CodexIssueClassifier
{
    private static readonly IReadOnlyDictionary<CodexIssueSeverity, int> SeverityOrder =
        new Dictionary<CodexIssueSeverity, int>
        {
            [CodexIssueSeverity.Critical] = 0,
            [CodexIssueSeverity.Urgent] = 1,
            [CodexIssueSeverity.Warning] = 2,
            [CodexIssueSeverity.Info] = 3,
            [CodexIssueSeverity.Ok] = 4
        };

    public static IReadOnlyList<CodexIssue> SortIssues(IEnumerable<CodexIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var materialized = issues.ToArray();
        var desktopFound = materialized.Any(x => x.Id.Equals("desktop-install", StringComparison.Ordinal));
        var desktopMissing = materialized.Any(x => x.Id.Equals("desktop-missing", StringComparison.Ordinal));

        return materialized
            .Select(x => NormalizeLanguageIssue(x, desktopFound, desktopMissing))
            .OrderBy(x => SeverityOrder[x.Severity])
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static CodexIssue NormalizeLanguageIssue(CodexIssue issue, bool desktopFound, bool desktopMissing)
    {
        if (!issue.Id.Equals("language-manual", StringComparison.Ordinal)) return issue;

        if (desktopMissing)
        {
            return issue with
            {
                Id = "language-not-applicable",
                Severity = CodexIssueSeverity.Info,
                TitleZh = "未发现 Desktop，界面语言切换不可用",
                SummaryZh = "先安装并重新扫描 Codex/ChatGPT Desktop 后，才可使用中文 / English 切换。",
                EvidenceZh = "Desktop=0",
                ImpactZh = "当前没有可绑定和验证的 Desktop 界面语言目标。",
                Status = CodexIssueStatus.NotApplicable,
                AutoRepairable = false,
                RepairActionId = null,
                RequiresAdmin = false,
                RequiresCodexRestart = false,
                RequiresUserConfirmation = false,
                BackupRequired = false,
                VerificationId = null
            };
        }

        if (!desktopFound) return issue;

        return issue with
        {
            Id = "language-switch-available",
            Severity = CodexIssueSeverity.Info,
            TitleZh = "Codex 界面语言可尝试切换",
            SummaryZh = "可使用中文 / English 按钮尝试切换；每次操作后必须重新验证目标语言，无法验证时不会报告成功。",
            EvidenceZh = string.IsNullOrWhiteSpace(issue.SummaryZh) ? "可信本地 UI 语言入口未确认。" : issue.SummaryZh,
            ImpactZh = "当前状态不代表自动切换必定成功；V8.1.1 仅会绑定扫描确认的 Desktop 并进行受限尝试。",
            Status = CodexIssueStatus.Detected,
            AutoRepairable = false,
            RepairActionId = null,
            RequiresAdmin = false,
            RequiresCodexRestart = false,
            RequiresUserConfirmation = false,
            BackupRequired = false,
            VerificationId = null
        };
    }
}
