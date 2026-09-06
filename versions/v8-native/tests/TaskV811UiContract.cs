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

        var main = File.ReadAllText(Path.Combine(root.FullName, "MainForm.cs"));
        Require(main.Contains("\"中文\""), "主界面必须有固定“中文”按钮。");
        Require(main.Contains("\"English\""), "主界面必须有固定“English”按钮。");
        Require(main.Contains("安装 / 卸载"), "主界面必须有“安装 / 卸载”入口。");
        Require(!main.Contains("_languageButton = AddActionButton"), "V8.1.1 不应继续使用单一语言按钮。");
    }
}
