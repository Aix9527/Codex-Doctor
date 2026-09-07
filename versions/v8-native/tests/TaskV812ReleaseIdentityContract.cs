using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV812ReleaseIdentityContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");
        var repoRoot = sourceRoot.Parent!.Parent!;

        // V8.1.2 已成为历史版本：后续版本不得再被它锁死当前产品身份，
        // 但 V8.1.2 的发布证据、tag 语义和既有资产保护必须永久保留。
        var changelog = File.ReadAllText(Path.Combine(repoRoot.FullName, "CHANGELOG.md"));
        var v812 = changelog.IndexOf("## V8.1.2", StringComparison.Ordinal);
        var v811 = changelog.IndexOf("## V8.1.1", StringComparison.Ordinal);
        Require(v812 >= 0 && v811 >= 0 && v812 < v811, "CHANGELOG 必须继续保留 V8.1.2 历史记录并位于 V8.1.1 之前。");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.1.2.md");
        Require(File.Exists(notesPath), "V8.1.2 历史 RELEASE_NOTES 必须继续保留。");
        var notes = File.ReadAllText(notesPath);
        foreach (var phrase in new[] { "V8.1.2", "自由选择", "动态", "多进程", "CodexDoctor.exe.sha256" })
            Require(notes.Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.1.2 历史发布说明缺少：{phrase}");

        var workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.2.yml");
        Require(File.Exists(workflowPath), "V8.1.2 历史 release 工作流必须继续保留。");
        var workflow = File.ReadAllText(workflowPath);
        foreach (var token in new[] { "v8.1.2", @"<Version>8\.1\.2</Version>", "PublishSingleFile=true", "IncludeNativeLibrariesForSelfExtract=true", "CodexDoctor.exe", "CodexDoctor.exe.sha256", "RELEASE_NOTES_V8.1.2.md" })
            Require(workflow.Contains(token, StringComparison.Ordinal), $"V8.1.2 历史发布工作流缺少：{token}");
        Require(workflow.Contains("workflow_dispatch:", StringComparison.Ordinal), "V8.1.2 历史 Release 必须保留手动触发能力。");
        Require(!workflow.Contains("branches: [main]", StringComparison.Ordinal), "V8.1.2 历史 Release 不得继续监听 main，避免 V8.2 合并时误跑旧版本门禁。");
        Require(!workflow.Contains("--clobber", StringComparison.Ordinal), "V8.1.2 Release 已存在时不得覆盖既有资产。");
        Require(workflow.Contains("Release 已存在，跳过发布", StringComparison.Ordinal), "V8.1.2 Release 已存在时必须明确跳过。");

        var oldWorkflow = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.1.yml"));
        Require(oldWorkflow.Contains("workflow_dispatch:", StringComparison.Ordinal), "历史 V8.1.1 Release 必须保留手动触发能力。");
        Require(!oldWorkflow.Contains("branches: [main]", StringComparison.Ordinal), "历史 V8.1.1 Release 不得继续监听 main。");
        Require(!oldWorkflow.Contains("--clobber", StringComparison.Ordinal), "历史 V8.1.1 Release 不得覆盖既有资产。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
