using System.Security.Principal;

namespace CodexDoctor.Native;

public static class AdminGuard
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool EnsureAdministratorOrExit()
    {
        if (IsAdministrator()) return true;
        MessageBox.Show(
            "Codex Doctor V8.1 必须以管理员权限运行。请重新启动并批准 Windows UAC。",
            "需要管理员权限",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return false;
    }
}
