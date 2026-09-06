using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81RepairActionsContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var temp = Path.Combine(Path.GetTempPath(), "CodexDoctorV81Repair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, ".codex"));
        var env = Path.Combine(temp, ".codex", ".env");
        File.WriteAllText(env, "FOO=bar\nHTTP_PROXY=http://127.0.0.1:1111\nHTTPS_PROXY=http://127.0.0.1:1111\n");

        var beforeUserHttp = Environment.GetEnvironmentVariable("HTTP_PROXY", EnvironmentVariableTarget.User);
        var beforeUserHttps = Environment.GetEnvironmentVariable("HTTPS_PROXY", EnvironmentVariableTarget.User);
        var action = new ProxyEnvRepairAction(temp, "http://127.0.0.1:7897");
        var execution = await action.ExecuteAsync(CancellationToken.None);
        Require(!string.IsNullOrWhiteSpace(execution.BackupPath) && File.Exists(execution.BackupPath), "修改已有 .env 前必须生成备份。");
        Require(await action.VerifyAsync(execution, CancellationToken.None), "代理动作执行后必须验证 HTTP/HTTPS_PROXY。");
        Require(Environment.GetEnvironmentVariable("HTTP_PROXY", EnvironmentVariableTarget.User) == beforeUserHttp, "默认动作不得修改用户级 HTTP_PROXY。");
        Require(Environment.GetEnvironmentVariable("HTTPS_PROXY", EnvironmentVariableTarget.User) == beforeUserHttps, "默认动作不得修改用户级 HTTPS_PROXY。");
        await action.RollbackAsync(execution, CancellationToken.None);
        Require(File.ReadAllText(env).Contains("127.0.0.1:1111"), "回滚必须恢复原 .env。");

        var beforeScan = CodexHealthScanResult.ForTest([
            new CodexIssue("proxy", "network", CodexIssueSeverity.Urgent, "proxy", "proxy", "", "", CodexIssueStatus.Repairable, true, "codex.proxy.env", true, false, true, true, "verify", "test")
        ]);
        var afterScan = CodexHealthScanResult.ForTest([CodexIssue.ForTest("healthy", CodexIssueSeverity.Ok)]);
        var scanner = new CountingScanner(afterScan);
        var engineAction = new ProxyEnvRepairAction(temp, "http://127.0.0.1:7897");
        var engine = new CodexRepairEngine(new RepairActionCatalog([engineAction]), scanner);
        var plan = engine.BuildPlan(beforeScan);
        var session = await engine.ExecuteAndRescanAsync(plan, beforeScan);
        Require(scanner.ScanCalls == 1, "一键修复结束后必须执行一次全量复检。");
        Require(session.BeforeScan.ScanId == beforeScan.ScanId, "修复前扫描必须保留。");
        Require(session.AfterScan.ScanId == afterScan.ScanId && session.AfterScan.ScanId != session.BeforeScan.ScanId, "修复后必须使用新的 ScanId。");

        Directory.Delete(temp, true);
    }

    private sealed class CountingScanner(CodexHealthScanResult result) : IHealthScanner
    {
        public int ScanCalls { get; private set; }
        public Task<CodexHealthScanResult> ScanAsync(IProgress<HealthScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ScanCalls++;
            return Task.FromResult(result);
        }
    }
}
