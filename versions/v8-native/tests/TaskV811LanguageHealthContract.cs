using System.Reflection;
using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811LanguageHealthContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var method = typeof(CodexHealthScanner).GetMethod(
            "CheckLanguage",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("无法定位 CheckLanguage 行为入口。");

        var desktop = new CodexDesktopInstallationInfo(
            "ChatGPT Desktop",
            "test",
            @"C:\Program Files\ChatGPT\ChatGPT.exe",
            "1.0.0",
            true,
            [1234]);

        var withDesktop = CodexDiscoveryResult.Empty() with
        {
            DesktopClients = [desktop]
        };

        var service = new CodexLanguageService();
        var rawWithDesktop = (CodexIssue?)method.Invoke(null, [service, withDesktop])
            ?? throw new Exception("CheckLanguage 未返回问题状态。");

        var switchable = CodexIssueClassifier.SortIssues([
            CodexIssue.ForTest("desktop-install", CodexIssueSeverity.Ok),
            rawWithDesktop
        ]).Single(x => x.Category == "language");

        Require(switchable.Severity == CodexIssueSeverity.Info, "未验证的语言切换能力只能是提示级别。 ");
        Require(switchable.Status != CodexIssueStatus.ManualRequired, "已发现 Desktop 时不得继续显示为只能人工处理。 ");
        Require(switchable.TitleZh.Contains("界面语言", StringComparison.Ordinal), "语言健康项标题应描述界面语言状态。 ");
        Require(switchable.SummaryZh.Contains("中文 / English", StringComparison.Ordinal), "语言健康项必须指向 V8.1.1 双向语言入口。 ");
        Require(switchable.SummaryZh.Contains("验证", StringComparison.Ordinal), "语言健康项必须明确切换后需要验证。 ");
        Require(!switchable.AutoRepairable, "UI Automation 尝试不得伪装成一键修复白名单动作。 ");

        var rawWithoutDesktop = (CodexIssue?)method.Invoke(null, [service, CodexDiscoveryResult.Empty()])
            ?? throw new Exception("CheckLanguage 未返回无 Desktop 状态。");
        var withoutDesktop = CodexIssueClassifier.SortIssues([
            CodexIssue.ForTest("desktop-missing", CodexIssueSeverity.Critical),
            rawWithoutDesktop
        ]).Single(x => x.Category == "language");

        Require(withoutDesktop.Status == CodexIssueStatus.NotApplicable, "未发现 Desktop 时语言切换应标记为不适用。 ");
        Require(withoutDesktop.TitleZh.Contains("未发现 Desktop", StringComparison.Ordinal), "未发现 Desktop 时必须给出准确原因。 ");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
