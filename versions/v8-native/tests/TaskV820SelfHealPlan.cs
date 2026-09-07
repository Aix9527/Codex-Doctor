using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820SelfHealPlan
{
    [ModuleInitializer]
    internal static void Run()
    {
        var proxyRepair = Issue("proxy-env", "codex.proxy.env", requiresRestart: true);
        var gitRepair = Issue("git-conflict", "git.proxy.clear");
        var npmRepair = Issue("npm-conflict", "npm.proxy.clear");
        var unsafeRepair = Issue("unsafe", "windows.user.proxy.write");

        var directHealthy = Scan(
            directOk: true,
            proxyOk: true,
            dnsOk: true,
            issues: [proxyRepair]);
        var directPlan = ReconnectingSelfHealPlan.Build(directHealthy);
        Require(!directPlan.ActionIds.Contains("codex.proxy.env"), "直连 TLS 正常时不得为了自愈写入 Codex 代理 .env。");

        var proxyNeeded = Scan(
            directOk: false,
            proxyOk: true,
            dnsOk: true,
            issues: [proxyRepair],
            desktop: true);
        var proxyPlan = ReconnectingSelfHealPlan.Build(proxyNeeded);
        Require(proxyPlan.ActionIds.SequenceEqual(["codex.proxy.env", "codex.desktop.restart"]), "已验证代理 + proxy env 修复问题时应写 .env，并把 Desktop 重启放最后。");

        var withGit = Scan(
            directOk: false,
            proxyOk: true,
            dnsOk: true,
            issues: [proxyRepair, gitRepair],
            desktop: true);
        var gitPlan = ReconnectingSelfHealPlan.Build(withGit);
        Require(gitPlan.ActionIds.SequenceEqual(["codex.proxy.env", "git.proxy.clear", "codex.desktop.restart"]), "Git 冲突只能增加 git.proxy.clear，且 Desktop 重启必须最后。");

        var withNpm = Scan(
            directOk: false,
            proxyOk: true,
            dnsOk: true,
            issues: [proxyRepair, npmRepair],
            desktop: true);
        var npmPlan = ReconnectingSelfHealPlan.Build(withNpm);
        Require(npmPlan.ActionIds.SequenceEqual(["codex.proxy.env", "npm.proxy.clear", "codex.desktop.restart"]), "npm 冲突只能增加 npm.proxy.clear，且 Desktop 重启必须最后。");

        var cleanupOnly = Scan(
            directOk: true,
            proxyOk: false,
            dnsOk: true,
            issues: [gitRepair, npmRepair]);
        var cleanupPlan = ReconnectingSelfHealPlan.Build(cleanupOnly);
        Require(cleanupPlan.ActionIds.SequenceEqual(["git.proxy.clear", "npm.proxy.clear"]), "直连正常时仍可清理明确冲突，但不得添加代理写入或无依据重启。");

        var unknown = new CodexHealthScanResult(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [unsafeRepair],
            CodexDiscoveryResult.Empty(),
            null);
        var unknownPlan = ReconnectingSelfHealPlan.Build(unknown);
        Require(unknownPlan.ActionIds.Count == 0, "未知/人工网络状态不得执行任意或非白名单动作。");
        Require(!unknownPlan.ActionIds.Contains("windows.user.proxy.write"), "Reconnecting 自愈计划不得写 Windows 用户级代理。");

        var proxyNotVerified = Scan(
            directOk: false,
            proxyOk: false,
            dnsOk: true,
            issues: [proxyRepair]);
        var unverifiedPlan = ReconnectingSelfHealPlan.Build(proxyNotVerified);
        Require(!unverifiedPlan.ActionIds.Contains("codex.proxy.env"), "代理 HTTPS 未验证成功时绝不能写入 .codex/.env。");

        foreach (var id in proxyPlan.ActionIds.Concat(gitPlan.ActionIds).Concat(npmPlan.ActionIds).Concat(cleanupPlan.ActionIds))
            Require(ReconnectingSelfHealPlan.AllowedActionIds.Contains(id), $"计划出现非白名单动作：{id}");
    }

    private static CodexHealthScanResult Scan(
        bool directOk,
        bool proxyOk,
        bool dnsOk,
        IReadOnlyList<CodexIssue> issues,
        bool desktop = false)
    {
        var discovery = desktop
            ? CodexDiscoveryResult.Empty() with
            {
                DesktopClients = [new CodexDesktopInstallationInfo("Codex Desktop", "test", @"C:\Apps\Codex.exe", "8.2.0", true, [4321])]
            }
            : CodexDiscoveryResult.Empty();

        var diagnosis = new DiagnosisResult(
            "8.2.0",
            directOk || proxyOk ? HealthState.Warning : HealthState.Error,
            directOk ? FailureClass.Healthy : proxyOk ? FailureClass.ProxyRequired : FailureClass.TlsFailure,
            "test",
            "test",
            new ProbeResult(dnsOk, dnsOk ? null : "DNS failed"),
            new ProbeResult(directOk, directOk ? null : "TLS failed"),
            new ProbeResult(proxyOk, proxyOk ? null : "Proxy failed", proxyOk ? 403 : null),
            proxyOk ? "http://127.0.0.1:7897" : string.Empty,
            new ProxyEnvironmentState(false, string.Empty, string.Empty),
            new ConflictState(string.Empty, string.Empty, issues.Any(x => x.RepairActionId == "git.proxy.clear")),
            new ConflictState(string.Empty, string.Empty, issues.Any(x => x.RepairActionId == "npm.proxy.clear")),
            new TunState(false, false, [], []),
            desktop ? 1 : 0);

        return new CodexHealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, issues, discovery, diagnosis);
    }

    private static CodexIssue Issue(string id, string actionId, bool requiresRestart = false) =>
        new(
            id,
            "test",
            CodexIssueSeverity.Warning,
            id,
            id,
            id,
            id,
            CodexIssueStatus.Repairable,
            true,
            actionId,
            true,
            requiresRestart,
            false,
            true,
            actionId + ".verify",
            id);
}
