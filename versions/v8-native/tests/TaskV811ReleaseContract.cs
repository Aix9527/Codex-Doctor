using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");
        var repoRoot = sourceRoot.Parent!.Parent!;

        // V8.1.1 已成为历史发布：当前主线版本可以继续前进，但 V8.1.1 的文档、Release Notes
        // 和固定到 v8.1.1 tag 的手动复现工作流必须继续保留，且不能在 main 上自动触发。
        var readme = File.ReadAllText(Path.Combine(sourceRoot.FullName, "README.md"));
        foreach (var phrase in new[]
        {
            "V8.1.1",
            "中文",
            "English",
            "安装 / 卸载",
            "9NT1R1C2HH7J",
            "@openai/codex",
            "重新扫描",
            "不伪报成功"
        })
            Require(readme.Contains(phrase, StringComparison.Ordinal), $"V8.1.1 README 缺少说明：{phrase}");

        var changelog = File.ReadAllText(Path.Combine(repoRoot.FullName, "CHANGELOG.md"));
        Require(changelog.Contains("## V8.1.1", StringComparison.Ordinal), "CHANGELOG 必须保留 V8.1.1 条目。");
        Require(changelog.Contains("中文 / English", StringComparison.Ordinal), "CHANGELOG 必须说明双向语言切换。");
        Require(changelog.Contains("Desktop / CLI", StringComparison.Ordinal), "CHANGELOG 必须说明 Desktop / CLI 安装卸载。");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.1.1.md");
        Require(File.Exists(notesPath), "必须保留 RELEASE_NOTES_V8.1.1.md。");
        var notes = File.ReadAllText(notesPath);
        foreach (var phrase in new[] { "V8.1.1", "中文", "English", "安装", "卸载", "9NT1R1C2HH7J", "@openai/codex", "重新扫描验证" })
            Require(notes.Contains(phrase, StringComparison.Ordinal), $"V8.1.1 Release Notes 缺少：{phrase}");

        var workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.1.yml");
        Require(File.Exists(workflowPath), "必须保留 release-v8.1.1.yml。");
        var workflow = File.ReadAllText(workflowPath);
        foreach (var token in new[]
        {
            "v8.1.1",
            "PublishSingleFile=true",
            "IncludeNativeLibrariesForSelfExtract=true",
            "PublishTrimmed=false",
            "CodexDoctor.exe",
            "CodexDoctor.exe.sha256",
            "requireAdministrator",
            "<Version>8\\.1\\.1</Version>",
            "Get-FileHash",
            "Release 已存在，跳过发布"
        })
            Require(workflow.Contains(token, StringComparison.Ordinal), $"V8.1.1 发布工作流缺少：{token}");
        Require(workflow.Contains("workflow_dispatch:", StringComparison.Ordinal), "历史 V8.1.1 Release 必须保留手动触发能力。");
        Require(workflow.Contains("ref: v8.1.1", StringComparison.Ordinal), "历史 V8.1.1 Release 必须固定 checkout v8.1.1 tag，不能用当前主线源码伪复现旧版本。");
        Require(!workflow.Contains("branches: [main]", StringComparison.Ordinal), "历史 V8.1.1 Release 不得继续监听 main。");
        Require(!workflow.Contains("--clobber", StringComparison.Ordinal), "V8.1.1 Release 已存在时不得覆盖既有资产。");

        var oldWorkflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.yml");
        var oldWorkflow = File.ReadAllText(oldWorkflowPath);
        Require(oldWorkflow.Contains("workflow_dispatch:", StringComparison.Ordinal), "历史 V8.1.0 Release 必须保留手动触发能力。");
        Require(!oldWorkflow.Contains("branches: [main]", StringComparison.Ordinal), "历史 V8.1.0 Release 不得继续监听 main，避免每次 V8.1.x 合并都重复尝试旧发布。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
