using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CodexDoctor.Native.Tests;

internal static class Task6ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");
        var repoRoot = sourceRoot.Parent!.Parent!;

        var csproj = File.ReadAllText(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"));
        var versionText = Regex.Match(csproj, @"<Version>([^<]+)</Version>").Groups[1].Value;
        Require(Version.TryParse(versionText, out var currentVersion) && currentVersion >= new Version(8, 0, 1), "原生项目版本不得低于 8.0.1。");

        var readme = File.ReadAllText(Path.Combine(sourceRoot.FullName, "README.md"));
        foreach (var phrase in new[] { "扫描本机 Codex", "扫描默认只读", "敏感", "需要用户操作", "不修改 MSIX/AppX", "Codex Desktop", "Codex CLI" })
            Require(readme.Contains(phrase), $"README 缺少 V8.0.1 说明：{phrase}");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.0.1.md");
        Require(File.Exists(notesPath), "必须保留 RELEASE_NOTES_V8.0.1.md。");
        var notes = File.ReadAllText(notesPath);
        Require(notes.Contains("V8.0.1"), "历史发布说明必须明确 V8.0.1。");
        Require(notes.Contains("一键设置 Codex 为简体中文"), "V8.0.1 发布说明必须保留一键中文功能。");

        var workflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.0.1.yml");
        Require(File.Exists(workflowPath), "必须保留 release-v8.0.1.yml。");
        var workflow = File.ReadAllText(workflowPath);
        foreach (var token in new[] { "v8.0.1", "PublishSingleFile=true", "IncludeNativeLibrariesForSelfExtract=true", "CodexDoctor.exe", "CodexDoctor.exe.sha256" })
            Require(workflow.Contains(token), $"V8.0.1 发布工作流缺少：{token}");
        Require(!workflow.Contains("--clobber"), "V8.0.1 Release 已存在时不得覆盖既有资产。");
        Require(workflow.Contains("Release 已存在，跳过发布"), "V8.0.1 Release 已存在时必须明确跳过。");

        var oldWorkflow = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.0.yml"));
        Require(oldWorkflow.Contains("workflow_dispatch:"), "旧 V8.0 Release 必须保留手动触发能力。");
        Require(!oldWorkflow.Contains("branches: [main]"), "旧 V8.0 Release 不得再监听 main，避免覆盖 V8.0.0 资产。");
        Require(!oldWorkflow.Contains("--clobber"), "旧 V8.0 Release 已存在时不得覆盖既有资产。");
        Require(oldWorkflow.Contains("Release 已存在，跳过发布"), "旧 V8.0 Release 已存在时必须明确跳过。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
