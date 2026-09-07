namespace CodexDoctor.Native;

public static class V811UiUpgrade
{
    public static void Apply(MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        form.Text = "Codex Doctor V8.1.3 原生维修中心";

        var title = form.Controls.OfType<Label>().FirstOrDefault(x => x.Text.StartsWith("Codex Doctor V8.1", StringComparison.Ordinal));
        if (title is not null) title.Text = "Codex Doctor V8.1.3";

        var panel = form.Controls.OfType<FlowLayoutPanel>().FirstOrDefault()
            ?? throw new InvalidOperationException("未找到主操作区。");

        var legacyLanguage = panel.Controls.OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("中文", StringComparison.OrdinalIgnoreCase) ||
                                 x.Text.Contains("用户操作", StringComparison.OrdinalIgnoreCase) ||
                                 x.Text.Contains("自动设置", StringComparison.OrdinalIgnoreCase));
        if (legacyLanguage is not null)
        {
            panel.Controls.Remove(legacyLanguage);
            legacyLanguage.Dispose();
        }

        // V8.1.x 把主操作保持在同一行，不扩大面板避免遮挡下方迁移目标和问题列表。
        panel.WrapContents = false;
        panel.Height = 56;
        ResizeExistingButtons(panel);

        var reconnect = CreateButton("修复重连", 100);
        var chinese = CreateButton("中文", 72);
        var english = CreateButton("English", 76);
        var install = CreateButton("安装 / 卸载", 112);
        panel.Controls.Add(reconnect);
        panel.Controls.Add(chinese);
        panel.Controls.Add(english);
        panel.Controls.Add(install);

        var report = panel.Controls.OfType<Button>().FirstOrDefault(x => x.Text.Contains("导出", StringComparison.OrdinalIgnoreCase));
        if (report is not null)
        {
            // 目标顺序：启动 / 重启 / 修复重连 / 一键修复 / 迁移 / 中文 / English / 安装卸载 / 导出。
            panel.Controls.SetChildIndex(reconnect, Math.Min(2, panel.Controls.Count - 1));
            panel.Controls.SetChildIndex(chinese, Math.Min(5, panel.Controls.Count - 1));
            panel.Controls.SetChildIndex(english, Math.Min(6, panel.Controls.Count - 1));
            panel.Controls.SetChildIndex(install, Math.Min(7, panel.Controls.Count - 1));
            panel.Controls.SetChildIndex(report, Math.Min(8, panel.Controls.Count - 1));
        }

