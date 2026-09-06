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

        var snapshotCounter = 0;
        Task<CodexDiscoveryResult> NextDiscovery()
        {
            snapshotCounter++;
            var empty = CodexDiscoveryResult.Empty();
            return Task.FromResult(empty with
            {
                LanguageState = empty.LanguageState with { MethodZh = $"snapshot-{snapshotCounter}" }
            });
        }

        var rescanScanner = new CodexHealthScanner(
            NextDiscovery,
            () => Task.FromResult<DiagnosisResult?>(null),
            (_, _) => [new FakeCheck("snapshot", [CodexIssue.ForTest("snapshot-ok", CodexIssueSeverity.Ok)])]);

        var first = await rescanScanner.ScanAsync();
        var second = await rescanScanner.ScanAsync();

        Require(first.Discovery.LanguageState.MethodZh == "snapshot-1", "第一次扫描必须使用第一次实时 Discovery 快照。");
        Require(second.Discovery.LanguageState.MethodZh == "snapshot-2", "修复后复检必须重新获取 Discovery 快照，不能复用第一次 Lazy 结果。");
        Require(snapshotCounter == 2, "同一个健康扫描器连续扫描两次时必须调用 Discovery provider 两次。");
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
