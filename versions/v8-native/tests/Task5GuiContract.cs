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
        Require(File.Exists(discoveryFormPath), "必须新增 CodexDiscoveryForm.cs。");

        var all = File.ReadAllText(mainPath) + "\n" + File.ReadAllText(discoveryFormPath);
        foreach (var phrase in new[]
        {
            "扫描本机 Codex",
            "一键设置 Codex 为简体中文",
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
        Require(main.Contains("_lastDiscovery"), "MainForm 必须保存最近一次 Codex 扫描结果。");
        Require(main.Contains("RunCodexDoctor(_lastDiscovery.Cli.Path"), "运行 doctor 必须复用扫描到的 CLI 真实路径。");
        Require(main.Contains("RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds"), "重启必须复用扫描到的 Desktop 真实路径和 PID。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
