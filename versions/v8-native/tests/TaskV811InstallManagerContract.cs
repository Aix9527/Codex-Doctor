using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811InstallManagerContract
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var empty = CodexDiscoveryResult.Empty();
        var desktopFound = empty with
        {
            DesktopClients = [new CodexDesktopInstallationInfo("ChatGPT", "msstore", @"C:\Apps\ChatGPT.exe", "1.0", false, [])]
        };
        var cliFound = empty with
        {
            Cli = new CodexCliInfo(true, @"C:\Users\X\AppData\Roaming\npm\codex.cmd", true, "1.2.3", true)
        };

        var runner = new FakeRunner();
        var discovery = new QueueDiscovery([desktopFound, desktopFound, empty, cliFound, empty]);
        var manager = new CodexInstallManager(runner, discovery);

        var desktopInstall = await manager.InstallDesktopAsync();
        Require(desktopInstall.Success && desktopInstall.Verified, "Desktop 安装后必须重新发现 Desktop 才能报告成功。");
        Require(runner.Calls[0].FileName.Equals("winget.exe", StringComparison.OrdinalIgnoreCase), "Desktop 安装必须使用 winget.exe。");
        Require(runner.Calls[0].Arguments.Contains("9NT1R1C2HH7J", StringComparison.Ordinal), "Desktop 安装必须使用官方 Store package id 9NT1R1C2HH7J。");
        Require(runner.Calls[0].Arguments.Contains("--source msstore", StringComparison.OrdinalIgnoreCase), "Desktop 安装必须限定 Microsoft Store 来源。");

        var desktopUninstall = await manager.UninstallDesktopAsync();
        Require(desktopUninstall.Success && desktopUninstall.Verified, "Desktop 卸载后必须确认 Desktop 已不再被发现。");
        Require(runner.Calls[1].Arguments.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase), "Desktop 卸载必须使用 winget uninstall，而不是直接删除应用目录。");

        var cliInstall = await manager.InstallCliAsync();
        Require(cliInstall.Success && cliInstall.Verified, "CLI 安装必须重新发现并验证 codex --version 后才能成功。");
        Require(runner.Calls.Any(x => x.FileName.Equals("npm.cmd", StringComparison.OrdinalIgnoreCase) && x.Arguments == "install -g @openai/codex"), "CLI 安装命令必须固定为 npm.cmd install -g @openai/codex。");
        Require(runner.Calls.Any(x => x.Arguments == "--version"), "CLI 安装后必须运行 codex --version 验证。");

        var cliUninstall = await manager.UninstallCliAsync();
        Require(cliUninstall.Success && cliUninstall.Verified, "CLI 卸载后必须确认 CLI 不再被发现。");
        Require(runner.Calls.Any(x => x.FileName.Equals("npm.cmd", StringComparison.OrdinalIgnoreCase) && x.Arguments == "uninstall -g @openai/codex"), "CLI 卸载命令必须固定为 npm.cmd uninstall -g @openai/codex。");

        var missingWinget = new FakeRunner(wingetAvailable: false, npmAvailable: true);
        var noWinget = new CodexInstallManager(missingWinget, new QueueDiscovery([empty]));
        var noWingetResult = await noWinget.InstallDesktopAsync();
        Require(noWingetResult.ManualRequired && !noWingetResult.Success, "winget 缺失时必须标记 ManualRequired，不能伪报安装成功。");
        Require(missingWinget.Calls.Count == 0, "winget 缺失时不应执行安装命令。");

        var missingNpm = new FakeRunner(wingetAvailable: true, npmAvailable: false);
        var noNpm = new CodexInstallManager(missingNpm, new QueueDiscovery([empty]));
        var noNpmResult = await noNpm.InstallCliAsync();
        Require(noNpmResult.ManualRequired && !noNpmResult.Success, "npm 缺失时必须标记 ManualRequired，且不得自动安装 Node.js。");
        Require(missingNpm.Calls.Count == 0, "npm 缺失时不应执行第三方或 Node 安装命令。");

        var source = File.ReadAllText(Path.Combine(SourceRoot().FullName, "CodexInstallManager.cs"));
        Require(!source.Contains("Directory.Delete", StringComparison.Ordinal), "安装管理器不得直接删除 Desktop 或 .codex 目录。");
        Require(!source.Contains(".codex", StringComparison.OrdinalIgnoreCase), "程序卸载逻辑不得把 .codex 当作删除目标。");
    }

    private sealed class FakeRunner(bool wingetAvailable = true, bool npmAvailable = true) : IInstallCommandRunner
    {
        public List<InstallCommandCall> Calls { get; } = [];
        public bool IsAvailable(string fileName) => fileName.Equals("winget.exe", StringComparison.OrdinalIgnoreCase) ? wingetAvailable :
            fileName.Equals("npm.cmd", StringComparison.OrdinalIgnoreCase) ? npmAvailable : true;

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
}
