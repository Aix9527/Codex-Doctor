using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81ScannerContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var checks = new IHealthCheck[]
        {
            new FakeCheck("a", [CodexIssue.ForTest("a-ok", CodexIssueSeverity.Ok)]),
            new ThrowingCheck("broken"),
            new FakeCheck("c", [CodexIssue.ForTest("c-warning", CodexIssueSeverity.Warning)])
        };

        var progress = new List<HealthScanProgress>();
        var scanner = new CodexHealthScanner(checks);
        var result = await scanner.ScanAsync(new InlineProgress<HealthScanProgress>(progress.Add));

        Require(result.Issues.Any(x => x.Id == "a-ok"), "前置检查结果丢失。");
        Require(result.Issues.Any(x => x.Id == "c-warning"), "一个检查异常不得阻断后续检查。");
        Require(result.Issues.Any(x => x.SourceCheckId == "broken" && x.Status == CodexIssueStatus.ManualRequired), "检查异常必须成为可审计的 ManualRequired 问题。");
        Require(progress.Count == checks.Length, "每个扫描阶段必须报告一次完成进度。");
        Require(progress[^1].Completed == checks.Length && progress[^1].Total == checks.Length, "最终扫描进度计数不正确。");
        Require(result.ScanId != Guid.Empty, "统一扫描必须生成 ScanId。");
    }

    private sealed class FakeCheck(string id, IReadOnlyList<CodexIssue> issues) : IHealthCheck
    {
        public string Id => id;
        public string NameZh => id;
        public Task<IReadOnlyList<CodexIssue>> RunAsync(CancellationToken cancellationToken) => Task.FromResult(issues);
    }

    private sealed class ThrowingCheck(string id) : IHealthCheck
    {
        public string Id => id;
        public string NameZh => id;
        public Task<IReadOnlyList<CodexIssue>> RunAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
