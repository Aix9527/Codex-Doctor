using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820ReleaseContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");
        var repoRoot = sourceRoot.Parent?.Parent ?? throw new Exception("无法定位仓库根目录。");

        var project = File.ReadAllText(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"));
        var about = File.ReadAllText(Path.Combine(sourceRoot.FullName, "AboutForm.cs"));
        var exporter = File.ReadAllText(Path.Combine(sourceRoot.FullName, "HealthReportExporter.cs"));
        var upgrade = File.ReadAllText(Path.Combine(sourceRoot.FullName, "V811UiUpgrade.cs"));
        var rootReadme = File.ReadAllText(Path.Combine(repoRoot.FullName, "README.md"));
        var changelog = File.ReadAllText(Path.Combine(repoRoot.FullName, "CHANGELOG.md"));
        var nativeWorkflow = File.ReadAllText(Path.Combine(repoRoot.FullName, ".github", "workflows", "v8-native.yml"));
        var releasePath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.2.0.yml");
        var releaseNotesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.2.0.md");

        Require(project.Contains("<Version>8.2.0</Version>", StringComparison.Ordinal), "项目版本必须固定为 8.2.0。");
        Require(about.Contains("V8.2.0", StringComparison.Ordinal), "关于页必须显示 V8.2.0。");
        Require(exporter.Contains("版本 = \"8.2.0\"", StringComparison.Ordinal), "健康报告内部版本必须为 8.2.0。");
        Require(upgrade.Contains("V8.2.0", StringComparison.Ordinal), "主窗口升级层必须显示 V8.2.0。");
        Require(rootReadme.Contains("当前推荐版本：V8.2.0", StringComparison.Ordinal), "根 README 必须推荐 V8.2.0。");
        Require(changelog.Contains("## V8.2.0", StringComparison.Ordinal), "CHANGELOG 必须包含 V8.2.0。");
        Require(File.Exists(releaseNotesPath), "必须存在 RELEASE_NOTES_V8.2.0.md。");
        Require(File.Exists(releasePath), "必须存在独立 release-v8.2.0 工作流。");

        Require(nativeWorkflow.Contains("V8.2.0", StringComparison.Ordinal), "原生 CI 文案必须升级到 V8.2.0。");
        Require(nativeWorkflow.Contains("<Version>8\\.2\\.0</Version>", StringComparison.Ordinal), "原生 CI 必须硬检查项目版本 8.2.0。");
        Require(nativeWorkflow.Contains("CodexDoctor-V8.2.0-win-x64", StringComparison.Ordinal), "CI artifact 必须使用 V8.2.0 身份。");

        if (File.Exists(releasePath))
        {
            var release = File.ReadAllText(releasePath);
            Require(release.Contains("v8.2.0", StringComparison.Ordinal), "Release tag 必须固定为 v8.2.0。");
            Require(release.Contains("CodexDoctor.exe", StringComparison.Ordinal), "Release 必须包含 CodexDoctor.exe。");
            Require(release.Contains("CodexDoctor.exe.sha256", StringComparison.Ordinal), "Release 必须包含 SHA256 文件。");
            Require(release.Contains("requireAdministrator", StringComparison.Ordinal), "Release 门必须验证管理员 manifest。");
            Require(release.Contains("Release 已存在，跳过发布且不覆盖", StringComparison.Ordinal), "既有 v8.2.0 Release 必须跳过且不覆盖资产。");
            Require(release.Contains(".dll", StringComparison.Ordinal) && release.Contains(".runtimeconfig.json", StringComparison.Ordinal), "Release 门必须禁止外置运行依赖。");
            Require(release.Contains("powershell.exe", StringComparison.Ordinal) && release.Contains("pwsh.exe", StringComparison.Ordinal), "Release 门必须检查 C# 运行时无 PowerShell 依赖。");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
