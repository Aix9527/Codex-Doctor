using System.Runtime.CompilerServices;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV820UiContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot().FullName, "MainForm.cs"));

        Require(source.Contains("Codex Doctor V8.2.0", StringComparison.Ordinal), "V8.2 主界面必须明确显示 8.2.0。");
        Require(source.Contains("Reconnecting 自愈", StringComparison.Ordinal), "主界面必须提供可见的“Reconnecting 自愈”按钮。");
        Require(source.Contains("ReconnectingSelfHealService", StringComparison.Ordinal), "主界面必须绑定专项自愈服务，而不是复用普通一键修复假装闭环。");
        Require(source.Contains("RecoverAsync(progress", StringComparison.Ordinal), "专项按钮的异步处理器必须调用 ReconnectingSelfHealService.RecoverAsync。");
        Require(source.Contains("ReconnectingRecoveryStatus.Recovered", StringComparison.Ordinal), "UI 必须只在终态为 Recovered 时显示恢复成功。");
        Require(source.Contains("_reconnectingButton.Enabled = !_busy", StringComparison.Ordinal), "专项自愈运行时必须禁用自身按钮，防止并发重复修复。");
        Require(source.Contains("await ScanAsync()", StringComparison.Ordinal), "专项自愈完成后必须刷新主界面的全量健康扫描事实源。");
        Require(source.Contains("action.StatusZh", StringComparison.Ordinal), "专项自愈必须把动作验证/回滚结果写入现有日志区。");
    }
}
