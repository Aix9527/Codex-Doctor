using System.Runtime.CompilerServices;
using CodexDoctor.Native;

namespace CodexDoctor.Native.Tests;

internal static class TaskV812DesktopUninstallIdentityContract
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var empty = CodexDiscoveryResult.Empty();
        var chatGpt = empty with
        {
            DesktopClients = [new CodexDesktopInstallationInfo("ChatGPT Desktop", "进程发现", @"C:\Apps\ChatGPT.exe", "1.0", true, [1234])]
        };

        var runner = new FakeRunner();
        var manager = new CodexInstallManager(runner, new QueueDiscovery([chatGpt, empty]));
        var result = await manager.UninstallDesktopAsync();

        Require(result.Success && result.Verified, "V8.1.2 Desktop 卸载必须先识别当前真实 Desktop，再在卸载后重新扫描确认已消失。");
        Require(runner.Calls.Count == 1, "一次 Desktop 卸载只应执行一个经过当前扫描身份约束的卸载命令。");
        var call = runner.Calls[0];
        Require(call.FileName.Equals("winget.exe", StringComparison.OrdinalIgnoreCase), "Desktop 卸载必须继续使用 Windows 官方 winget，而不是直接删除目录。");
        Require(call.Arguments.Contains("--name \"ChatGPT\"", StringComparison.OrdinalIgnoreCase), "扫描到 ChatGPT.exe 时应按实际产品名 ChatGPT 卸载。");
        Require(call.Arguments.Contains("--exact", StringComparison.OrdinalIgnoreCase), "Desktop 卸载必须使用 exact 名称约束，避免误删相似应用。");
        Require(!call.Arguments.Contains(CodexInstallManager.DesktopStorePackageId, StringComparison.OrdinalIgnoreCase), "V8.1.2 卸载不得再硬编码单一 Microsoft Store package id。");
        Require(!call.Arguments.Contains("--source msstore", StringComparison.OrdinalIgnoreCase), "卸载不得强制限定 msstore 来源；winget 可卸载非 winget 安装的已注册应用。");

        var codex = empty with
        {
            DesktopClients = [new CodexDesktopInstallationInfo("Codex Desktop", "进程发现", @"D:\Tools\Codex.exe", "1.0", true, [4321])]
        };
        var codexRunner = new FakeRunner();
        var codexManager = new CodexInstallManager(codexRunner, new QueueDiscovery([codex, empty]));
        var codexResult = await codexManager.UninstallDesktopAsync();
        Require(codexResult.Success && codexResult.Verified, "独立 Codex Desktop 也必须支持动态卸载身份。");
        Require(codexRunner.Calls[0].Arguments.Contains("--name \"Codex\"", StringComparison.OrdinalIgnoreCase), "扫描到 Codex.exe 时应按 Codex 名称卸载。");

        var missingRunner = new FakeRunner();
        var missingManager = new CodexInstallManager(missingRunner, new QueueDiscovery([empty]));
        var missing = await missingManager.UninstallDesktopAsync();
        Require(missing.ManualRequired && !missing.Success, "重新扫描未发现 Desktop 时不得尝试猜测卸载目标。");
        Require(missingRunner.Calls.Count == 0, "未识别真实 Desktop 时不得执行 winget uninstall。");
    }

    private sealed class FakeRunner : IInstallCommandRunner
    {
        public List<InstallCommandCall> Calls { get; } = [];
        public bool IsAvailable(string fileName) => true;

        public Task<InstallCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls.Add(new InstallCommandCall(fileName, arguments));
            return Task.FromResult(new InstallCommandResult(0, "ok", "", false));
        }
    }

    private sealed class QueueDiscovery(IEnumerable<CodexDiscoveryResult> results) : IInstallDiscoveryProvider
    {
        private readonly Queue<CodexDiscoveryResult> _results = new(results);
        public Task<CodexDiscoveryResult> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.Count > 0 ? _results.Dequeue() : CodexDiscoveryResult.Empty());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
