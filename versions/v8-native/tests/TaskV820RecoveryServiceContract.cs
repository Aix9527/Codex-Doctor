using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820RecoveryServiceContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyRecoveredRequiresNetworkAndDesktop().GetAwaiter().GetResult();
        VerifyNetworkOnlyNeverReportsRecovered().GetAwaiter().GetResult();
        VerifyMissingDesktopRequiresManualAction().GetAwaiter().GetResult();
        VerifyRestartFailureIsNotRecovered().GetAwaiter().GetResult();
    }

    private static async Task VerifyRecoveredRequiresNetworkAndDesktop()
    {
        var before = Scan(Desktop(running: true), Diagnosis(directTlsOk: false, proxyOk: true));
        var after = Scan(Desktop(running: true), Diagnosis(directTlsOk: true, proxyOk: false));
        var scanner = new QueueScanner(after);
        var repair = new FakeRepairExecutor();
        var restart = new FakeRestartBackend(runningAfterRestart: true);
        var discovery = new QueueDiscovery(Desktop(running: true));
        var service = new ReconnectingRecoveryService(scanner, repair, restart, discovery.ScanAsync);

        var result = await service.RecoverAsync(before);

        Require(result.Status == ReconnectingRecoveryStatus.Recovered, "网络与 Desktop 双验证通过时必须返回 RECOVERED。");
        Require(result.NetworkVerified && result.DesktopVerified, "RECOVERED 必须同时标记网络与 Desktop 已验证。");
        Require(result.AfterScanId == after.ScanId, "结果必须记录 fresh after ScanId。");
        Require(restart.RestartCalls == 1, "运行中的 Desktop 执行专项自愈时必须通过扫描确认的 EXE/PID 重启一次。");
        Require(scanner.Calls == 1, "自愈后必须执行一次 fresh health scan。");
        Require(discovery.Calls == 1, "自愈后必须执行 fresh Desktop discovery。");
    }

    private static async Task VerifyNetworkOnlyNeverReportsRecovered()
    {
        var before = Scan(Desktop(running: true), Diagnosis(directTlsOk: false, proxyOk: true));
        var after = Scan(Desktop(running: false), Diagnosis(directTlsOk: true, proxyOk: false));
        var service = new ReconnectingRecoveryService(
            new QueueScanner(after),
            new FakeRepairExecutor(),
            new FakeRestartBackend(runningAfterRestart: false),
            new QueueDiscovery(Desktop(running: false)).ScanAsync);

        var result = await service.RecoverAsync(before);

        Require(result.Status == ReconnectingRecoveryStatus.NetworkRecovered, "网络恢复但 Desktop 未验证时只能返回 NETWORK_RECOVERED。");
        Require(result.NetworkVerified && !result.DesktopVerified, "NETWORK_RECOVERED 必须保留 Desktop 未验证事实。");
    }

    private static async Task VerifyMissingDesktopRequiresManualAction()
    {
        var before = Scan(CodexDiscoveryResult.Empty(), Diagnosis(directTlsOk: false, proxyOk: true));
        var restart = new FakeRestartBackend(true);
        var service = new ReconnectingRecoveryService(
            new QueueScanner(before),
            new FakeRepairExecutor(),
            restart,
            new QueueDiscovery(CodexDiscoveryResult.Empty()).ScanAsync);

        var result = await service.RecoverAsync(before);

        Require(result.Status == ReconnectingRecoveryStatus.ManualRequired, "没有可信 Desktop 时必须返回 MANUAL_REQUIRED。");
        Require(restart.RestartCalls == 0, "没有可信 Desktop 时不得尝试重启未知程序。");
    }

    private static async Task VerifyRestartFailureIsNotRecovered()
    {
        var before = Scan(Desktop(running: true), Diagnosis(directTlsOk: true, proxyOk: false));
        var after = Scan(Desktop(running: false), Diagnosis(directTlsOk: true, proxyOk: false));
        var service = new ReconnectingRecoveryService(
            new QueueScanner(after),
            new FakeRepairExecutor(),
            new FakeRestartBackend(runningAfterRestart: false, throwOnRestart: true),
            new QueueDiscovery(Desktop(running: false)).ScanAsync);

        var result = await service.RecoverAsync(before);

        Require(result.Status is ReconnectingRecoveryStatus.DesktopRestartFailed or ReconnectingRecoveryStatus.NetworkRecovered,
            "Desktop 重启失败时不得误报 RECOVERED。");
    }

    private static CodexHealthScanResult Scan(CodexDiscoveryResult discovery, DiagnosisResult diagnosis) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, [], discovery, diagnosis);

    private static CodexDiscoveryResult Desktop(bool running)
    {
        var exe = @"C:\Apps\Codex.exe";
        return new CodexDiscoveryResult(
            [new CodexDesktopInstallationInfo("Codex Desktop", "test", exe, "1.0", running, running ? [1234] : [])],
            new CodexCliInfo(false, null, false, null, false),
            new CodexDataDirectoryInfo(@"C:\Users\X\.codex", true, false, null, 0, 0),
            [],
            new CodexLanguageState("未知", "未知", "未知", false, true, "test"));
    }

    private static DiagnosisResult Diagnosis(bool directTlsOk, bool proxyOk) =>
        new(
            "8.2.0-test",
            directTlsOk ? HealthState.Healthy : HealthState.Warning,
            directTlsOk ? FailureClass.Healthy : FailureClass.ProxyRequired,
            "test",
            "test",
            new ProbeResult(true),
            new ProbeResult(directTlsOk),
            new ProbeResult(proxyOk, StatusCode: proxyOk ? 403 : null),
            proxyOk ? "http://127.0.0.1:7897" : string.Empty,
            new ProxyEnvironmentState(false, string.Empty, string.Empty),
            new ConflictState(string.Empty, string.Empty, false),
            new ConflictState(string.Empty, string.Empty, false),
            new TunState(false, false, [], []),
            1);

    private sealed class QueueScanner : IHealthScanner
    {
        private readonly CodexHealthScanResult _result;
        public int Calls { get; private set; }
        public QueueScanner(CodexHealthScanResult result) => _result = result;
        public Task<CodexHealthScanResult> ScanAsync(IProgress<HealthScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class QueueDiscovery
    {
        private readonly CodexDiscoveryResult _result;
        public int Calls { get; private set; }
        public QueueDiscovery(CodexDiscoveryResult result) => _result = result;
        public Task<CodexDiscoveryResult> ScanAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeRepairExecutor : IReconnectingRepairExecutor
    {
        public RepairPlan BuildPlan(CodexHealthScanResult scan) =>
            new(Guid.NewGuid(), DateTimeOffset.UtcNow, scan.ScanId, [], false, [], []);

        public Task<RepairSessionResult> ExecuteAsync(RepairPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepairSessionResult(plan.PlanId, []));
    }

    private sealed class FakeRestartBackend : IDesktopRestartBackend
    {
        private readonly bool _runningAfterRestart;
        private readonly bool _throwOnRestart;
        public int RestartCalls { get; private set; }
        public FakeRestartBackend(bool runningAfterRestart, bool throwOnRestart = false)
        {
            _runningAfterRestart = runningAfterRestart;
            _throwOnRestart = throwOnRestart;
        }
        public void Restart(string executablePath, IReadOnlyCollection<int> processIds)
        {
            RestartCalls++;
            if (_throwOnRestart) throw new InvalidOperationException("restart failed");
        }
        public bool IsRunning(string executablePath) => _runningAfterRestart;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
