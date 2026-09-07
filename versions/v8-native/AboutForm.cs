namespace CodexDoctor.Native;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "关于 Codex Doctor V8.1.3";
        Width = 520;
        Height = 360;
        MinimumSize = new Size(500, 340);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var title = new Label
        {
            Text = "Codex Doctor V8.1.3",
            Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
            Location = new Point(28, 24),
            Size = new Size(440, 44)
        };
        Controls.Add(title);

        var info = new Label
        {
            Text = "Windows 原生 Codex 维修中心\n\n" +
                   "软件作者：Aix\n" +
                   "QQ：976936105\n" +
                   "抖音：xch03209527\n\n" +
                   "GitHub：Aix9527/Codex-Doctor\n" +
                   "版本：8.1.3",
            Location = new Point(32, 82),
            Size = new Size(430, 170)
        };
        Controls.Add(info);

        var close = new Button
        {
            Text = "关闭",
            Location = new Point(360, 270),
            Size = new Size(110, 38),
            DialogResult = DialogResult.OK
        };
        Controls.Add(close);
        AcceptButton = close;
    }
}
