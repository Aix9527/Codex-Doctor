using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CodexDoctor.Native;

public sealed class MainForm : Form
{
    private readonly DiagnosisService _diagnosis = new();
    private readonly RepairService _repair = new();
    private readonly MigrationService _migration = new();
    private readonly Label _health = new();
    private readonly Label _detail = new();
    private readonly TextBox _proxy = new();
    private readonly CheckBox _writeUserEnv = new();
    private readonly TextBox _target = new();
    private readonly TextBox _log = new();
    private DiagnosisResult? _last;

    public MainForm()
    {
        Text = "Codex Doctor V8 原生中文版";
        Width = 1040;
        Height = 800;
        MinimumSize = new Size(960, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        var title = new Label
        {
            Text = "Codex Doctor V8 原生版",
            Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            Location = new Point(28, 18),
            Size = new Size(650, 46)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "统一诊断 · 安全修复 · .codex 迁移/恢复 · 无 PowerShell 运行依赖",
            Location = new Point(32, 68),
            Size = new Size(850, 26)
        };
        Controls.Add(subtitle);

        _health.Text = "● 尚未诊断";
        _health.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
        _health.Location = new Point(32, 105);
        _health.Size = new Size(900, 34);
        Controls.Add(_health);

        _detail.Location = new Point(32, 145);
        _detail.Size = new Size(950, 70);
        Controls.Add(_detail);

        Controls.Add(new Label { Text = "代理：", Location = new Point(32, 218), Size = new Size(58, 26) });
        _proxy.Location = new Point(92, 214);
        _proxy.Size = new Size(315, 27);
        Controls.Add(_proxy);
        Controls.Add(new Label { Text = "留空 = 自动检测已验证代理", Location = new Point(92, 243), Size = new Size(260, 22) });

        _writeUserEnv.Text = "修复时同时写入 Windows 用户环境变量";
        _writeUserEnv.Checked = false;
        _writeUserEnv.Location = new Point(430, 214);
        _writeUserEnv.Size = new Size(345, 28);
        Controls.Add(_writeUserEnv);

        Controls.Add(new Label { Text = "迁移目标：", Location = new Point(32, 278), Size = new Size(82, 26) });
        _target.Text = @"D:\Codex";
        _target.Location = new Point(116, 274);
        _target.Size = new Size(290, 27);
        Controls.Add(_target);

        var diagnose = AddButton("一键诊断", 32, 320, 135);
        var repair = AddButton("修复建议项", 182, 320, 145);
        var restart = AddButton("重启 Codex", 342, 320, 135);
        var migrate = AddButton("迁移 .codex", 492, 320, 135);
        var restore = AddButton("恢复 .codex", 642, 320, 135);
        var report = AddButton("导出报告", 792, 320, 135);

        var doctor = AddButton("运行 codex doctor", 32, 372, 175);
        var clearGit = AddButton("清理 Git 代理", 222, 372, 150);
        var clearNpm = AddButton("清理 npm 代理", 387, 372, 150);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F);
        _log.Location = new Point(32, 435);
        _log.Size = new Size(950, 280);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_log);

