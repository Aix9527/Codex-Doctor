namespace CodexDoctor.Native;

public static class ReconnectingRecoveryEvidenceStore
{
    private static ReconnectingRecoveryResult? _latest;

    public static ReconnectingRecoveryResult? Latest => Volatile.Read(ref _latest);

    public static void Set(ReconnectingRecoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Volatile.Write(ref _latest, result);
    }
}

public static class ReconnectingRecoveryUiExtensions
{
    private sealed class EligibilityState
    {
        public bool Known;
        public bool Eligible;
        public bool Refreshing;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MainForm, EligibilityState> Eligibility = new();

    public static bool CanRunReconnectingRecovery(this MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var state = Eligibility.GetOrCreateValue(form);
        return state.Known && state.Eligible && !state.Refreshing;
    }

    public static async Task RefreshReconnectingRecoveryEligibilityAsync(this MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var state = Eligibility.GetOrCreateValue(form);
        if (state.Refreshing) return;
        state.Refreshing = true;
        try
        {
            var scan = await CodexHealthScanner.CreateDefault().ScanAsync().ConfigureAwait(true);
            state.Eligible = MainDashboardState.From(scan).CanRecoverReconnecting;
            state.Known = true;
        }
        catch
        {
            state.Eligible = false;
            state.Known = true;
        }
        finally
        {
            state.Refreshing = false;
        }
    }

    public static async Task RunReconnectingRecoveryAsync(this MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var state = Eligibility.GetOrCreateValue(form);
        if (state.Refreshing) return;

        state.Refreshing = true;
        form.UseWaitCursor = true;
        try
        {
            var scanner = CodexHealthScanner.CreateDefault();
            var beforeScan = await scanner.ScanAsync().ConfigureAwait(true);
            var dashboard = MainDashboardState.From(beforeScan);
            state.Eligible = dashboard.CanRecoverReconnecting;
            state.Known = true;

            if (!dashboard.CanRecoverReconnecting)
            {
                MessageBox.Show(
                    "当前 fresh 扫描没有满足 Reconnecting 自动自愈安全门。请先处理严重/紧急的人工或外部问题，并确认已经发现可信 Desktop。",
                    "当前不能自动自愈",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var desktop = CodexDesktopSelector.SelectPreferred(beforeScan.Discovery.DesktopClients);
            if (desktop is null)
            {
                MessageBox.Show("fresh 扫描未发现可信 Codex/ChatGPT Desktop，未执行任何自动变更。", "需要人工处理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var decision = ReconnectingRecoveryDecisionEngine.Decide(beforeScan.Diagnosis);
            var route = decision.SelectedProxyUrl is null ? "直连/现有可用路径" : decision.SelectedProxyUrl;
            var confirmation =
                "将执行 Reconnecting 端到端自愈闭环：\n\n" +
                "1. 使用 fresh 扫描事实选择网络路径\n" +
                "2. 只执行白名单、可验证、可回滚的安全动作\n" +
                "3. 通过扫描确认的真实 EXE/PID 重启 Desktop\n" +
                "4. fresh discovery + fresh 网络诊断双重验证\n\n" +
                $"当前网络路径：{route}\n" +
                $"Desktop：{desktop.ExecutablePath}\n\n" +
                "只有网络与 Desktop 双验证通过才会显示 RECOVERED。是否继续？";
            if (MessageBox.Show(confirmation, "确认 Reconnecting 自愈", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexDoctorV8",
                "repair-backups");
            var actions = new List<IRepairAction>();
            if (beforeScan.Diagnosis?.Proxy.Ok == true && !string.IsNullOrWhiteSpace(beforeScan.Diagnosis.ProxyUrl))
                actions.Add(new ProxyEnvRepairAction(profile, beforeScan.Diagnosis.ProxyUrl));
            actions.Add(new GitProxyRepairAction(new GitProxyToolBackend(), backupRoot));
            actions.Add(new NpmProxyRepairAction(new NpmProxyToolBackend(), backupRoot));

            var engine = new CodexRepairEngine(new RepairActionCatalog(actions));
            var discovery = new CodexDiscoveryService();
            var recovery = new ReconnectingRecoveryService(
                new CodexHealthScannerAdapter(scanner),
                new CodexRepairEngineReconnectingExecutor(engine),
                new DesktopRestartBackend(),
                _ => discovery.ScanAsync());

            var result = await recovery.RecoverAsync(beforeScan).ConfigureAwait(true);
            ReconnectingRecoveryEvidenceStore.Set(result);

            var actionSummary = result.Actions.Count == 0
                ? "没有需要执行的配置修复动作。"
                : string.Join(Environment.NewLine, result.Actions.Select(x => $"• {x.TitleZh}：{x.StatusZh}"));
            var details =
                $"状态：{result.StatusZh}\n" +
                $"网络验证：{(result.NetworkVerified ? "通过" : "未通过")}\n" +
                $"Desktop 验证：{(result.DesktopVerified ? "通过" : "未通过")}\n" +
                $"修复前 ScanId：{result.BeforeScanId}\n" +
                $"修复后 ScanId：{result.AfterScanId?.ToString() ?? "无"}\n\n" +
                result.SummaryZh + "\n\n" + actionSummary;

            var icon = result.Status == ReconnectingRecoveryStatus.Recovered
                ? MessageBoxIcon.Information
                : MessageBoxIcon.Warning;
            MessageBox.Show(details, "Reconnecting 自愈结果", MessageBoxButtons.OK, icon);

            TriggerMainRescan(form);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Reconnecting 自愈未完成：" + ex.Message + "\n\n未验证的状态不会被报告为 RECOVERED。",
                "Reconnecting 自愈失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            form.UseWaitCursor = false;
            state.Refreshing = false;
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
}