        var scanButton = Descendants(form).OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("一键扫描 Codex", StringComparison.OrdinalIgnoreCase));
        var progress = Descendants(form).OfType<ProgressBar>().FirstOrDefault();
        var extensionBusy = false;

        reconnect.Click += async (_, _) =>
        {
            if (extensionBusy) return;
            extensionBusy = true;
            try { await RepairReconnectAsync(form); }
            finally { extensionBusy = false; }
        };
        chinese.Click += async (_, _) =>
        {
            if (extensionBusy) return;
            extensionBusy = true;
            try { await SwitchLanguageAsync(form, DesktopUiLanguage.ChineseSimplified); }
            finally { extensionBusy = false; }
        };
        english.Click += async (_, _) =>
        {
            if (extensionBusy) return;
            extensionBusy = true;
            try { await SwitchLanguageAsync(form, DesktopUiLanguage.English); }
            finally { extensionBusy = false; }
        };
        install.Click += (_, _) =>
        {
            if (extensionBusy) return;
            extensionBusy = true;
            try
            {
                using var manager = new CodexInstallManagerForm();
                manager.ShowDialog(form);
                if (manager.Changed) TriggerMainRescan(form);
            }
            finally { extensionBusy = false; }
        };

        // 与 V8.1 的“先扫描，再操作”一致：完成一次 22/22 扫描后才启用新增主动作。
        var timer = new System.Windows.Forms.Timer { Interval = 300 };
        timer.Tick += (_, _) =>
        {
            var scanned = progress is not null && progress.Maximum > 0 && progress.Value >= progress.Maximum;
            var mainReady = scanButton is null || scanButton.Enabled;
            var enabled = scanned && mainReady && !extensionBusy;
            reconnect.Enabled = enabled;
            chinese.Enabled = enabled;
            english.Enabled = enabled;
            install.Enabled = enabled;
        };
        form.FormClosed += (_, _) => timer.Dispose();
        timer.Start();
    }

    private static async Task RepairReconnectAsync(MainForm owner)
    {
        try
        {
            var discovery = await new CodexDiscoveryService().ScanAsync();
            var desktop = CodexDesktopSelector.SelectPreferred(discovery.DesktopClients);
            if (desktop is null)
            {
                MessageBox.Show(
                    "重新扫描后仍未发现 ChatGPT/Codex Desktop，无法执行重连修复。",
                    "无法修复重连",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var repair = new RepairService();
            var service = new ReconnectRepairService(
                new DiagnosisReconnectHealthProbe(new DiagnosisService()),
                new ReconnectRepairBackend(repair));

            var result = await service.RepairAsync(discovery);
            if (result.ProxyChanged || result.Restarted)
                TriggerMainRescan(owner);

            if (!result.Success || !result.Verified)
            {
                var backup = string.IsNullOrWhiteSpace(result.BackupPath)
                    ? string.Empty
                    : $"\n备份：{result.BackupPath}";
                MessageBox.Show(
                    $"重连修复未通过验证。\n\n{result.SummaryZh}{backup}\n\nCodex Doctor 不会在没有健康网络路径时伪报修复成功，也不会默认修改 Windows 用户级代理。",
                    "修复重连未通过验证",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var changed = result.ProxyChanged ? "已修复 Codex 专用代理并重启 Desktop。" : "网络配置无需改写，已重启 Desktop。";
            MessageBox.Show(
                $"✓ 重连修复已完成并通过网络复检。\n\n{changed}\n{result.SummaryZh}",
                "修复重连完成",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("修复重连失败：" + ex.Message, "修复重连失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static async Task SwitchLanguageAsync(MainForm owner, DesktopUiLanguage target)
    {
        var targetName = target == DesktopUiLanguage.ChineseSimplified ? "中文" : "English";
        try
        {
            var discoveryService = new CodexDiscoveryService();
            var discovery = await discoveryService.ScanAsync();
            var desktop = CodexDesktopSelector.SelectPreferred(discovery.DesktopClients);
            if (desktop is null)
            {
                MessageBox.Show("重新扫描后仍未发现 ChatGPT/Codex Desktop，无法执行界面语言切换。", "无法切换语言", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!desktop.IsRunning || desktop.ProcessIds.Count == 0)
            {
                new RepairService().StartCodexDesktop(desktop.ExecutablePath);
                await Task.Delay(1400);
                discovery = await discoveryService.ScanAsync();
                desktop = CodexDesktopSelector.SelectPreferred(discovery.DesktopClients);
                if (desktop is null || !desktop.IsRunning || desktop.ProcessIds.Count == 0)
                {
                    MessageBox.Show("已尝试启动 Desktop，但重新扫描没有获得可绑定的运行窗口，未执行语言切换。", "无法绑定 Desktop", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var languageService = new CodexLanguageService();
            var switcher = new DesktopLanguageSwitchService(
                new CodexTrustedLanguageAdapter(languageService),
                new WindowsUiAutomationLanguageBackend(),
                new DesktopRestartBackend());

            var result = await switcher.SwitchAsync(discovery, target);
            if (!result.Success || !result.Verified)
            {
                MessageBox.Show(
                    $"未能确认已切换为 {targetName}。\n\n方式：{result.MethodZh}\n说明：{result.SummaryZh}\n\nCodex Doctor 不会通过修改未知数据库、MSIX/AppX 或二进制资源来伪造成功。",
                    "语言切换未通过验证",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show($"✓ 已验证 Codex/ChatGPT Desktop 界面切换为 {targetName}。", "语言切换完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            TriggerMainRescan(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show("一键语言切换失败：" + ex.Message, "语言切换失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

    private static void ResizeExistingButtons(FlowLayoutPanel panel)
    {
        foreach (var button in panel.Controls.OfType<Button>())
        {
            if (button.Text.Contains("启动 Codex", StringComparison.OrdinalIgnoreCase)) button.Width = 95;
            else if (button.Text.Contains("重启 Codex", StringComparison.OrdinalIgnoreCase)) button.Width = 95;
            else if (button.Text.Contains("修复", StringComparison.OrdinalIgnoreCase)) button.Width = 105;
            else if (button.Text.Contains("迁移", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("恢复", StringComparison.OrdinalIgnoreCase)) button.Width = 125;
            else if (button.Text.Contains("导出", StringComparison.OrdinalIgnoreCase)) button.Width = 115;
        }
    }

    private static Button CreateButton(string text, int width) => new()
    {
        Text = text,
        Size = new Size(width, 44),
        Margin = new Padding(4)
    };
}
