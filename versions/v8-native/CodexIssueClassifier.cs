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
        return issues
            .OrderBy(x => SeverityOrder[x.Severity])
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
