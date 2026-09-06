using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811UiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = SourceRoot();

        // V8.1.1 引入的 UI 能力是后续版本必须继续保留的兼容合同，
        // 不再把当前产品版本永久锁死在 8.1.1。
        var program = File.ReadAllText(Path.Combine(root.FullName, "Program.cs"));
        Require(program.Contains("V811UiUpgrade.Apply"), "后续版本启动主窗体前仍必须应用 V8.1.1 引入的 UI 升级层。");

        var upgradePath = Path.Combine(root.FullName, "V811UiUpgrade.cs");
        Require(File.Exists(upgradePath), "缺少 V811UiUpgrade.cs。");
        var upgrade = File.ReadAllText(upgradePath);
        Require(upgrade.Contains("\"中文\""), "主界面必须保留固定“中文”按钮。");
        Require(upgrade.Contains("\"English\""), "主界面必须保留固定“English”按钮。");
        Require(upgrade.Contains("安装 / 卸载"), "主界面必须保留“安装 / 卸载”入口。");
        Require(upgrade.Contains("Controls.Remove"), "必须继续移除旧的单一语言按钮，避免重复显示过时入口。");
    }
}
