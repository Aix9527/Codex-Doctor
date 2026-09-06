using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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

        var csproj = File.ReadAllText(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"));
        Require(Regex.IsMatch(csproj, @"<Version>8\.1\.1</Version>"), "V8.1.1 发布合同要求项目版本固定为 8.1.1。");

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
        Require(changelog.Contains("## V8.1.1", StringComparison.Ordinal), "CHANGELOG 必须新增 V8.1.1 条目。");
        Require(changelog.Contains("中文 / English", StringComparison.Ordinal), "CHANGELOG 必须说明双向语言切换。");
        Require(changelog.Contains("Desktop / CLI", StringComparison.Ordinal), "CHANGELOG 必须说明 Desktop / CLI 安装卸载。");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.1.1.md");
        Require(File.Exists(notesPath), "必须新增 RELEASE_NOTES_V8.1.1.md。");
        var notes = File.ReadAllText(notesPath);
        foreach (var phrase in new[] { "V8.1.1", "中文", "English", "安装", "卸载", "9NT1R1C2HH7J", "@openai/codex", "重新扫描验证" })
            Require(notes.Contains(phrase, StringComparison.Ordinal), $"V8.1.1 Release Notes 缺少：{phrase}");

        var workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.1.yml");
        Require(File.Exists(workflowPath), "必须新增 release-v8.1.1.yml。");
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
