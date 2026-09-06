using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81RepairEngineContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var action = new FakeRepairAction("codex.proxy.env", verifyOk: false);
        var catalog = new RepairActionCatalog([action]);
        var engine = new CodexRepairEngine(catalog);

        var scan = CodexHealthScanResult.ForTest([
            Repairable("env-proxy", "codex.proxy.env"),
            Manual("unknown-db"),
            External("service-outage"),
            Repairable("not-whitelisted", "windows.user.proxy.write")
        ]);

        var plan = engine.BuildPlan(scan);
        Require(plan.Actions.Count == 1, "只有白名单且可自动修复的问题才能进入 RepairPlan。");
        Require(plan.Actions[0].ActionId == "codex.proxy.env", "RepairPlan 动作映射错误。");
        Require(plan.ManualIssues.Count == 1 && plan.ExternalIssues.Count == 1, "人工/外部问题必须保留在计划摘要中。");
        Require(!plan.Actions.Any(x => x.ActionId == "windows.user.proxy.write"), "默认一键修复不得写 Windows 用户级代理。");

        var result = await engine.ExecuteAsync(plan);
        Require(action.ExecuteCalls == 1, "动作必须执行一次。");
        Require(action.VerifyCalls == 1, "动作执行后必须验证。");
        Require(action.RollbackCalls == 1, "验证失败必须回滚。");
        Require(result.Actions.Single().Status == RepairActionStatus.RolledBack, "验证失败后的动作状态必须是 RolledBack。");
    }

    private static CodexIssue Repairable(string id, string actionId) => new(
        id, "test", CodexIssueSeverity.Urgent, id, id, "evidence", "impact",
        CodexIssueStatus.Repairable, true, actionId, true, false, true, true,
        actionId + ".verify", "test");

    private static CodexIssue Manual(string id) => new(
        id, "test", CodexIssueSeverity.Warning, id, id, "evidence", "impact",
        CodexIssueStatus.ManualRequired, false, null, false, false, false, false,
        null, "test");

    private static CodexIssue External(string id) => new(
        id, "test", CodexIssueSeverity.Warning, id, id, "evidence", "impact",
        CodexIssueStatus.ExternalRequired, false, null, false, false, false, false,
        null, "test");

    private sealed class FakeRepairAction(string actionId, bool verifyOk) : IRepairAction
    {
        public string ActionId => actionId;
        public string TitleZh => actionId;
        public bool RequiresRestart => false;
        public bool BackupRequired => true;
        public int ExecuteCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecuteCalls++;
            return Task.FromResult(new RepairActionExecution("backup", "executed"));
        }

        public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
        {
            VerifyCalls++;
            return Task.FromResult(verifyOk);
        }

        public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
        {
            RollbackCalls++;
            return Task.CompletedTask;
        }
    }
}
