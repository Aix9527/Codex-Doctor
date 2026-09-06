using System.Text;

namespace CodexDoctor.Native;

public sealed class MainForm : Form
{
    private readonly CodexHealthScanner _healthScanner = CodexHealthScanner.CreateDefault();
    private readonly RepairService _repair = new();
    private readonly MigrationService _migration = new();
    private readonly CodexLanguageService _language = new();

    private readonly Label _health = new();
    private readonly Label _summary = new();
    private readonly Label _progressText = new();
    private readonly ProgressBar _progress = new();
    private readonly ListView _issues = new();
    private readonly TextBox _log = new();
    private readonly TextBox _target = new();

    private readonly Button _scanButton;
    private readonly Button _startButton;
    private readonly Button _restartButton;
    private readonly Button _repairButton;
    private readonly Button _migrationButton;
    private readonly Button _languageButton;
    private readonly Button _reportButton;

    private CodexHealthScanResult? _lastScan;
    private RepairAndRescanResult? _lastRepair;
    private RepairPlan? _lastRepairPlan;
    private bool _busy;

    public MainForm()
    {
        Text = "Codex Doctor V8.1 原生维修中心";
        Width = 1120;
        Height = 900;
        MinimumSize = new Size(1020, 800);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        var title = new Label
        {
            Text = "Codex Doctor V8.1",
            Font = new Font("Microsoft YaHei UI", 23F, FontStyle.Bold),
            Location = new Point(28, 18),
            Size = new Size(520, 48)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "先扫描 → 问题分级 → 一键修复 → 自动复检",
            Location = new Point(32, 68),
            Size = new Size(600, 25)
        };
        Controls.Add(subtitle);

        var about = new Button
        {
            Text = "关于",
            Location = new Point(1000, 22),
            Size = new Size(72, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        about.Click += (_, _) => { using var form = new AboutForm(); form.ShowDialog(this); };
        Controls.Add(about);

        _scanButton = new Button
        {
            Text = "一键扫描 Codex",
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            Location = new Point(32, 105),
            Size = new Size(250, 54)
        };
        _scanButton.Click += async (_, _) => await ScanAsync();
        Controls.Add(_scanButton);

        _progress.Location = new Point(300, 119);
        _progress.Size = new Size(500, 22);
        _progress.Minimum = 0;
        _progress.Maximum = 22;
        Controls.Add(_progress);

        _progressText.Text = "等待扫描";
        _progressText.Location = new Point(815, 115);
        _progressText.Size = new Size(260, 30);
        Controls.Add(_progressText);

        _health.Text = "尚未扫描：请先点击“一键扫描 Codex”";
        _health.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
        _health.Location = new Point(32, 176);
        _health.Size = new Size(1040, 34);
        Controls.Add(_health);

        _summary.Text = "严重 0 ｜ 紧急 0 ｜ 警告 0 ｜ 提示 0 ｜ 正常 0";
        _summary.Location = new Point(34, 214);
        _summary.Size = new Size(900, 28);
        Controls.Add(_summary);

        var actionPanel = new FlowLayoutPanel
        {
            Location = new Point(28, 252),
            Size = new Size(1048, 56),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Controls.Add(actionPanel);

        _startButton = AddActionButton(actionPanel, "启动 Codex", 145);
        _restartButton = AddActionButton(actionPanel, "重启 Codex", 145);
        _repairButton = AddActionButton(actionPanel, "一键修复", 150);
        _migrationButton = AddActionButton(actionPanel, "智能迁移/恢复", 170);
        _languageButton = AddActionButton(actionPanel, "一键中文", 145);
        _reportButton = AddActionButton(actionPanel, "导出完整报告", 165);

        _startButton.Click += async (_, _) => await StartCodexAsync();
        _restartButton.Click += async (_, _) => await RestartCodexAsync();
        _repairButton.Click += async (_, _) => await RepairAllAsync();
        _migrationButton.Click += async (_, _) => await SmartMigrationAsync();
        _languageButton.Click += async (_, _) => await SetChineseAsync();
        _reportButton.Click += (_, _) => ExportReport();

        Controls.Add(new Label
        {
            Text = "迁移目标：",
            Location = new Point(32, 318),
            Size = new Size(82, 26)
        });
        _target.Text = @"D:\Codex";
        _target.Location = new Point(116, 314);
        _target.Size = new Size(300, 27);
        Controls.Add(_target);

        _issues.Location = new Point(32, 356);
        _issues.Size = new Size(1040, 270);
        _issues.View = View.Details;
        _issues.FullRowSelect = true;
        _issues.GridLines = true;
        _issues.Columns.Add("级别", 72);
        _issues.Columns.Add("问题", 285);
        _issues.Columns.Add("状态", 120);
        _issues.Columns.Add("说明", 520);
        _issues.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_issues);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F);
        _log.Location = new Point(32, 642);
        _log.Size = new Size(1040, 160);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_log);

        var author = new Label
        {
            Text = "软件作者：Aix ｜ QQ：976936105 ｜ 抖音：xch03209527",
            Location = new Point(32, 816),
            Size = new Size(700, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        Controls.Add(author);

        ApplyDashboardState(MainDashboardState.From(null));
    }

    private static Button AddActionButton(FlowLayoutPanel panel, string text, int width)
    {
        var button = new Button { Text = text, Size = new Size(width, 44), Margin = new Padding(4, 4, 4, 4) };
        panel.Controls.Add(button);
        return button;
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        SetBusy(true);
        _progress.Value = 0;
        _progressText.Text = "正在准备扫描……";
        WriteLog("开始 V8.1 全量健康扫描……");

        try
        {
            var progress = new Progress<HealthScanProgress>(p =>
            {
                _progress.Maximum = Math.Max(1, p.Total);
                _progress.Value = Math.Min(_progress.Maximum, p.Completed);
                _progressText.Text = $"{p.Completed}/{p.Total} {p.StageNameZh}";
            });

            _lastScan = await _healthScanner.ScanAsync(progress);
            _lastRepair = null;
            _lastRepairPlan = null;
            RenderScan(_lastScan);
            WriteLog($"扫描完成：ScanId={_lastScan.ScanId}；发现 {_lastScan.Issues.Count} 项检查结果。");
        }
        catch (Exception ex)
        {
            WriteLog("扫描失败：" + ex.Message);
            MessageBox.Show(ex.Message, "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            ApplyDashboardState(MainDashboardState.From(_lastScan));
        }
    }

    private void RenderScan(CodexHealthScanResult scan)
    {
        var state = MainDashboardState.From(scan);
        _summary.Text = $"严重 {state.CriticalCount} ｜ 紧急 {state.UrgentCount} ｜ 警告 {state.WarningCount} ｜ 提示 {state.InfoCount} ｜ 正常 {state.OkCount}";

        var top = scan.Issues.FirstOrDefault(x => x.Severity != CodexIssueSeverity.Ok);
        if (top is null)
        {
            _health.Text = "● 当前扫描未发现需要处理的问题";
            _health.ForeColor = Color.ForestGreen;
        }
        else
        {
            _health.Text = $"● {top.SeverityZh}：{top.TitleZh}";
            _health.ForeColor = top.Severity switch
            {
                CodexIssueSeverity.Critical => Color.Firebrick,
                CodexIssueSeverity.Urgent => Color.DarkRed,
                CodexIssueSeverity.Warning => Color.DarkOrange,
                _ => Color.DimGray
            };
        }

        _issues.BeginUpdate();
        _issues.Items.Clear();
        foreach (var issue in scan.Issues)
        {
            var item = new ListViewItem(issue.SeverityZh);
            item.SubItems.Add(issue.TitleZh);
            item.SubItems.Add(issue.StatusZh);
            item.SubItems.Add(issue.SummaryZh);
            item.Tag = issue;
            _issues.Items.Add(item);
        }
        _issues.EndUpdate();
        ApplyDashboardState(state);
    }

    private void ApplyDashboardState(MainDashboardState state)
    {
        _scanButton.Enabled = !_busy;
        _scanButton.Font = new Font("Microsoft YaHei UI", state.ShowScanPrimary ? 12F : 10F, state.ShowScanPrimary ? FontStyle.Bold : FontStyle.Regular);
        _startButton.Enabled = !_busy && state.CanStart;
        _restartButton.Enabled = !_busy && state.CanRestart;
        _repairButton.Enabled = !_busy && state.CanRepair;
        _migrationButton.Enabled = !_busy && _lastScan is not null;
        _languageButton.Enabled = !_busy && _lastScan is not null;
        _reportButton.Enabled = !_busy && state.CanExportReport;
        _repairButton.Text = state.RepairableCount > 0 ? $"一键修复 ({state.RepairableCount})" : "一键修复";

        if (_lastScan is null)
        {
            _migrationButton.Text = state.MigrationActionZh;
            _languageButton.Text = state.LanguageActionZh;
        }
        else
        {
            _migrationButton.Text = CurrentMigrationDecision().ActionZh;
            _languageButton.Text = CurrentLanguageDecision().ActionZh;
        }
    }

    private CodexDesktopInstallationInfo? PreferredDesktop()
    {
        var candidates = _lastScan?.Discovery.DesktopClients
            .Where(x => File.Exists(x.ExecutablePath))
            .ToArray() ?? [];
        return CodexDesktopSelector.SelectPreferred(candidates);
    }

    private MigrationActionDecision CurrentMigrationDecision()
    {
        if (_lastScan is null)
            return new MigrationActionDecision(MigrationActionKind.ViewDetails, "智能迁移/恢复", false, "请先扫描 Codex。");
        return MigrationActionResolver.Resolve(
            _lastScan.Discovery.DataDirectory,
            _migration.HasMigrationState,
            _migration.CanRecoverInterrupted());
    }

    private LanguageActionDecision CurrentLanguageDecision()
    {
        if (_lastScan is null)
            return new LanguageActionDecision("一键中文", false, false, true, "请先扫描 Codex。");
        var detected = _language.Detect(_lastScan.Discovery);
        return LanguageActionResolver.Resolve(detected);
    }

    private async Task StartCodexAsync()
    {
        var desktop = PreferredDesktop();
        if (desktop is null)
        {
            MessageBox.Show("扫描结果中没有可启动的 Codex Desktop 实际路径。", "无法启动", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _repair.StartCodexDesktop(desktop.ExecutablePath);
            WriteLog("已通过扫描确认的实际路径启动 Codex：" + desktop.ExecutablePath);
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动 Codex 失败：" + ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RestartCodexAsync()
    {
        var desktop = PreferredDesktop();
        if (desktop is null)
        {
            MessageBox.Show("扫描结果中没有可重启的 Codex Desktop 实际路径。", "无法重启", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show($"将通过扫描确认的实际路径重启：\n{desktop.ExecutablePath}\n\n是否继续？", "确认重启 Codex", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            _repair.RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds);
            WriteLog("已重启 Codex Desktop。");
            await Task.Delay(800);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "重启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RepairAllAsync()
    {
        if (_lastScan is null || _busy) return;

        var actions = new List<IRepairAction>();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexDoctorV8", "repair-backups");

        if (_lastScan.Diagnosis?.Proxy.Ok == true && !string.IsNullOrWhiteSpace(_lastScan.Diagnosis.ProxyUrl))
            actions.Add(new ProxyEnvRepairAction(profile, _lastScan.Diagnosis.ProxyUrl));
        actions.Add(new GitProxyRepairAction(new GitProxyToolBackend(), backupRoot));
        actions.Add(new NpmProxyRepairAction(new NpmProxyToolBackend(), backupRoot));

        var languageDecision = CurrentLanguageDecision();
        if (languageDecision.CanApply)
            actions.Add(new LanguageRepairAction(_language, _lastScan.Discovery));

        var scannerAdapter = new CodexHealthScannerAdapter(_healthScanner);
        var engine = new CodexRepairEngine(new RepairActionCatalog(actions), scannerAdapter);
        var plan = engine.BuildPlan(_lastScan);
        if (plan.Actions.Count == 0)
        {
            MessageBox.Show("当前扫描结果没有符合安全白名单且可验证的自动修复动作。需要人工或外部处理的问题不会被伪报为已修复。", "没有安全自动修复项", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var lines = string.Join("\n", plan.Actions.Select(x => "• " + x.TitleZh));
        var confirm = $"将执行以下经过白名单确认的修复：\n\n{lines}\n\n每项会执行后验证；需要备份的项目会先备份；验证失败将回滚。完成后会自动执行全量复检。是否继续？";
        if (MessageBox.Show(confirm, "确认一键修复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _lastRepairPlan = plan;
        SetBusy(true);
        try
        {
            WriteLog($"开始执行 RepairPlan：{plan.PlanId}");
            _lastRepair = await engine.ExecuteAndRescanAsync(plan, _lastScan);
            foreach (var action in _lastRepair.Repair.Actions)
                WriteLog($"{action.TitleZh}：{action.StatusZh}；{action.SummaryZh}");
            _lastScan = _lastRepair.AfterScan;
            RenderScan(_lastScan);
            WriteLog($"修复后全量复检完成：ScanId={_lastScan.ScanId}");
        }
        catch (Exception ex)
        {
            WriteLog("一键修复失败：" + ex.Message);
            MessageBox.Show(ex.Message, "修复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            ApplyDashboardState(MainDashboardState.From(_lastScan));
        }
    }

    private async Task SmartMigrationAsync()
    {
        if (_lastScan is null) return;
        var decision = MigrationActionResolver.Resolve(
            _lastScan.Discovery.DataDirectory,
            _migration.HasMigrationState,
            _migration.CanRecoverInterrupted());

        if (decision.Kind == MigrationActionKind.ViewDetails)
        {
            var state = _migration.ReadMigrationState();
            var details = new StringBuilder()
                .AppendLine(decision.ExplanationZh)
                .AppendLine()
                .AppendLine("当前 .codex：" + _lastScan.Discovery.DataDirectory.Path)
                .AppendLine("链接目标：" + (_lastScan.Discovery.DataDirectory.LinkTarget ?? "无/未知"))
                .AppendLine("迁移状态：" + (_migration.HasMigrationState ? "存在" : "不存在"));
            if (state is not null)
            {
                details.AppendLine("状态源：" + state.Source)
                    .AppendLine("状态目标：" + state.Target)
                    .AppendLine("历史备份：" + (string.IsNullOrWhiteSpace(state.Backup) ? "无" : state.Backup));
            }
            details.AppendLine().Append("为避免误删或覆盖，当前状态不会自动执行迁移/恢复。");
            MessageBox.Show(details.ToString(), "迁移详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (decision.Kind == MigrationActionKind.Restore)
        {
            if (MessageBox.Show("检测到有效 .codex Junction 和可审计迁移状态。将恢复为普通目录并保留目标数据副本。是否继续？", "确认恢复 .codex", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                _migration.Restore();
                WriteLog(".codex 已恢复为普通目录。");
                await ScanAsync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return;
        }

        if (decision.Kind == MigrationActionKind.Recover)
        {
            if (MessageBox.Show("检测到可证明安全恢复的中断迁移事务。将按状态文件重建 Junction，不会覆盖非空普通目录。是否继续？", "确认恢复迁移事务", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                var state = _migration.RecoverInterrupted();
                WriteLog($"已恢复中断迁移事务：{state.Source} → {state.Target}");
                await ScanAsync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "迁移事务恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return;
        }

        var target = _target.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show("请先填写迁移目标，例如 D:\\Codex。", "需要迁移目标", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"准备把 .codex 安全迁移到：\n{target}\n\n首次无历史目标时默认建议 D:\\Codex；本次将复制、备份并创建 Junction。是否继续？", "确认迁移 .codex", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            var state = _migration.Migrate(target);
            WriteLog($"迁移完成：{state.Source} → {state.Target}");
            await ScanAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "迁移失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task SetChineseAsync()
    {
        if (_lastScan is null) return;
        var state = _language.Detect(_lastScan.Discovery);
        var decision = LanguageActionResolver.Resolve(state);

        if (decision.AlreadyChinese)
        {
            MessageBox.Show("当前可信语言状态已经是简体中文。", "已是中文", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (decision.NeedsUserAction)
        {
            MessageBox.Show(decision.ExplanationZh + "\n\nCodex Doctor 不会修改未知内部数据库、MSIX/AppX 或二进制资源。", "需要用户操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!decision.CanApply) return;

        if (MessageBox.Show("检测到经过批准的可逆语言适配器，可自动设置为简体中文。是否继续？", "确认一键中文", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        try
        {
            var applied = _language.ApplySimplifiedChinese(_lastScan.Discovery);
            WriteLog("中文设置：" + applied.MethodZh);
            await ScanAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "中文设置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ExportReport()
    {
        if (_lastScan is null) return;
        try
        {
            var options = ReportSaveDefaults.Create(DateTime.Now);
            using var dialog = new SaveFileDialog
            {
                Title = "导出完整报告",
                InitialDirectory = options.InitialDirectory,
                FileName = options.FileName,
                Filter = options.Filter,
                DefaultExt = options.DefaultExt,
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var exporter = new HealthReportExporter();
            var file = exporter.Export(dialog.FileName, _lastScan, _lastRepair, _lastRepairPlan);
            WriteLog("隐私安全完整报告已导出：" + file);
            MessageBox.Show(file, "报告已导出", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        ApplyDashboardState(MainDashboardState.From(_lastScan));
    }

    private void WriteLog(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.Text.Length;
        _log.ScrollToCaret();
    }
}
