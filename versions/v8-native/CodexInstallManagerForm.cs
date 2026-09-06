namespace CodexDoctor.Native;

public sealed class CodexInstallManagerForm : Form
{
    private readonly IInstallDiscoveryProvider _discovery;
    private readonly CodexInstallManager _manager;

    private readonly Label _desktopStatus = new();
    private readonly Label _cliStatus = new();
    private readonly Label _activity = new();
    private readonly Button _installDesktop = new();
    private readonly Button _uninstallDesktop = new();
    private readonly Button _installCli = new();
    private readonly Button _uninstallCli = new();
    private readonly Button _close = new();

    private bool _busy;

    public bool Changed { get; private set; }

    public CodexInstallManagerForm()
    {
        Text = "Codex 安装 / 卸载";
        Width = 700;
        Height = 470;
        MinimumSize = new Size(660, 430);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9F);

        _discovery = new InstallDiscoveryProvider();
        _manager = new CodexInstallManager(new InstallCommandRunner(), _discovery);

        Controls.Add(new Label
        {
            Text = "Codex 安装管理",
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            Location = new Point(28, 22),
            Size = new Size(500, 40)
        });
        Controls.Add(new Label
        {
            Text = "Desktop 与 CLI 是两个独立客户端；只使用官方 Windows / npm 安装来源。",
            Location = new Point(31, 68),
            Size = new Size(620, 28)
        });

        var desktopGroup = new GroupBox
        {
            Text = "ChatGPT / Codex Desktop",
            Location = new Point(28, 108),
            Size = new Size(625, 120)
        };
        Controls.Add(desktopGroup);
        _desktopStatus.Location = new Point(18, 28);
        _desktopStatus.Size = new Size(585, 28);
        _desktopStatus.Text = "状态：正在重新扫描……";
        desktopGroup.Controls.Add(_desktopStatus);
        ConfigureButton(_installDesktop, "安装 Desktop", 18, 66, 150);
        ConfigureButton(_uninstallDesktop, "卸载 Desktop", 182, 66, 150);
        _installDesktop.Click += async (_, _) => await InstallDesktopAsync();
        _uninstallDesktop.Click += async (_, _) => await UninstallDesktopAsync();
        desktopGroup.Controls.Add(_installDesktop);
        desktopGroup.Controls.Add(_uninstallDesktop);

        var cliGroup = new GroupBox
        {
            Text = "Codex CLI",
            Location = new Point(28, 242),
            Size = new Size(625, 120)
        };
        Controls.Add(cliGroup);
        _cliStatus.Location = new Point(18, 28);
        _cliStatus.Size = new Size(585, 28);
        _cliStatus.Text = "状态：正在重新扫描……";
        cliGroup.Controls.Add(_cliStatus);
        ConfigureButton(_installCli, "安装 CLI", 18, 66, 150);
        ConfigureButton(_uninstallCli, "卸载 CLI", 182, 66, 150);
        _installCli.Click += async (_, _) => await InstallCliAsync();
        _uninstallCli.Click += async (_, _) => await UninstallCliAsync();
        cliGroup.Controls.Add(_installCli);
        cliGroup.Controls.Add(_uninstallCli);

        _activity.Location = new Point(31, 375);
        _activity.Size = new Size(510, 30);
        _activity.Text = "准备就绪";
        Controls.Add(_activity);

        ConfigureButton(_close, "关闭", 550, 372, 103);
        _close.Click += (_, _) => Close();
        Controls.Add(_close);

