namespace CodexDoctor.Native;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (!AdminGuard.EnsureAdministratorOrExit()) return;
        Application.Run(new MainForm());
    }
}