        diagnose.Click += async (_, _) => await DiagnoseAsync();
        repair.Click += async (_, _) => await RepairAsync();
        restart.Click += (_, _) => RestartCodex();
        migrate.Click += (_, _) => Migrate();
        restore.Click += (_, _) => Restore();
        report.Click += (_, _) => ExportReport();
        doctor.Click += (_, _) => RunDoctor();
        clearGit.Click += (_, _) => ClearGit();
        clearNpm.Click += (_, _) => ClearNpm();
    }

    private Button AddButton(string text, int x, int y, int width)
    {
        var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 42) };
        Controls.Add(button);
        return button;
    }

    private async Task DiagnoseAsync()
    {
        SetBusy(true);
        try
        {
            WriteLog("开始执行网络诊断……");
            _last = await _diagnosis.DiagnoseAsync(_proxy.Text.Trim());
            if (string.IsNullOrWhiteSpace(_proxy.Text) && !string.IsNullOrWhiteSpace(_last.ProxyUrl)) _proxy.Text = _last.ProxyUrl;
            UpdateResult(_last);
            WriteLog($"诊断完成：{_last.FailureNameZh}；健康状态：{HealthZh(_last.Health)}；代理：{Display(_last.ProxyUrl)}");
        }
        catch (Exception ex)
        {
            WriteLog("诊断失败：" + ex.Message);
            MessageBox.Show(ex.Message, "诊断失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    private async Task RepairAsync()
    {
        try
        {
            var result = await _diagnosis.DiagnoseAsync(_proxy.Text.Trim());
            _last = result;
            UpdateResult(result);
            if (result.FailureClass is FailureClass.ProxyRequired or FailureClass.ProxyMisconfigured)
            {
                if (!result.Proxy.Ok || string.IsNullOrWhiteSpace(result.ProxyUrl))
                {
                    MessageBox.Show("当前没有通过 HTTPS 验证的代理，不能自动写入 Codex 配置。", "无法修复", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var text = $"将把已验证代理写入：\n{_repair.EnvFile}\n\n代理：{result.ProxyUrl}\n\n是否继续？";
                if (MessageBox.Show(text, "确认修复", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var backup = _repair.WriteCodexProxyEnv(result.ProxyUrl, _writeUserEnv.Checked);
                WriteLog("已更新 Codex 专用代理配置。" + (string.IsNullOrWhiteSpace(backup) ? "" : $" 备份：{backup}"));
                MessageBox.Show("Codex 代理配置已更新。建议随后点击“重启 Codex”让桌面应用重新读取配置。", "修复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await DiagnoseAsync();
                return;
            }

            if (result.FailureClass == FailureClass.EnvironmentConflict)
            {
                MessageBox.Show("检测到 Git/npm 环境代理冲突。请使用下方“清理 Git 代理”或“清理 npm 代理”分别处理，避免误清理。", "需要人工确认", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(result.RecommendationZh, "当前没有自动修复动作", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            WriteLog("修复失败：" + ex.Message);
            MessageBox.Show(ex.Message, "修复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestartCodex()
    {
        if (MessageBox.Show("将关闭正在运行的 Codex/ChatGPT 进程并尝试重新启动。是否继续？", "确认重启", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            _repair.RestartCodexDesktop();
            WriteLog("已请求重启 Codex/ChatGPT。 ");
        }
        catch (Exception ex)
        {
            WriteLog("重启失败：" + ex.Message);
            MessageBox.Show(ex.Message, "重启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Migrate()
    {
        var target = _target.Text.Trim();
        var message = $"准备把 .codex 数据迁移到：\n{target}\n\n迁移会复制数据、备份原目录并创建 Junction。请先关闭 Codex。是否继续？";
        if (MessageBox.Show(message, "确认迁移", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            var state = _migration.Migrate(target);
            WriteLog($"迁移完成：{state.Source} → {state.Target}");
            MessageBox.Show(".codex 迁移完成。", "迁移完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            WriteLog("迁移失败：" + ex.Message);
            MessageBox.Show(ex.Message, "迁移失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Restore()
    {
        if (MessageBox.Show("将根据上次迁移状态恢复 .codex。是否继续？", "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            _migration.Restore();
            WriteLog(".codex 已恢复。 ");
            MessageBox.Show(".codex 已恢复。", "恢复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            WriteLog("恢复失败：" + ex.Message);
            MessageBox.Show(ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportReport()
    {
        if (_last is null)
        {
            MessageBox.Show("请先执行一次诊断。", "没有诊断结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexDoctorV8", "reports");
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, $"CodexDoctorV8-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(file, JsonSerializer.Serialize(_last, options), new UTF8Encoding(false));
            WriteLog("报告已导出：" + file);
            MessageBox.Show(file, "报告已导出", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunDoctor()
    {
        try
        {
            WriteLog("正在运行 codex doctor……");
            var output = _repair.RunCodexDoctor();
            WriteLog(string.IsNullOrWhiteSpace(output) ? "codex doctor 已执行，但没有返回文本。" : output);
        }
        catch (Exception ex)
        {
            WriteLog("运行 codex doctor 失败：" + ex.Message);
            MessageBox.Show("无法运行 codex doctor。请确认 Codex CLI 已安装并可从 PATH 调用。\n\n" + ex.Message, "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ClearGit()
    {
        if (MessageBox.Show("将删除 Git 全局 http.proxy / https.proxy 配置。是否继续？", "确认清理 Git 代理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { _repair.ClearGitProxy(); WriteLog("已清理 Git 全局代理。 "); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "清理失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ClearNpm()
    {
        if (MessageBox.Show("将删除 npm 的 proxy / https-proxy 配置。是否继续？", "确认清理 npm 代理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { _repair.ClearNpmProxy(); WriteLog("已清理 npm 代理。 "); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "清理失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void UpdateResult(DiagnosisResult result)
    {
        _health.Text = $"● {HealthZh(result.Health)}：{result.FailureNameZh}";
        _health.ForeColor = result.Health switch
        {
            HealthState.Healthy => Color.ForestGreen,
            HealthState.Warning => Color.DarkOrange,
            _ => Color.Firebrick
        };
        _detail.Text =
            $"DNS={BoolZh(result.Dns.Ok)}    直连TLS={BoolZh(result.DirectTls.Ok)}    代理HTTPS={BoolZh(result.Proxy.Ok)}    TUN={BoolZh(result.Tun.AdapterDetected)}\n" +
            $"Git冲突={ConflictZh(result.Git.Conflict)}    npm冲突={ConflictZh(result.Npm.Conflict)}    .env={BoolZh(result.Env.Exists)}    代理={Display(result.ProxyUrl)}\n" +
            $"建议：{result.RecommendationZh}";
    }

    private void WriteLog(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.Text.Length;
        _log.ScrollToCaret();
    }

    private static string HealthZh(HealthState state) => state switch
    {
        HealthState.Healthy => "健康",
        HealthState.Warning => "需要检查",
        _ => "连接故障"
    };

    private static string BoolZh(bool value) => value ? "正常" : "异常";
    private static string ConflictZh(bool conflict) => conflict ? "有冲突" : "无冲突";
    private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "未设置" : value;

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        Enabled = !busy;
        if (!busy) Enabled = true;
    }
}
