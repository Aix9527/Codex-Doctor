using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV812DesktopWindowBindingContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var desktop = new CodexDesktopInstallationInfo(
            "ChatGPT Desktop",
            "进程发现",
            @"C:\Apps\ChatGPT\ChatGPT.exe",
            "1.0",
            true,
            [100]);

        var exact = DesktopWindowBindingSelector.Select(
            desktop,
            [new DesktopWindowCandidate((nint)0x101, 100, true)],
            [new DesktopProcessSnapshot(100, null, @"C:\Apps\ChatGPT\ChatGPT.exe")]);
        Require(exact.Found && exact.Handle == (nint)0x101 && exact.Method == DesktopWindowBindingMethod.ExactScannedPid,
            "扫描 PID 自己拥有可见顶层窗口时必须优先精确绑定。");

        var child = DesktopWindowBindingSelector.Select(
            desktop,
            [new DesktopWindowCandidate((nint)0x202, 200, true)],
            [
                new DesktopProcessSnapshot(100, null, @"C:\Apps\ChatGPT\ChatGPT.exe"),
                new DesktopProcessSnapshot(200, 100, @"C:\Apps\ChatGPT\renderer.exe")
            ]);
        Require(child.Found && child.Handle == (nint)0x202 && child.Method == DesktopWindowBindingMethod.ProcessTree,
            "V8.1.2 必须能绑定扫描 Desktop 的子进程顶层窗口，不能只依赖 Process.MainWindowHandle。");

        var sameExe = DesktopWindowBindingSelector.Select(
            desktop,
            [new DesktopWindowCandidate((nint)0x303, 300, true)],
            [
                new DesktopProcessSnapshot(100, null, @"C:\Apps\ChatGPT\ChatGPT.exe"),
                new DesktopProcessSnapshot(300, null, @"C:\Apps\ChatGPT\ChatGPT.exe")
            ]);
        Require(sameExe.Found && sameExe.Method == DesktopWindowBindingMethod.ExactExecutablePath,
            "多进程客户端中，同一已确认 EXE 路径的可见顶层窗口必须可作为安全回退。");

        var unrelated = DesktopWindowBindingSelector.Select(
            desktop,
            [new DesktopWindowCandidate((nint)0x404, 999, true)],
            [
                new DesktopProcessSnapshot(100, null, @"C:\Apps\ChatGPT\ChatGPT.exe"),
                new DesktopProcessSnapshot(999, null, @"C:\Windows\notepad.exe")
            ]);
        Require(!unrelated.Found,
            "与扫描 PID 无亲缘关系且 EXE 路径不同的窗口必须拒绝，不能只按标题猜测。");

        var hidden = DesktopWindowBindingSelector.Select(
            desktop,
            [new DesktopWindowCandidate((nint)0x505, 100, false)],
            [new DesktopProcessSnapshot(100, null, @"C:\Apps\ChatGPT\ChatGPT.exe")]);
        Require(!hidden.Found, "不可见窗口不得作为语言 UI Automation 主窗口。");

        var source = File.ReadAllText(Path.Combine(SourceRoot().FullName, "DesktopLanguageSwitchService.cs"));
        Require(source.Contains("IDesktopWindowLocator", StringComparison.Ordinal),
            "Windows UI Automation 语言后端必须通过可审计的 Desktop 窗口定位器绑定 HWND。");
        Require(!source.Contains("process.MainWindowHandle", StringComparison.Ordinal),
            "V8.1.2 语言后端不得继续只依赖 Process.MainWindowHandle。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
