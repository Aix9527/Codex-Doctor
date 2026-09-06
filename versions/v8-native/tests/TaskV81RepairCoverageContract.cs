using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81RepairCoverageContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var temp = Path.Combine(Path.GetTempPath(), "CodexDoctorV81Actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var gitBackend = new FakeProxyBackend(new ToolProxySettings("http://old:1", "http://old:1"));
        var git = new GitProxyRepairAction(gitBackend, temp);
        var gitExec = await git.ExecuteAsync(CancellationToken.None);
        Require(gitBackend.Current.IsEmpty, "Git 修复动作必须清理冲突代理。");
        Require(await git.VerifyAsync(gitExec, CancellationToken.None), "Git 修复动作必须可验证。");
        await git.RollbackAsync(gitExec, CancellationToken.None);
        Require(gitBackend.Current.HttpProxy == "http://old:1", "Git 修复回滚必须恢复旧代理。");

        var npmBackend = new FakeProxyBackend(new ToolProxySettings("http://old:2", "http://old:2"));
        var npm = new NpmProxyRepairAction(npmBackend, temp);
        var npmExec = await npm.ExecuteAsync(CancellationToken.None);
        Require(npmBackend.Current.IsEmpty, "npm 修复动作必须清理冲突代理。");
        await npm.RollbackAsync(npmExec, CancellationToken.None);
        Require(npmBackend.Current.HttpProxy == "http://old:2", "npm 修复回滚必须恢复旧代理。");

        var restartBackend = new FakeRestartBackend();
        var restart = new CodexRestartRepairAction(restartBackend, @"C:\Apps\Codex.exe", [123]);
        var restartExec = await restart.ExecuteAsync(CancellationToken.None);
        Require(restartBackend.RestartCalls == 1, "Codex 重启动作必须通过已确认路径执行一次。");
        Require(await restart.VerifyAsync(restartExec, CancellationToken.None), "Codex 重启动作必须验证启动结果。");

        var languageDir = Path.Combine(temp, "language");
        Directory.CreateDirectory(languageDir);
        var config = Path.Combine(languageDir, "ui.toml");
        File.WriteAllText(config, "language=en-US\n");
        var backup = Path.Combine(languageDir, "backup.json");
        var languageService = new CodexLanguageService(backup, [config]);
        var languageDiscovery = new CodexDiscoveryResult(
            [],
            new CodexCliInfo(false, null, false, null, false),
            new CodexDataDirectoryInfo(languageDir, true, false, null, 1, new FileInfo(config).Length),
            [CodexDiscoveryService.ScanConfigFile(config)],
            new CodexLanguageState("未知", "未知", "未知", false, true, "尚未检测"));
        var language = new LanguageRepairAction(languageService, languageDiscovery);
        var langExec = await language.ExecuteAsync(CancellationToken.None);
        Require(await language.VerifyAsync(langExec, CancellationToken.None), "可信语言动作必须验证 zh-CN 写入。");
        await language.RollbackAsync(langExec, CancellationToken.None);
        Require(File.ReadAllText(config).Contains("en-US"), "语言回滚必须恢复原语言。");

        Directory.Delete(temp, true);
    }

    private sealed class FakeProxyBackend(ToolProxySettings initial) : IProxyToolRepairBackend
    {
        public ToolProxySettings Current { get; private set; } = initial;
        public ToolProxySettings Capture() => Current;
        public void Clear() => Current = ToolProxySettings.Empty;
        public void Restore(ToolProxySettings state) => Current = state;
    }

    private sealed class FakeRestartBackend : IDesktopRestartBackend
    {
        public int RestartCalls { get; private set; }
        public void Restart(string executablePath, IReadOnlyCollection<int> processIds) => RestartCalls++;
        public bool IsRunning(string executablePath) => RestartCalls > 0;
    }
}
