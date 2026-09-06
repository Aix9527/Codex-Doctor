namespace CodexDoctor.Native;

public static class V811UiUpgrade
{
    public static void Apply(MainForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        form.Text = "Codex Doctor V8.1.1 原生维修中心";

        var panel = form.Controls.OfType<FlowLayoutPanel>().FirstOrDefault()
            ?? throw new InvalidOperationException("未找到主操作区。");

        var legacyLanguage = panel.Controls.OfType<Button>()
            .FirstOrDefault(x => x.Text.Contains("中文", StringComparison.OrdinalIgnoreCase) ||
                                 x.Text.Contains("用户操作", StringComparison.OrdinalIgnoreCase));
        if (legacyLanguage is not null)
        {
            panel.Controls.Remove(legacyLanguage);
            legacyLanguage.Dispose();
        }

        panel.WrapContents = true;
        panel.Height = Math.Max(panel.Height, 96);

        var chinese = CreateButton("中文", 105);
        var english = CreateButton("English", 105);
        var install = CreateButton("安装 / 卸载", 145);

        chinese.Enabled = false;
        english.Enabled = false;
        install.Enabled = false;

        panel.Controls.Add(chinese);
        panel.Controls.Add(english);
        panel.Controls.Add(install);
    }

    private static Button CreateButton(string text, int width) => new()
    {
        Text = text,
        Size = new Size(width, 44),
        Margin = new Padding(4)
    };
}
