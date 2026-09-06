using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var repo = RepoRoot();
        var source = SourceRoot();
        var releaseNotesPath = Path.Combine(repo.FullName, "RELEASE_NOTES_V8.1.0.md");
        var releaseWorkflowPath = Path.Combine(repo.FullName, ".github", "workflows", "release-v8.1.yml");

        Require(File.Exists(releaseNotesPath), "V8.1.0 必须有独立 Release Notes。");
        Require(File.Exists(releaseWorkflowPath), "V8.1.0 必须有独立 release workflow。");

        var releaseNotes = File.ReadAllText(releaseNotesPath);
        foreach (var phrase in new[] { "V8.1.0", "一键扫描", "一键修复", "管理员", "导出完整报告", "软件作者：Aix" })
            Require(releaseNotes.Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.1.0 Release Notes 缺少：{phrase}");

        var workflow = File.ReadAllText(releaseWorkflowPath);
        foreach (var token in new[]
        {
            "v8.1.0",
            "--self-contained true",
            "PublishSingleFile=true",
            "IncludeNativeLibrariesForSelfExtract=true",
            "CodexDoctor.exe",
            "CodexDoctor.exe.sha256",
            "gh release view v8.1.0",
            "exit 0"
        })
            Require(workflow.Contains(token, StringComparison.OrdinalIgnoreCase), $"V8.1 release workflow 缺少发布/防覆盖合同：{token}");

        var rootReadme = File.ReadAllText(Path.Combine(repo.FullName, "README.md"));
        var nativeReadme = File.ReadAllText(Path.Combine(source.FullName, "README.md"));
        foreach (var phrase in new[] { "UAC", "一键扫描", "问题分级", "一键修复", "自动复检", "启动 Codex", "重启 Codex", "智能迁移", "一键中文", "导出完整报告" })
            Require((rootReadme + "\n" + nativeReadme).Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.1 README 缺少主流程/主操作：{phrase}");
        foreach (var phrase in new[] { "Aix", "976936105", "xch03209527" })
            Require((rootReadme + "\n" + nativeReadme).Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.1 README 缺少公开作者信息：{phrase}");

        var changelog = File.ReadAllText(Path.Combine(repo.FullName, "CHANGELOG.md"));
        Require(changelog.Contains("V8.1.0", StringComparison.OrdinalIgnoreCase), "CHANGELOG 必须记录 V8.1.0。");
        Require(changelog.Contains("自动复检", StringComparison.OrdinalIgnoreCase), "CHANGELOG 必须记录 V8.1 自动复检能力。");

        var ci = File.ReadAllText(Path.Combine(repo.FullName, ".github", "workflows", "v8-native.yml"));
        foreach (var token in new[] { "0x4D", "0x5A", ".ps1", ".psm1", ".dll", "powershell.exe", "pwsh.exe", "SHA256" })
            Require(ci.Contains(token, StringComparison.OrdinalIgnoreCase), $"v8-native 最终门禁缺少：{token}");
    }
}
