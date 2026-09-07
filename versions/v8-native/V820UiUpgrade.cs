using System.Runtime.CompilerServices;

namespace CodexDoctor.Native;

public static class V820UiUpgrade
{
    private sealed class RecoveryHolder
    {
        public ReconnectingRecoveryResult? Value { get; set; }
    }

    private static readonly ConditionalWeakTable<MainForm, RecoveryHolder> LatestRecovery = new();

    public static void Apply(MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        // 保留 V8.1.2 的语言切换、安装/卸载等既有升级能力，再叠加 V8.2 专项入口。
        V811UiUpgrade.Apply(form);

        form.Text = "Codex Doctor V8.2.0 Reconnecting 自愈中心";
        var title = Descendants(form).OfType<Label>()
            .FirstOrDefault(x => x.Text.StartsWith("Codex Doctor V8.1", StringComparison.Ordinal));
        if (title is not null) title.Text = "Codex Doctor V8.2.0";

        var panel = form.Controls.OfType<FlowLayoutPanel>().FirstOrDefault()
            ?? throw new InvalidOperationException("未找到主操作区。");
        var scanButton = Descendants(form).OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("一键扫描 Codex", StringComparison.OrdinalIgnoreCase));

        ResizeForNineButtons(panel);

        var reconnecting = new Button
        {
            Text = "Reconnecting 自愈",
            Size = new Size(142, 44),
            Margin = new Padding(4),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold)
        };
        panel.Controls.Add(reconnecting);

        var restart = panel.Controls.OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("重启 Codex", StringComparison.OrdinalIgnoreCase));
        if (restart is not null)
            panel.Controls.SetChildIndex(reconnecting, Math.Min(panel.Controls.GetChildIndex(restart) + 1, panel.Controls.Count - 1));

        var selfHeal = new ReconnectingSelfHealService(
            CodexHealthScannerAdapter.CreateDefault(),
            new ReconnectingRepairExecutor(new DefaultReconnectingRepairActionFactory()));

        reconnecting.Click += async (_, _) =>
        {
            if (!reconnecting.Enabled) return;
            if (MessageBox.Show(
                    "将执行 Reconnecting 专项闭环：\n\n修复前扫描 → 安全白名单计划 → 备份/修复/验证/必要时回滚 → Desktop 重启（如需要）→ 独立修复后复检。\n\n直连正常时不会写代理；未通过 HTTPS 验证的代理不会写入 .codex/.env。是否继续？",
                    "确认 Reconnecting 自愈",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            var enabledSnapshot = CaptureEnabledState(panel, scanButton);
            reconnecting.Enabled = false;
            SetControlsEnabled(panel, scanButton, false);

            try
            {
                AppendLog(form, "开始 V8.2 Reconnecting 专项自愈……");
                var progress = new Progress<ReconnectingSelfHealProgress>(p =>
                {
                    SetProgressText(form, p.MessageZh);
                    AppendLog(form, $"[Reconnecting][{p.Stage}] {p.MessageZh}");
                });

                var result = await selfHeal.RecoverAsync(progress);
                SetLatestRecovery(form, result);

                foreach (var action in result.Actions)
                    AppendLog(form, $"[专项动作] {action.TitleZh} | {action.StatusZh} | {action.SummaryZh}" +
                        (string.IsNullOrWhiteSpace(action.BackupPath) ? string.Empty : $" | 备份={action.BackupPath}"));
                AppendLog(form, $"[专项终态] {result.StatusName} | {result.SummaryZh}");

                if (result.Status == ReconnectingRecoveryStatus.Recovered)
                {
                    MessageBox.Show(
                        "✓ Reconnecting 专项复检已通过：网络路径与 Codex Desktop 均满足完整恢复条件。\n\n" + result.SummaryZh,
                        "Reconnecting 已恢复",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        result.SummaryZh + "\n\n当前终态不会被伪报为成功。建议导出完整报告查看修复前/后网络诊断和每个动作的验证/回滚证据。",
                        "Reconnecting 自愈未达到完整恢复",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog(form, "Reconnecting 自愈已取消；取消后的后续变更未继续执行。");
            }
            catch (Exception ex)
            {
                AppendLog(form, "Reconnecting 自愈失败：" + ex.Message);
                MessageBox.Show(
                    "Reconnecting 自愈失败：" + ex.Message + "\n\n建议导出完整报告并按终态证据继续处理。",
                    "Reconnecting 自愈失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                RestoreEnabledState(enabledSnapshot);
                reconnecting.Enabled = true;
                TriggerMainRescan(form);
            }
        };
    }

    public static ReconnectingRecoveryResult? GetLatestRecovery(MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return LatestRecovery.TryGetValue(form, out var holder) ? holder.Value : null;
    }

    private static void SetLatestRecovery(MainForm form, ReconnectingRecoveryResult result)
    {
        var holder = LatestRecovery.GetOrCreateValue(form);
        holder.Value = result;
    }

    private static Dictionary<Control, bool> CaptureEnabledState(FlowLayoutPanel panel, Button? scanButton)
    {
        var result = panel.Controls.Cast<Control>().ToDictionary(x => x, x => x.Enabled);
        if (scanButton is not null) result[scanButton] = scanButton.Enabled;
        return result;
    }

    private static void RestoreEnabledState(IReadOnlyDictionary<Control, bool> snapshot)
    {
        foreach (var pair in snapshot)
        {
            if (!pair.Key.IsDisposed) pair.Key.Enabled = pair.Value;
        }
    }

    private static void SetControlsEnabled(FlowLayoutPanel panel, Button? scanButton, bool enabled)
    {
        foreach (Control control in panel.Controls) control.Enabled = enabled;
        if (scanButton is not null) scanButton.Enabled = enabled;
    }

    private static void SetProgressText(MainForm form, string text)
    {
        var label = Descendants(form).OfType<Label>()
            .FirstOrDefault(x => x.Location.Y is >= 100 and <= 150 && x.Width >= 200);
        if (label is not null) label.Text = text;
    }

    private static void AppendLog(MainForm form, string message)
    {
        var log = Descendants(form).OfType<TextBox>()
            .FirstOrDefault(x => x.Multiline && x.ReadOnly);
        if (log is null) return;
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void TriggerMainRescan(MainForm form)
    {
        var scan = Descendants(form).OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("一键扫描 Codex", StringComparison.OrdinalIgnoreCase));
        if (scan?.Enabled == true) scan.PerformClick();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void ResizeForNineButtons(FlowLayoutPanel panel)
    {
        panel.WrapContents = false;
        panel.Height = 56;
        foreach (var button in panel.Controls.OfType<Button>())
        {
            button.Font = new Font("Microsoft YaHei UI", 8.5F, button.Font.Bold ? FontStyle.Bold : FontStyle.Regular);
            if (button.Text.Contains("启动 Codex", StringComparison.OrdinalIgnoreCase)) button.Width = 102;
            else if (button.Text.Contains("重启 Codex", StringComparison.OrdinalIgnoreCase)) button.Width = 102;
            else if (button.Text.Contains("修复", StringComparison.OrdinalIgnoreCase)) button.Width = 102;
            else if (button.Text.Contains("迁移", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("恢复", StringComparison.OrdinalIgnoreCase)) button.Width = 120;
            else if (button.Text.Equals("中文", StringComparison.OrdinalIgnoreCase)) button.Width = 68;
            else if (button.Text.Equals("English", StringComparison.OrdinalIgnoreCase)) button.Width = 76;
            else if (button.Text.Contains("安装", StringComparison.OrdinalIgnoreCase)) button.Width = 105;
            else if (button.Text.Contains("导出", StringComparison.OrdinalIgnoreCase)) button.Width = 115;
        }
    }
}