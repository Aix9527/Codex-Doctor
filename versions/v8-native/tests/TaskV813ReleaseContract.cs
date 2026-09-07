using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CodexDoctor.Native.Tests;

internal static class TaskV813ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj")))
            sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");
        var repoRoot = sourceRoot.Parent!.Parent!;

        var csproj = File.ReadAllText(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"));
        Require(Regex.IsMatch(csproj, @"<Version>8\.1\.3</Version>"), "V8.1.3 发布前项目版本必须固定为 8.1.3。");

        var ui = File.ReadAllText(Path.Combine(sourceRoot.FullName, "V811UiUpgrade.cs"));
        Require(ui.Contains("Codex Doctor V8.1.3 原生维修中心", StringComparison.Ordinal), "主窗口标题必须显示 V8.1.3。");
        Require(ui.Contains("Codex Doctor V8.1.3", StringComparison.Ordinal), "主界面版本标签必须显示 V8.1.3。");
        Require(ui.Contains("\"修复重连\"", StringComparison.Ordinal), "V8.1.3 主界面必须保留修复重连按钮。");

        var about = File.ReadAllText(Path.Combine(sourceRoot.FullName, "AboutForm.cs"));
        Require(about.Contains("版本：8.1.3", StringComparison.Ordinal), "关于窗口必须显示版本 8.1.3。");

        var report = File.ReadAllText(Path.Combine(sourceRoot.FullName, "HealthReportExporter.cs"));
        Require(report.Contains("版本 = \"8.1.3\"", StringComparison.Ordinal), "完整报告内部版本必须是 8.1.3。");
        Require(report.Contains("CodexDoctor-V8.1-Report-", StringComparison.Ordinal), "报告默认文件名必须继续保持用户批准的 V8.1 命名族。");

        var readme = File.ReadAllText(Path.Combine(repoRoot.FullName, "README.md"));
        Require(readme.Contains("当前推荐版本：V8.1.3", StringComparison.Ordinal), "README 当前推荐版本必须是 V8.1.3。");
        Require(readme.Contains("修复重连", StringComparison.Ordinal), "README 必须说明修复重连能力。");
        Require(readme.Contains("中文", StringComparison.Ordinal) && readme.Contains("English", StringComparison.Ordinal), "README 必须保留双向语言切换说明。");

        var changelog = File.ReadAllText(Path.Combine(repoRoot.FullName, "CHANGELOG.md"));
        var v813 = changelog.IndexOf("## V8.1.3", StringComparison.Ordinal);
        var v812 = changelog.IndexOf("## V8.1.2", StringComparison.Ordinal);
        Require(v813 >= 0 && v812 >= 0 && v813 < v812, "CHANGELOG 必须把 V8.1.3 放在 V8.1.2 之前。");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.1.3.md");
        Require(File.Exists(notesPath), "必须新增 RELEASE_NOTES_V8.1.3.md。");
        var notes = File.ReadAllText(notesPath);
        foreach (var phrase in new[] { "V8.1.3", "修复重连", "UI Automation 根元素", "NativeWindowHandle", "CodexDoctor.exe.sha256" })
            Require(notes.Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.1.3 发布说明缺少：{phrase}");

        var nativeWorkflow = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "v8-native.yml"));
        foreach (var token in new[] { "V8.1.3", @"<Version>8\.1\.3</Version>", "CodexDoctor-V8.1.3-win-x64" })
            Require(nativeWorkflow.Contains(token, StringComparison.Ordinal), $"主原生 CI 尚未升级：{token}");

        var workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.3.yml");
        Require(File.Exists(workflowPath), "必须新增 release-v8.1.3.yml。");
        var workflow = File.ReadAllText(workflowPath);
        foreach (var token in new[] { "v8.1.3", @"<Version>8\.1\.3</Version>", "PublishSingleFile=true", "IncludeNativeLibrariesForSelfExtract=true", "CodexDoctor.exe", "CodexDoctor.exe.sha256", "RELEASE_NOTES_V8.1.3.md" })
            Require(workflow.Contains(token, StringComparison.Ordinal), $"V8.1.3 发布工作流缺少：{token}");
        Require(workflow.Contains("branches: [main]", StringComparison.Ordinal), "V8.1.3 发布工作流必须监听 main。");
        Require(!workflow.Contains("--clobber", StringComparison.Ordinal), "V8.1.3 Release 已存在时不得覆盖既有资产。");
        Require(workflow.Contains("Release 已存在，跳过发布", StringComparison.Ordinal), "V8.1.3 Release 已存在时必须明确跳过。");

        var oldWorkflow = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.2.yml"));
        Require(oldWorkflow.Contains("workflow_dispatch:", StringComparison.Ordinal), "历史 V8.1.2 Release 必须保留手动触发能力。");
        Require(!oldWorkflow.Contains("branches: [main]", StringComparison.Ordinal), "历史 V8.1.2 Release 不得继续监听 main，避免 V8.1.3 合并时误跑旧版本门禁。");
        Require(!oldWorkflow.Contains("--clobber", StringComparison.Ordinal), "历史 V8.1.2 Release 不得覆盖既有资产。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("V8.1.3: " + message);
    }
}
