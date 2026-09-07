using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81ReportContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var profile = Path.Combine(Path.GetTempPath(), "Aix-Private-Profile-" + Guid.NewGuid().ToString("N"));
        var desktopPath = Path.Combine(profile, "AppData", "Local", "Programs", "Codex", "Codex.exe");
        var envPath = Path.Combine(profile, ".codex", ".env");
        var issue = new CodexIssue(
            "secret-evidence", "config", CodexIssueSeverity.Warning,
            "配置异常", "OPENAI_API_KEY=sk-super-secret-123456",
            "Bearer private-session-token", "test",
            CodexIssueStatus.ManualRequired, false, null,
            false, false, false, false, null, "config");
        var discovery = new CodexDiscoveryResult(
            [new CodexDesktopInstallationInfo("Codex Desktop", "测试", desktopPath, "1.0", true, [7])],
            new CodexCliInfo(false, null, false, null, false),
            new CodexDataDirectoryInfo(Path.Combine(profile, ".codex"), true, false, null, 1, 10),
            [new CodexConfigFileInfo(envPath, true, [new CodexConfigEntryInfo("OPENAI_API_KEY", null, true, true)])],
            new CodexLanguageState("未知", "未知", "未知", false, true, "需要用户操作"));
        var before = new CodexHealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, [issue], discovery, null);
        var after = new CodexHealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, [], discovery, null);
        var repair = new RepairSessionResult(Guid.NewGuid(),
            [new RepairActionResult("test", "测试动作", RepairActionStatus.RolledBack, "验证失败，已执行回滚。", Path.Combine(profile, "backup.json"))]);
        var repairAndRescan = new RepairAndRescanResult(before, repair, after);
        var plan = new RepairPlan(repair.PlanId, DateTimeOffset.UtcNow, before.ScanId, [], false, [issue], []);

        var exporter = new HealthReportExporter(profile, () => true);
        var json = exporter.BuildJson(after, repairAndRescan, plan);

        // V8.1 引入的是报告审计/隐私合同，不应把后续 V8 版本永久锁死在 8.1.x。
        // 报告身份必须和当前项目版本保持一致，同时不得回退到 8.1.0 之前。
        var csproj = File.ReadAllText(Path.Combine(SourceRoot().FullName, "CodexDoctor.Native.csproj"));
        var projectVersionText = Regex.Match(csproj, @"<Version>([^<]+)</Version>").Groups[1].Value;
        Require(Version.TryParse(projectVersionText, out var projectVersion) && projectVersion >= new Version(8, 1, 0),
            "当前项目版本不得低于 V8.1 报告合同基线。");
        using (var document = JsonDocument.Parse(json))
        {
            var reportVersionText = document.RootElement.GetProperty("版本").GetString();
            Require(Version.TryParse(reportVersionText, out var reportVersion), "报告必须声明有效版本号。");
            Require(reportVersion == projectVersion, "报告版本必须与当前项目版本一致。");
        }

        Require(json.Contains("\"软件作者\": \"Aix\""), "报告作者必须只写 Aix。");
        Require(json.Contains("\"管理员权限\": true"), "报告必须记录管理员权限状态。");
        Require(json.Contains("\"修复计划\""), "报告必须包含 RepairPlan 摘要。");
        Require(json.Contains("\"修复执行\""), "报告必须包含修复动作/验证/回滚结果。");
        Require(json.Contains(before.ScanId.ToString()), "报告必须包含修复前 ScanId。");
        Require(json.Contains(after.ScanId.ToString()), "报告必须包含修复后 ScanId。");
        Require(json.Contains("已回滚"), "报告必须记录回滚状态。");
        Require(json.Contains("%USERPROFILE%"), "用户目录必须标准化为 %USERPROFILE%。");
        Require(!json.Contains(profile, StringComparison.OrdinalIgnoreCase), "报告不得包含真实用户目录。");
        Require(!json.Contains("sk-super-secret", StringComparison.OrdinalIgnoreCase), "报告不得包含 API Key 原值。");
        Require(!json.Contains("private-session-token", StringComparison.OrdinalIgnoreCase), "报告不得包含 Bearer/会话令牌。");
        Require(!json.Contains("976936105", StringComparison.Ordinal), "完整报告不得写入 QQ。");
        Require(!json.Contains("xch03209527", StringComparison.OrdinalIgnoreCase), "完整报告不得写入抖音号。");
        Require(!json.Contains("\\u8f6f\\u4ef6", StringComparison.OrdinalIgnoreCase), "中文报告不得使用 Unicode 转义展示中文字段。");

        var exporterSource = File.ReadAllText(Path.Combine(SourceRoot().FullName, "HealthReportExporter.cs"));
        Require(exporterSource.Contains("UnsafeRelaxedJsonEscaping"), "报告必须直接输出可读中文 JSON。");
        Require(!exporterSource.Contains("976936105"), "报告模块不得硬编码 QQ。");
        Require(!exporterSource.Contains("xch03209527", StringComparison.OrdinalIgnoreCase), "报告模块不得硬编码抖音号。");

        var main = File.ReadAllText(Path.Combine(SourceRoot().FullName, "MainForm.cs"));
        Require(main.Contains("HealthReportExporter"), "主界面“导出完整报告”必须使用隐私安全报告导出器。");
        Require(main.Contains("_lastRepairPlan"), "主界面必须保留最近 RepairPlan 供报告审计。");
    }
}
