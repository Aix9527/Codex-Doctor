namespace CodexDoctor.V9;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 1 && args[0] == "--self-test")
        {
            var root = Path.Combine(Path.GetTempPath(), "CodexDoctorV9-selftest-" + Guid.NewGuid().ToString("N"));
            try { var c = new ProxyConfiguration(root); var change = c.Write("http://127.0.0.1:23456"); if (!c.Matches("http://127.0.0.1:23456")) return 1; c.Rollback(change); return File.Exists(c.EnvFile) ? 1 : 0; }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        if (args.Length == 2 && args[0] == "--render-ui")
        {
            using var form = new DoctorForm(); form.CreateControl(); form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(Path.GetFullPath(args[1])); form.Close(); return 0;
        }
        if (args.Length > 0) return 2;
        Application.Run(new DoctorForm()); return 0;
    }
}
