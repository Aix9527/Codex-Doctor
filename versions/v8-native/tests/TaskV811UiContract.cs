using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811UiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = SourceRoot();
        var project = File.ReadAllText(Path.Combine(root.FullName, "CodexDoctor.Native.csproj"));
        Require(project.Contains("<Version>8.1.1</Version>"), "V8.1.1 项目版本必须为 8.1.1。");

        var program = File.ReadAllText(Path.Combine(root.FullName, "Program.cs"));
        Require(program.Contains("V811UiUpgrade.Apply"), "启动主窗体前必须应用 V8.1.1 UI 升级。");

        var upgradePath = Path.Combine(root.FullName, "V811UiUpgrade.cs");
        Require(File.Exists(upgradePath), "缺少 V811UiUpgrade.cs。");
        var upgrade = File.ReadAllText(upgradePath);
        Require(upgrade.Contains("\"中文\""), "主界面必须有固定“中文”按钮。");
        Require(upgrade.Contains("\"English\""), "主界面必须有固定“English”按钮。");
        Require(upgrade.Contains("安装 / 卸载"), "主界面必须有“安装 / 卸载”入口。");
        Require(upgrade.Contains("Controls.Remove"), "V8.1.1 必须移除旧的单一语言按钮，避免继续显示“需要用户操作”。");
    }
}
