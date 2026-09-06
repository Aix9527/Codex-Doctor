using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81HealthContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var issues = new[]
        {
            CodexIssue.ForTest("ok", CodexIssueSeverity.Ok),
            CodexIssue.ForTest("critical", CodexIssueSeverity.Critical),
            CodexIssue.ForTest("warning", CodexIssueSeverity.Warning),
            CodexIssue.ForTest("urgent", CodexIssueSeverity.Urgent),
            CodexIssue.ForTest("info", CodexIssueSeverity.Info)
        };

        var sorted = CodexIssueClassifier.SortIssues(issues);
        Require(sorted.Select(x => x.Severity).SequenceEqual(new[]
        {
            CodexIssueSeverity.Critical,
            CodexIssueSeverity.Urgent,
            CodexIssueSeverity.Warning,
            CodexIssueSeverity.Info,
            CodexIssueSeverity.Ok
        }), "严重度排序不正确。");
        Require(sorted.Count == 5, "健康模型必须保留多个并存问题。");

        var scan = CodexHealthScanResult.ForTest(sorted);
        Require(scan.Issues.Count == 5, "扫描结果必须承载完整问题集合。");
        Require(scan.ScanId != Guid.Empty, "扫描结果必须有非空 ScanId。");
    }
}
