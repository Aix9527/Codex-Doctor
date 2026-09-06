using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811InstallGuiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = SourceRoot();
        var upgrade = File.ReadAllText(Path.Combine(root.FullName, "V811UiUpgrade.cs"));
        Require(upgrade.Contains("SwitchLanguageAsync"), "中文/English 按钮必须绑定真实双向切换行为。");
        Require(upgrade.Contains("DesktopUiLanguage.ChineseSimplified"), "中文按钮必须请求 zh-CN 切换。");
        Require(upgrade.Contains("DesktopUiLanguage.English"), "English 按钮必须请求 en-US 切换。");
        Require(upgrade.Contains("CodexInstallManagerForm"), "安装/卸载按钮必须打开独立安装管理窗口。");
        Require(!upgrade.Contains("chinese.Enabled = false"), "V8.1.1 中文按钮不能永久禁用。");
        Require(!upgrade.Contains("english.Enabled = false"), "V8.1.1 English 按钮不能永久禁用。");
        Require(!upgrade.Contains("install.Enabled = false"), "V8.1.1 安装/卸载按钮不能永久禁用。");

        var formPath = Path.Combine(root.FullName, "CodexInstallManagerForm.cs");
        Require(File.Exists(formPath), "缺少 CodexInstallManagerForm.cs。");
        var form = File.ReadAllText(formPath);
        foreach (var token in new[] { "安装 Desktop", "卸载 Desktop", "安装 CLI", "卸载 CLI" })
            Require(form.Contains(token), $"安装管理器缺少动作：{token}");
        Require(form.Contains("保留 .codex"), "Desktop/CLI 卸载确认必须明确保留 .codex。");
        Require(form.Contains("保留用户项目"), "卸载确认必须明确保留用户项目。");
        Require(form.Contains("重新扫描"), "安装/卸载完成后 GUI 必须重新扫描验证并刷新状态。");
    }
}
