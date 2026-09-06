using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class Task5GuiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");

        var mainPath = Path.Combine(sourceRoot.FullName, "MainForm.cs");
        var discoveryFormPath = Path.Combine(sourceRoot.FullName, "CodexDiscoveryForm.cs");
        var repairServicePath = Path.Combine(sourceRoot.FullName, "RepairService.cs");
        Require(File.Exists(discoveryFormPath), "必须保留 CodexDiscoveryForm.cs 详细扫描页。");

        var all = File.ReadAllText(mainPath) + "\n" + File.ReadAllText(discoveryFormPath);
        foreach (var phrase in new[]
        {
            "一键扫描 Codex",
            "恢复原语言",
            "当前界面语言",
            "回答语言偏好",
            "CLI 输出偏好",
            "打开安装目录",
            "打开 .codex 目录",
            "复制扫描摘要",
            "导出扫描报告"
        })
            Require(all.Contains(phrase), $"GUI 缺少功能文案：{phrase}");

        var main = File.ReadAllText(mainPath);
        Require(main.Contains("_lastScan"), "V8.1 MainForm 必须以统一健康扫描结果作为最近状态事实源。");
        Require(main.Contains("RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds"), "重启必须复用扫描到的 Desktop 真实路径和 PID。");

        var repairService = File.ReadAllText(repairServicePath);
        Require(repairService.Contains("RunCodexDoctor(string cliPath)"), "必须继续保留基于显式 CLI 路径的 codex doctor 能力。");
        Require(repairService.Contains("return RunCodexDoctor(discovery.Cli.Path)"), "doctor 自动入口必须复用扫描到的 CLI 真实路径。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
