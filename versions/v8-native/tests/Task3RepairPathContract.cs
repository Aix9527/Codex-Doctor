using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class Task3RepairPathContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");

        var source = File.ReadAllText(Path.Combine(sourceRoot.FullName, "RepairService.cs"));
        Require(source.Contains("RunCodexDoctor(string cliPath)"), "RepairService 必须通过显式 CLI 路径运行 codex doctor。");
        Require(source.Contains("RestartCodexDesktop(string executablePath"), "RepairService 必须通过显式 Desktop 路径重启 Codex。");
        Require(!source.Contains("Run(\"codex\", \"doctor\""), "不得继续硬编码裸 codex 命令。");
        Require(!source.Contains("ProcessStartInfo(\"chatgpt:\""), "不得继续依赖 chatgpt: URL 协议重启 Desktop。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
