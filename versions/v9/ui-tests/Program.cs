using CodexDoctor.V9;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        ApplicationConfiguration.Initialize();
        using var form = new Form { Text = "Codex Doctor 语言自动化测试夹具", Size = new Size(500, 260) };
        var settings = new Button { Text = "Settings", AccessibleName = "Settings", Left = 20, Top = 20, Width = 120 };
        var combo = new ComboBox { AccessibleName = "Language", DropDownStyle = ComboBoxStyle.DropDownList, Left = 20, Top = 80, Width = 220, Visible = false };
        combo.Items.AddRange(["English", "简体中文"]); combo.SelectedIndex = 0;
        settings.Click += (_, _) => combo.Visible = true;
        form.Controls.Add(settings); form.Controls.Add(combo);
        var exit = 1;
        form.Shown += async (_, _) =>
        {
            try
            {
                form.Show(); form.Activate(); await Task.Delay(700);
                var desktop = new DesktopInstallation(Environment.ProcessPath!, "fixture", [Environment.ProcessId]);
                await LanguageService.SetAsync(desktop, "zh-CN", CancellationToken.None);
                if ((string?)combo.SelectedItem != "简体中文") throw new Exception("简体中文没有实际选中。");
                await LanguageService.SetAsync(desktop, "en-US", CancellationToken.None);
                if ((string?)combo.SelectedItem != "English") throw new Exception("英文没有实际选中。");
                File.WriteAllText(args[0], "通过：Windows UI Automation 实际打开 Settings、切换中文并复检，再切换英文并复检。\n"); exit = 0;
            }
            catch (Exception ex) { File.WriteAllText(args[0], "失败：" + ex); }
            finally { form.Close(); }
        };
        Application.Run(form); return exit;
    }
}
