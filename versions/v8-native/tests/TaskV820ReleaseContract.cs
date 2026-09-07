using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = SourceRoot();
        var repoRoot = sourceRoot.Parent!.Parent!;

        var csproj = File.ReadAllText(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"));
        Require(Regex.IsMatch(csproj, @"<Version>8\.2\.0</Version>"), "V8.2 发布门要求项目版本固定为 8.2.0。");
        Require(csproj.Contains("Reconnecting", StringComparison.OrdinalIgnoreCase), "V8.2 项目描述必须体现 Reconnecting 专项自愈能力。");

        var about = File.ReadAllText(Path.Combine(sourceRoot.FullName, "AboutForm.cs"));
        Require(about.Contains("版本：8.2.0", StringComparison.Ordinal), "关于窗口必须显示版本 8.2.0。");

        var readme = File.ReadAllText(Path.Combine(repoRoot.FullName, "README.md"));
        // A newer major release may replace the recommended version; V8 documentation must remain explicit.
        Require(readme.Contains("当前推荐版本：V8.2.0", StringComparison.Ordinal) || readme.Contains("历史版本：V8.2.0", StringComparison.Ordinal), "README 必须保留 V8.2.0 当前或历史版本说明。");
        Require(readme.Contains("Reconnecting 自愈", StringComparison.Ordinal), "README 必须解释 Reconnecting 自愈入口。");
        Require(readme.Contains("RECOVERED", StringComparison.Ordinal), "README 必须解释 RECOVERED 终态。");
        Require(readme.Contains("NETWORK_RECOVERED", StringComparison.Ordinal), "README 必须解释 NETWORK_RECOVERED 终态。");

        var changelog = File.ReadAllText(Path.Combine(repoRoot.FullName, "CHANGELOG.md"));
        var v820 = changelog.IndexOf("## V8.2.0", StringComparison.Ordinal);
        var v812 = changelog.IndexOf("## V8.1.2", StringComparison.Ordinal);
        Require(v820 >= 0 && v812 >= 0 && v820 < v812, "CHANGELOG 必须把 V8.2.0 放在 V8.1.2 之前。");

        var notesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.2.0.md");
        Require(File.Exists(notesPath), "必须新增 RELEASE_NOTES_V8.2.0.md。");
        var notes = File.ReadAllText(notesPath);
        foreach (var phrase in new[] { "Reconnecting", "RECOVERED", "NETWORK_RECOVERED", "codex.proxy.env", "git.proxy.clear", "npm.proxy.clear", "codex.desktop.restart", "回滚", "Windows 用户级", "CodexDoctor.exe.sha256" })
            Require(notes.Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.2.0 发布说明缺少：{phrase}");

        var ci = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "v8-native.yml"));
        foreach (var token in new[] { "V8.2.0", @"<Version>8\.2\.0</Version>", "PublishSingleFile=true", "--self-contained true", "CodexDoctor-V8.2.0-win-x64", "CodexDoctor.exe.sha256", "requireAdministrator" })
            Require(ci.Contains(token, StringComparison.Ordinal), $"V8.2 CI 缺少：{token}");

        var releasePath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.2.0.yml");
        Require(File.Exists(releasePath), "必须新增 release-v8.2.0.yml。");
        var release = File.ReadAllText(releasePath);
        foreach (var token in new[] { "v8.2.0", @"<Version>8\.2\.0</Version>", "PublishSingleFile=true", "IncludeNativeLibrariesForSelfExtract=true", "CodexDoctor.exe", "CodexDoctor.exe.sha256", "RELEASE_NOTES_V8.2.0.md", "Release 已存在，跳过发布" })
            Require(release.Contains(token, StringComparison.Ordinal), $"V8.2 Release 工作流缺少：{token}");
        Require(!release.Contains("--clobber", StringComparison.Ordinal), "V8.2 Release 已存在时不得覆盖既有资产。");

        // 历史 V8.1.2 发布证据必须继续存在且不可被 V8.2 覆盖。
        var oldNotes = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.1.2.md");
        var oldWorkflow = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.1.2.yml");
        Require(File.Exists(oldNotes) && File.Exists(oldWorkflow), "V8.1.2 历史 Release Notes/工作流必须继续保留。");
        var oldRelease = File.ReadAllText(oldWorkflow);
        Require(oldRelease.Contains("v8.1.2", StringComparison.Ordinal), "V8.1.2 历史工作流必须仍绑定 v8.1.2。");
        Require(!oldRelease.Contains("--clobber", StringComparison.Ordinal), "V8.1.2 历史资产仍不得覆盖。");
    }
}
