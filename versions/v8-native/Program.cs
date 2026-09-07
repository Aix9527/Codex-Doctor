namespace CodexDoctor.Native;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (!AdminGuard.EnsureAdministratorOrExit()) return;
        var form = new MainForm();
        V820UiUpgrade.Apply(form);
        Application.Run(form);
    }
}