        Shown += async (_, _) => await RefreshStatusAsync();
        FormClosing += (_, e) =>
        {
            if (_busy)
            {
                e.Cancel = true;
                MessageBox.Show("安装/卸载正在执行，请等待当前安全操作完成。", "正在执行", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
    }

    private static void ConfigureButton(Button button, string text, int x, int y, int width)
    {
        button.Text = text;
        button.Location = new Point(x, y);
        button.Size = new Size(width, 36);
    }

    private async Task RefreshStatusAsync()
    {
        _activity.Text = "正在重新扫描 Codex 安装状态……";
        SetBusy(true, preserveClose: true);
        try
        {
            var scan = await _discovery.ScanAsync();
            var desktop = CodexDesktopSelector.SelectPreferred(scan.DesktopClients);
            if (desktop is null)
            {
                _desktopStatus.Text = "状态：未安装 / 未发现 Desktop";
                _installDesktop.Enabled = true;
                _uninstallDesktop.Enabled = false;
            }
            else
            {
                _desktopStatus.Text = $"状态：已安装 ｜ 版本：{desktop.Version ?? "未知"} ｜ 来源：{desktop.Source}";
                _installDesktop.Enabled = false;
                _uninstallDesktop.Enabled = true;
            }

            if (!scan.Cli.Found)
            {
                _cliStatus.Text = "状态：未安装 / 未发现 Codex CLI";
                _installCli.Enabled = true;
                _uninstallCli.Enabled = false;
            }
            else
            {
                _cliStatus.Text = $"状态：已安装 ｜ 版本：{scan.Cli.Version ?? "未知"} ｜ 路径：{scan.Cli.Path ?? "未知"}";
                _installCli.Enabled = false;
                _uninstallCli.Enabled = true;
            }
            _activity.Text = "重新扫描完成";
        }
        catch (Exception ex)
        {
            _desktopStatus.Text = "状态：扫描失败";
            _cliStatus.Text = "状态：扫描失败";
            _activity.Text = "重新扫描失败：" + ex.Message;
        }
        finally
        {
            SetBusy(false, preserveClose: true);
        }
    }

    private async Task InstallDesktopAsync()
    {
        if (MessageBox.Show(
                "将使用 Microsoft Store 官方包 ID 安装 ChatGPT / Codex Desktop。\n\n不会从第三方下载安装器。是否继续？",
                "确认安装 Desktop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

        await RunOperationAsync("正在安装 Desktop……", ct => _manager.InstallDesktopAsync(ct));
    }

    private async Task UninstallDesktopAsync()
    {
        var text = "将使用 Windows/winget 卸载 ChatGPT / Codex Desktop。\n\n" +
                   "默认保留 .codex\n" +
                   "保留用户项目\n" +
                   "保留 Codex Doctor 备份和报告\n\n" +
                   "不会直接删除应用目录，也不会执行彻底清理用户数据。是否继续？";
        if (MessageBox.Show(text, "确认卸载 Desktop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        await RunOperationAsync("正在卸载 Desktop……", ct => _manager.UninstallDesktopAsync(ct));
    }

    private async Task InstallCliAsync()
    {
        if (MessageBox.Show(
                "将使用 npm 官方包 @openai/codex 安装 Codex CLI。\n\n如果本机没有 npm，Codex Doctor 不会自动安装 Node.js。是否继续？",
                "确认安装 CLI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

        await RunOperationAsync("正在安装 Codex CLI……", ct => _manager.InstallCliAsync(ct));
    }

    private async Task UninstallCliAsync()
    {
        var text = "将使用 npm 卸载 Codex CLI。\n\n" +
                   "默认保留 .codex\n" +
                   "保留用户项目\n" +
                   "保留 Codex Doctor 备份和报告\n\n" +
                   "不会删除 Codex 用户数据。是否继续？";
        if (MessageBox.Show(text, "确认卸载 CLI", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        await RunOperationAsync("正在卸载 Codex CLI……", ct => _manager.UninstallCliAsync(ct));
    }

    private async Task RunOperationAsync(
        string activity,
        Func<CancellationToken, Task<InstallOperationResult>> operation)
    {
        if (_busy) return;
        SetBusy(true);
        _activity.Text = activity;
        try
        {
            var result = await operation(CancellationToken.None);
            if (result.Success && result.Verified)
            {
                Changed = true;
                MessageBox.Show(result.SummaryZh, "操作完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result.ManualRequired)
            {
                MessageBox.Show(result.SummaryZh, "需要用户操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(result.SummaryZh, "操作未通过验证", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "安装管理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            await RefreshStatusAsync();
        }
    }

    private void SetBusy(bool busy, bool preserveClose = false)
    {
        _busy = busy;
        UseWaitCursor = busy;
        if (busy)
        {
            _installDesktop.Enabled = false;
            _uninstallDesktop.Enabled = false;
            _installCli.Enabled = false;
            _uninstallCli.Enabled = false;
        }
        if (!preserveClose) _close.Enabled = !busy;
    }
}
