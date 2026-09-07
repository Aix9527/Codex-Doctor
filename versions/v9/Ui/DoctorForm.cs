using System.Diagnostics;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace CodexDoctor.V9;

public sealed class DoctorForm : Form
{
    private readonly TextBox _home = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _desktop = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListView _issues = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _status = new() { AutoSize = true, Text = "选择一项操作开始。扫描不会修改配置。", Dock = DockStyle.Fill };
    private readonly List<Button> _actions = [];
    private readonly Button _cancel = new() { Text = "取消当前操作", AutoSize = true, Enabled = false };
    private readonly List<string> _events = [];
    private CancellationTokenSource? _operation;
    private HealthSnapshot? _scan;
    private ReconnectResult? _repair;
    private string? _selectedDesktop;
    private readonly string _stateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexDoctorV9");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public DoctorForm()
    {
        Text = "Codex Doctor V9 · 诊断与修复"; Font = new Font("Microsoft YaHei UI", 10); Size = new Size(1120, 830); MinimumSize = new Size(920, 730); StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 247, 251);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new(SizeType.Absolute, 62)); layout.RowStyles.Add(new(SizeType.Absolute, 84)); layout.RowStyles.Add(new(SizeType.Absolute, 55)); layout.RowStyles.Add(new(SizeType.Absolute, 104)); layout.RowStyles.Add(new(SizeType.Absolute, 32)); layout.RowStyles.Add(new(SizeType.Percent, 60)); layout.RowStyles.Add(new(SizeType.Absolute, 25)); layout.RowStyles.Add(new(SizeType.Percent, 40));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Codex Doctor V9\n检测真实状态 · 修复前备份 · 执行后验证", AutoSize = true, Font = new Font(Font.FontFamily, 14, FontStyle.Bold) }, 0, 0);
        var paths = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        paths.ColumnStyles.Add(new(SizeType.Absolute, 145)); paths.ColumnStyles.Add(new(SizeType.Percent, 100)); paths.ColumnStyles.Add(new(SizeType.Absolute, 138));
        _home.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        paths.Controls.Add(new Label { Text = "Codex 数据目录", AutoSize = true }, 0, 0); paths.Controls.Add(_home, 1, 0);
        var chooseHome = Make("选择数据目录", () => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) { _home.Text = d.SelectedPath; _scan = null; _repair = null; } }); paths.Controls.Add(chooseHome, 2, 0);
        paths.Controls.Add(new Label { Text = "Desktop 客户端", AutoSize = true }, 0, 1); paths.Controls.Add(_desktop, 1, 1);
        paths.Controls.Add(Make("选择 Codex.exe", ChooseDesktop), 2, 1); layout.Controls.Add(paths, 0, 1);
        var primary = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        primary.Controls.Add(MakeAsync("一键扫描", ScanAsync));
        var reconnect = MakeAsync("一键修复重连", RepairAsync); reconnect.BackColor = Color.FromArgb(32, 104, 220); reconnect.ForeColor = Color.White; reconnect.FlatStyle = FlatStyle.Flat;
        primary.Controls.Add(reconnect); primary.Controls.Add(MakeAsync("启动 Codex", ct => StartAsync(false, ct))); primary.Controls.Add(MakeAsync("重启并应用代理", ct => StartAsync(true, ct)));
        _cancel.Click += (_, _) => _operation?.Cancel(); primary.Controls.Add(_cancel); layout.Controls.Add(primary, 0, 2);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        tools.Controls.Add(MakeAsync("安装/升级 CLI", ct => InstallAsync(false, false, ct)));
        tools.Controls.Add(MakeAsync("安装 Desktop", ct => InstallAsync(true, false, ct)));
        tools.Controls.Add(MakeAsync("卸载 CLI", ct => InstallAsync(false, true, ct)));
        tools.Controls.Add(MakeAsync("卸载 Desktop", ct => InstallAsync(true, true, ct)));
        tools.Controls.Add(MakeAsync("修复 CLI PATH", RepairPathAsync));
        tools.Controls.Add(MakeAsync("修复配置只读", RepairReadOnlyAsync));
        tools.Controls.Add(MakeAsync("清理 Git 代理", ct => ClearToolAsync(true, ct)));
        tools.Controls.Add(MakeAsync("清理 npm 代理", ct => ClearToolAsync(false, ct)));
        tools.Controls.Add(MakeAsync("迁移数据", MigrateAsync)); tools.Controls.Add(MakeAsync("恢复迁移", RestoreMigrationAsync));
        tools.Controls.Add(MakeAsync("切换中文", ct => LanguageAsync("zh-CN", ct)));
        tools.Controls.Add(MakeAsync("切换英文", ct => LanguageAsync("en-US", ct)));
        tools.Controls.Add(MakeAsync("撤销上次代理修复", UndoAsync)); tools.Controls.Add(Make("导出报告", Export));
        tools.Controls.Add(Make("官方服务状态", () => Process.Start(new ProcessStartInfo("https://status.openai.com/") { UseShellExecute = true })));
        layout.Controls.Add(tools, 0, 3); layout.Controls.Add(_status, 0, 4);
        _issues.Columns.Add("检查项", 175); _issues.Columns.Add("状态", 150); _issues.Columns.Add("结果与下一步", 680);
        layout.Controls.Add(_issues, 0, 5); layout.Controls.Add(new Label { Text = "操作记录", AutoSize = true }, 0, 6); layout.Controls.Add(_log, 0, 7);
        FormClosing += (_, e) => { if (_operation is not null) { e.Cancel = true; _operation.Cancel(); Log("正在取消；等待当前文件事务结束后再关闭窗口。"); } };
    }

    private Button Make(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 36, Padding = new Padding(6, 3, 6, 3), Margin = new Padding(3) };
        b.Click += (_, _) => { try { action(); } catch (Exception ex) { ShowError(ex); } }; _actions.Add(b); return b;
    }
    private Button MakeAsync(string text, Func<CancellationToken, Task> action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 36, Padding = new Padding(6, 3, 6, 3), Margin = new Padding(3) };
        b.Click += async (_, _) => await RunAsync(action); _actions.Add(b); return b;
    }
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_operation is not null) return;
        using var cts = new CancellationTokenSource(); _operation = cts;
        _actions.ForEach(b => b.Enabled = false); _cancel.Enabled = true; _home.Enabled = false; _desktop.Enabled = false;
        try { await action(cts.Token); }
        catch (OperationCanceledException) { Log("操作已取消。文件事务已结束；可重新扫描确认当前状态。"); }
        catch (Exception ex) { ShowError(ex); }
        finally { _operation = null; _actions.ForEach(b => b.Enabled = true); _cancel.Enabled = false; _home.Enabled = true; _desktop.Enabled = true; }
    }
    private void Log(string message) { var line = DateTime.Now.ToString("HH:mm:ss") + "  " + message; _events.Add(line); _log.AppendText(line + Environment.NewLine); _status.Text = message; }
    private void ShowError(Exception ex) { var detail = ex is IOException or ArgumentException or UnauthorizedAccessException ? ex.Message : "操作异常（" + ex.GetType().Name + "），请导出报告。"; Log(detail); MessageBox.Show(this, detail, "未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private string Home() => Path.GetFullPath(_home.Text.Trim());
    private bool Confirm(string message) => MessageBox.Show(this, message, "确认操作范围", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private DesktopInstallation Desktop() => _desktop.SelectedItem as DesktopInstallation ?? throw new IOException("请先扫描客户端或选择实际 Codex.exe。");
    private void ChooseDesktop()
    {
        using var dialog = new OpenFileDialog { Filter = "客户端|Codex.exe;ChatGPT.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!DesktopService.IsDesktopExecutable(dialog.FileName)) throw new IOException("请选择 Codex Desktop 图形主程序，不是 CLI 或 Doctor。");
        _selectedDesktop = dialog.FileName; RenderDesktops(DesktopService.Discover(_selectedDesktop));
    }
    private void RenderDesktops(IReadOnlyList<DesktopInstallation> desktops) { _desktop.Items.Clear(); foreach (var d in desktops) _desktop.Items.Add(d); if (_desktop.Items.Count > 0) _desktop.SelectedIndex = 0; }
    private async Task ScanAsync(CancellationToken ct)
    {
        var home = Home(); var selected = _selectedDesktop; var progress = new Progress<string>(Log);
        _scan = await Task.Run(() => HealthService.ScanAsync(home, selected, progress, ct), ct);
        RenderDesktops(_scan.Desktops); _issues.Items.Clear();
        foreach (var item in _scan.Items) _issues.Items.Add(new ListViewItem([item.Item, item.Status, item.Detail]));
        Log("扫描完成。检查结果已显示；没有修改系统或 Codex 配置。");
    }
    private async Task RepairAsync(CancellationToken ct)
    {
        var home = Home(); var progress = new Progress<string>(Log);
        var config = new ProxyConfiguration(home);
        Log("开始一键修复重连：寻找本机有效代理端口……");
        _repair = await Task.Run(() => new ReconnectRepair(config, new LocalProxyDiscovery()).RunAsync(LocalProxyDiscovery.Candidates(config), progress, ct), ct);
        Log(_repair.Summary);
        if (_repair.Change is { Changed: true } change) { Directory.CreateDirectory(_stateRoot); File.WriteAllText(Path.Combine(_stateRoot, "last-proxy-repair.json"), JsonSerializer.Serialize(change, Json)); if (change.BackupPath is not null) Log("原配置备份：" + change.BackupPath); }
        _issues.Items.Clear();
        foreach (var probe in _repair.Search.Probes) _issues.Items.Add(new ListViewItem([probe.Address, probe.Reachable ? "HTTPS 已验证" : "未通过", probe.Detail]));
        MessageBox.Show(this, _repair.Summary, _repair.ConfigurationVerified ? "代理配置完成" : "需要处理", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private async Task StartAsync(bool restart, CancellationToken ct)
    {
        var desktop = Desktop(); var candidates = new ProxyConfiguration(Home()).ReadCandidates(); var proxy = candidates.FirstOrDefault();
        if (proxy is not null && !new ProxyConfiguration(Home()).Matches(proxy)) throw new IOException("HTTP_PROXY 与 HTTPS_PROXY 配置不一致，请先执行一键修复重连。");
        if (restart && !Confirm("将请求 Codex 正常退出，然后携带代理环境重新启动。请先保存正在运行的任务。继续？")) return;
        if (proxy is not null && !(await LocalProxyDiscovery.ProbeAsync(proxy, ct)).Reachable) throw new IOException("配置中的代理当前不可用，请先执行一键修复重连。");
        await DesktopService.StartAsync(desktop, restart, proxy, ct); Log("已确认客户端进程启动。请在客户端验证实际连接状态。");
    }
    private async Task InstallAsync(bool desktop, bool uninstall, CancellationToken ct)
    {
        if (!Confirm($"将{(uninstall ? "卸载" : "安装/升级")} {(desktop ? "官方 Desktop 包" : "Codex CLI")}。此操作由 {(desktop ? "winget / Microsoft Store" : "npm 官方包 @openai/codex")} 执行。继续？")) return;
        Log("正在执行官方安装管理命令，请等待……");
        Log(desktop ? await InstallationService.ChangeDesktopAsync(uninstall, ct) : await InstallationService.ChangeCliAsync(uninstall, ct));
    }
    private Task RepairPathAsync(CancellationToken ct)
    {
        var cli = InstallationService.CliPath() ?? throw new IOException("没有找到 CLI 安装入口，请先安装 CLI。");
        var directory = Path.GetDirectoryName(cli)!; var before = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var after = InstallationService.AddUserPath(directory, before); if (before == after) { Log("用户 PATH 已包含 CLI 目录。"); return Task.CompletedTask; }
        if (!Confirm("将把以下目录添加到用户 PATH，并备份原 PATH：\n" + directory)) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(_stateRoot);
        var backup = Path.Combine(_stateRoot, "user-path-" + Guid.NewGuid().ToString("N") + ".txt"); File.WriteAllText(backup, before);
        Environment.SetEnvironmentVariable("PATH", after, EnvironmentVariableTarget.User);
        if (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) != after) { Environment.SetEnvironmentVariable("PATH", before, EnvironmentVariableTarget.User); throw new IOException("用户 PATH 写入复检失败，已尝试恢复。"); }
        Environment.SetEnvironmentVariable("PATH", InstallationService.AddUserPath(directory, Environment.GetEnvironmentVariable("PATH") ?? ""));
        Log("用户 PATH 已补齐。新终端生效；原 PATH 备份：" + backup); return Task.CompletedTask;
    }
    private async Task ClearToolAsync(bool git, CancellationToken ct)
    {
        if (!Confirm($"将备份并清理 {(git ? "Git" : "npm")} 默认用户配置中的 HTTP/HTTPS 代理。该配置可能供其他项目使用。继续？")) return;
        Log(await ToolProxyService.ClearAsync(git, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ct));
    }
    private Task RepairReadOnlyAsync(CancellationToken ct)
    {
        var files = new[] { ".env", "config.toml" }.Select(name => Path.Combine(Home(), name)).Where(File.Exists).Where(path => File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly)).ToArray();
        if (files.Length == 0) { Log("没有发现带只读属性的已知配置文件。"); return Task.CompletedTask; }
        if (!Confirm("将移除以下配置文件的只读属性，保留文件内容；不会改写 ACL：\n" + string.Join("\n", files))) return Task.CompletedTask;
        foreach (var file in files) { ct.ThrowIfCancellationRequested(); ConfigurationPermissions.MakeWritable(file); Log(Path.GetFileName(file) + " 只读属性已移除，写入权限验证通过。"); }
        return Task.CompletedTask;
    }
    private DataMigration Migration() => new(Home(), Path.Combine(_stateRoot, "migration.json"));
    private async Task MigrateAsync(CancellationToken ct)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择迁移目标父目录；将在其下创建 .codex" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var target = Path.Combine(dialog.SelectedPath, ".codex");
        if (!Confirm("将迁移到：\n" + target + "\n所有 Codex 进程必须先退出。将校验文件、保留原目录备份并创建 Junction。继续？")) return;
        Log("正在复制并逐文件校验，数据量大时需要较长时间……"); await Migration().MigrateAsync(target, ct); Log("迁移完成，文件校验与 Junction 目标检查通过。");
    }
    private async Task RestoreMigrationAsync(CancellationToken ct)
    {
        if (!Confirm("将按本工具的迁移记录恢复数据目录，保留目标副本。请先退出 Codex。继续？")) return;
        await Migration().RestoreAsync(ct); Log("迁移恢复完成，已使用目标目录的最新数据。");
    }
    private Task UndoAsync(CancellationToken ct)
    {
        var path = Path.Combine(_stateRoot, "last-proxy-repair.json"); if (!File.Exists(path)) throw new IOException("没有本工具保存的代理修复记录。");
        var change = JsonSerializer.Deserialize<ConfigChange>(File.ReadAllText(path)) ?? throw new IOException("修复记录无效。");
        if (!Confirm("将撤销上次代理配置修复。若文件后来被其他程序修改，操作会拒绝覆盖。继续？")) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested(); new ProxyConfiguration(Home()).Rollback(change); File.Delete(path); Log("上次代理修复已撤销。"); return Task.CompletedTask;
    }
    private async Task LanguageAsync(string language, CancellationToken ct)
    {
        var d = Desktop();
        if (!Confirm("将尝试操作客户端自身的 Language 设置。请保持客户端窗口打开；没有可识别控件时会报告未完成。继续？")) return;
        await LanguageService.SetAsync(d, language, ct);
        Log("客户端语言控件已选中并复检：" + (language == "zh-CN" ? "简体中文" : "英文") + "。如应用提示重启，请保存任务后重启。");
    }
    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "JSON 报告|*.json", FileName = "CodexDoctor-V9-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var report = new { 版本 = "9.0.0", 时间 = DateTimeOffset.Now, 扫描 = _scan, 重连修复 = _repair, 操作记录 = _events, 说明 = "报告不包含配置原文或登录凭据；HTTPS 可用不等于实际会话恢复。" };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(report, Json)); Log("已导出报告：" + dialog.FileName);
    }
}
