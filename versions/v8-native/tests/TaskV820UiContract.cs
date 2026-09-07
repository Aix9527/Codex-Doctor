using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820UiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = SourceRoot().FullName;
        var mainSource = File.ReadAllText(Path.Combine(root, "MainForm.cs"));
        var programSource = File.ReadAllText(Path.Combine(root, "Program.cs"));
        var upgradePath = Path.Combine(root, "V820UiUpgrade.cs");
        Require(File.Exists(upgradePath), "V8.2 必须使用独立 V820UiUpgrade.cs 扩展现有主界面，避免继续膨胀 MainForm。");
        var upgradeSource = File.ReadAllText(upgradePath);
        var source = mainSource + "\n" + programSource + "\n" + upgradeSource;

        var legacyIndex = programSource.IndexOf("V811UiUpgrade.Apply(form)", StringComparison.Ordinal);
        var v820Index = programSource.IndexOf("V820UiUpgrade.Apply(form)", StringComparison.Ordinal);
        Require(legacyIndex >= 0 && v820Index > legacyIndex, "Program 必须先应用 V8.1.2 UI 兼容层，再叠加 V8.2 UI 升级层。");
        Require(!upgradeSource.Contains("V811UiUpgrade.Apply(form)", StringComparison.Ordinal), "V820UiUpgrade 不得重复应用 V811UiUpgrade，避免重复按钮和重复事件绑定。");
        Require(source.Contains("Codex Doctor V8.2.0", StringComparison.Ordinal), "V8.2 主界面必须明确显示 8.2.0。");
        Require(source.Contains("Reconnecting 自愈", StringComparison.Ordinal), "主界面必须提供可见的“Reconnecting 自愈”按钮。");
        Require(upgradeSource.Contains("ReconnectingSelfHealService", StringComparison.Ordinal), "专项入口必须绑定 ReconnectingSelfHealService，而不是复用普通一键修复假装闭环。");
        Require(upgradeSource.Contains("RecoverAsync(progress", StringComparison.Ordinal), "专项按钮的异步处理器必须调用 ReconnectingSelfHealService.RecoverAsync。");
        Require(upgradeSource.Contains("ReconnectingRecoveryStatus.Recovered", StringComparison.Ordinal), "UI 必须只在终态为 Recovered 时显示恢复成功。");
        Require(upgradeSource.Contains("reconnecting.Enabled = false", StringComparison.Ordinal), "专项自愈运行时必须禁用自身按钮，防止并发重复修复。");
        Require(upgradeSource.Contains("finally", StringComparison.Ordinal) && upgradeSource.Contains("reconnecting.Enabled = true", StringComparison.Ordinal), "专项自愈按钮必须在 finally 中恢复可用状态。");
        Require(upgradeSource.Contains("TriggerMainRescan(form)", StringComparison.Ordinal), "专项自愈完成后必须刷新主界面的全量健康扫描事实源。");
        Require(upgradeSource.Contains("action.StatusZh", StringComparison.Ordinal), "专项自愈必须把动作验证/回滚结果写入现有日志区。");
        Require(upgradeSource.Contains("建议导出完整报告", StringComparison.Ordinal), "非 Recovered 终态必须提示导出报告，而不能伪报成功。");
    }
}