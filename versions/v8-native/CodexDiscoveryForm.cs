using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CodexDoctor.Native;

public sealed class CodexDiscoveryForm : Form
{
    private readonly CodexDiscoveryService _discovery;
    private readonly CodexLanguageService _language;
    private CodexDiscoveryResult _result;
    private readonly TextBox _summary = new();

    public CodexDiscoveryForm(CodexDiscoveryResult result, CodexDiscoveryService discovery, CodexLanguageService language)
    {
        _result = result;
        _discovery = discovery;
        _language = language;

        Text = "Codex 本机扫描与中文设置";
        Width = 920;
        Height = 700;
        MinimumSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9F);

        var title = new Label
        {
            Text = "Codex 本机扫描与配置审计",
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            Location = new Point(24, 18),
            Size = new Size(520, 36)
        };
        Controls.Add(title);

        _summary.Multiline = true;
        _summary.ReadOnly = true;
        _summary.ScrollBars = ScrollBars.Vertical;
        _summary.Font = new Font("Consolas", 9F);
        _summary.Location = new Point(24, 68);
        _summary.Size = new Size(850, 455);
        _summary.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_summary);

        var rescan = AddButton("重新扫描", 24, 540, 118);
        var openInstall = AddButton("打开安装目录", 154, 540, 132);
        var openCodex = AddButton("打开 .codex 目录", 298, 540, 145);
        var copy = AddButton("复制扫描摘要", 455, 540, 132);
        var export = AddButton("导出扫描报告", 599, 540, 132);
        var chinese = AddButton("一键设置 Codex 为简体中文", 24, 592, 235);
        var restore = AddButton("恢复原语言", 271, 592, 132);
        var openApp = AddButton("打开 Codex 设置", 415, 592, 145);

        rescan.Click += async (_, _) => await RescanAsync();
        openInstall.Click += (_, _) => OpenInstallDirectory();
        openCodex.Click += (_, _) => OpenCodexDirectory();
        copy.Click += (_, _) => CopySummary();
        export.Click += (_, _) => ExportScanReport();
        chinese.Click += (_, _) => ApplyChinese();
        restore.Click += (_, _) => RestoreLanguage();
        openApp.Click += (_, _) => OpenDetectedApp();

        RenderSummary();
    }

    public CodexDiscoveryResult CurrentResult => _result;

    private Button AddButton(string text, int x, int y, int width)
    {
        var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 40) };
        Controls.Add(button);
        return button;
    }

    private async Task RescanAsync()
    {
        try
        {
            UseWaitCursor = true;
            var scanned = await _discovery.ScanAsync();
            _result = scanned with { LanguageState = _language.Detect(scanned) };
            RenderSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show("重新扫描失败：" + ex.Message, "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { UseWaitCursor = false; }
    }

    private void ApplyChinese()
    {
        var state = _language.ApplySimplifiedChinese(_result);
        _result = _result with { LanguageState = state };
        RenderSummary();
        if (state.Applied)
            MessageBox.Show("已设置为简体中文。建议重启 Codex 使界面重新加载语言配置。", "中文设置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(state.MethodZh + "\n\n当前没有稳定可写的本地语言配置，因此不会修改内部数据库、MSIX/AppX 安装资源。", "需要用户操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RestoreLanguage()
    {
        var state = _language.RestorePreviousLanguage();
        _result = _result with { LanguageState = state };
        RenderSummary();
        MessageBox.Show(state.MethodZh, state.Applied ? "恢复完成" : "无法自动恢复", MessageBoxButtons.OK,
            state.Applied ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void OpenInstallDirectory()
    {
        var desktop = PreferredDesktop();
        if (desktop is null)
        {
            MessageBox.Show("尚未发现 Codex/ChatGPT Desktop 安装路径。", "没有安装路径", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        OpenFolder(Path.GetDirectoryName(desktop.ExecutablePath)!);
    }

    private void OpenCodexDirectory()
    {
        if (!_result.DataDirectory.Exists)
        {
            MessageBox.Show("未检测到 .codex 数据目录。", "目录不存在", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        OpenFolder(_result.DataDirectory.Path);
    }

    private void CopySummary()
    {
        try
        {
            Clipboard.SetText(_summary.Text);
            MessageBox.Show("扫描摘要已复制到剪贴板。", "复制扫描摘要", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("复制失败：" + ex.Message, "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportScanReport()
    {
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexDoctorV8", "reports");
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, $"CodexDiscovery-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(file, JsonSerializer.Serialize(_result, options), new UTF8Encoding(false));
            MessageBox.Show(file, "导出扫描报告", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenDetectedApp()
    {
        var desktop = PreferredDesktop();
        if (desktop is null)
        {
            MessageBox.Show("未检测到可启动的 Codex/ChatGPT Desktop。", "没有可启动客户端", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(desktop.ExecutablePath) { UseShellExecute = true });
            MessageBox.Show("已打开检测到的 Codex/ChatGPT Desktop。若当前版本不支持自动写入界面语言，请在应用内进入“设置 → 通用 → 语言”选择简体中文。", "打开 Codex 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开客户端：" + ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private CodexDesktopInstallationInfo? PreferredDesktop() =>
        _result.DesktopClients.OrderByDescending(x => x.IsRunning).ThenByDescending(x => x.ProcessIds.Count).FirstOrDefault();

    private void RenderSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【Codex / ChatGPT Desktop】");
        if (_result.DesktopClients.Count == 0) sb.AppendLine("未检测到 Desktop 客户端。");
        foreach (var d in _result.DesktopClients)
        {
            sb.AppendLine($"产品：{d.ProductType}");
            sb.AppendLine($"来源：{d.Source}");
            sb.AppendLine($"路径：{d.ExecutablePath}");
            sb.AppendLine($"版本：{d.Version ?? "未知"}");
            sb.AppendLine($"运行中：{(d.IsRunning ? "是" : "否")}  PID：{(d.ProcessIds.Count == 0 ? "无" : string.Join(",", d.ProcessIds))}");
            sb.AppendLine();
        }

        sb.AppendLine("【Codex CLI】");
        sb.AppendLine($"已发现：{(_result.Cli.Found ? "是" : "否")}");
        sb.AppendLine($"路径：{_result.Cli.Path ?? "未发现"}");
        sb.AppendLine($"PATH 可调用：{(_result.Cli.PathCallable ? "是" : "否")}");
        sb.AppendLine($"版本：{_result.Cli.Version ?? "未知"}");
        sb.AppendLine();

        sb.AppendLine("【.codex 数据目录】");
        sb.AppendLine($"路径：{_result.DataDirectory.Path}");
        sb.AppendLine($"存在：{(_result.DataDirectory.Exists ? "是" : "否")}");
        sb.AppendLine($"重解析点/Junction：{(_result.DataDirectory.IsReparsePoint ? "是" : "否")}");
        sb.AppendLine($"链接目标：{_result.DataDirectory.LinkTarget ?? "无"}");
        sb.AppendLine($"文件数：{_result.DataDirectory.FileCount}  大小：{_result.DataDirectory.SizeBytes} 字节");
        sb.AppendLine();

        sb.AppendLine("【配置文件】");
        foreach (var file in _result.ConfigFiles)
        {
            sb.AppendLine($"{file.Path}：{(file.Exists ? "存在" : "不存在")}");
            foreach (var entry in file.Entries)
                sb.AppendLine($"  {entry.Name} = {(entry.Sensitive ? (entry.Configured ? "已配置（已隐藏）" : "未配置") : entry.Value ?? (entry.Configured ? "已配置" : "未配置"))}");
        }
        sb.AppendLine();

        sb.AppendLine("【语言状态】");
        sb.AppendLine($"当前界面语言：{_result.LanguageState.UiLanguage}");
        sb.AppendLine($"回答语言偏好：{_result.LanguageState.ResponseLanguagePreference}");
        sb.AppendLine($"CLI 输出偏好：{_result.LanguageState.CliLanguagePreference}");
        sb.AppendLine($"设置方式：{_result.LanguageState.MethodZh}");
        _summary.Text = sb.ToString();
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"\"{path}\"", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开目录：" + ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
