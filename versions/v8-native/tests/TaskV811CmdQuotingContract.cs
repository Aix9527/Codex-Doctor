using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811CmdQuotingContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "Codex Doctor V811 cmd quoting");
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "npm probe.cmd");
        File.WriteAllText(script, "@echo off\r\necho ARG1=%1\r\necho ARG2=%2\r\nexit /b 0\r\n");

        try
        {
            var runner = new InstallCommandRunner();
            var result = runner.RunAsync(script, "alpha beta", TimeSpan.FromSeconds(10), CancellationToken.None)
                .GetAwaiter().GetResult();

            Require(!result.TimedOut, "带空格路径的 .cmd 执行不应超时。");
            Require(result.ExitCode == 0, $"带空格路径的 .cmd 必须通过 cmd.exe 正确执行，实际退出码 {result.ExitCode}，stderr={result.StandardError}");
            Require(result.StandardOutput.Contains("ARG1=alpha", StringComparison.OrdinalIgnoreCase), "第一个参数必须完整传给 .cmd。");
            Require(result.StandardOutput.Contains("ARG2=beta", StringComparison.OrdinalIgnoreCase), "第二个参数必须完整传给 .cmd。");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
