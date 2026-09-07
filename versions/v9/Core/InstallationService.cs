namespace CodexDoctor.V9;

public static class InstallationService
{
    // OpenAI Windows documentation, checked 2026-09-08.
    public const string StoreId = "9PLM9XGG6VKS";
    public static string? CliPath() => CommandRunner.Find("codex.cmd") ?? CommandRunner.Find("codex.exe") ??
        new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd") }.FirstOrDefault(File.Exists);

    public static async Task<string> CliVersionAsync(CancellationToken ct)
    {
        var path = CliPath(); if (path is null) return "未安装或无法发现";
        var result = await CommandRunner.RunAsync(path, ["--version"], TimeSpan.FromSeconds(10), ct);
        result.EnsureSuccess("CLI 版本检查");
        return result.Output;
    }

    public static async Task<string> ChangeCliAsync(bool uninstall, CancellationToken ct)
    {
        var npm = CommandRunner.Find("npm.cmd") ?? throw new IOException("未找到 npm.cmd，请先安装 Node.js LTS 后重试。");
        var result = await CommandRunner.RunAsync(npm, [uninstall ? "uninstall" : "install", "--global", "@openai/codex"], TimeSpan.FromMinutes(10), ct);
        result.EnsureSuccess(uninstall ? "CLI 卸载" : "CLI 安装/升级");
        if (uninstall)
        {
            if (CliPath() is not null) throw new IOException("npm 命令已完成，但仍发现 CLI 入口，可能存在其他安装来源；未确认完全卸载。");
            return "CLI 卸载完成，已重新检查入口。";
        }
        return "CLI 安装/升级完成，版本验证：" + await CliVersionAsync(ct);
    }

    public static async Task<string> ChangeDesktopAsync(bool uninstall, CancellationToken ct)
    {
        var winget = CommandRunner.Find("winget.exe") ?? throw new IOException("未发现 winget，请通过官方 Microsoft Store 页面安装或修复 App Installer。");
        string[] args = uninstall
            ? ["uninstall", "--id", StoreId, "--exact", "--silent", "--disable-interactivity"]
            : ["install", "--id", StoreId, "--source", "msstore", "--accept-package-agreements", "--accept-source-agreements", "--silent", "--disable-interactivity"];
        var result = await CommandRunner.RunAsync(winget, args, TimeSpan.FromMinutes(10), ct);
        result.EnsureSuccess(uninstall ? "Desktop 卸载" : "Desktop 安装");
        var verification = await CommandRunner.RunAsync(winget, ["list", "--id", StoreId, "--exact", "--accept-source-agreements", "--disable-interactivity"], TimeSpan.FromSeconds(45), ct);
        var listed = verification.ExitCode == 0 && verification.Output.Contains(StoreId, StringComparison.OrdinalIgnoreCase);
        if (!uninstall && !listed) throw new IOException("安装命令完成，但官方包 ID 未通过安装列表复检。");
        if (uninstall && (verification.TimedOut || verification.ExitCode != unchecked((int)0x8A150014))) throw new IOException("未得到包管理器明确的“未找到已安装包”结果，请在 Windows 应用设置复核；不能把查询失败当作卸载成功。");
        return uninstall ? "官方 Desktop 包卸载命令完成，安装列表未再发现目标包。" : "官方 Desktop 包安装完成，已核对安装列表。";
    }

    public static string AddUserPath(string directory, string current)
    {
        directory = Path.GetFullPath(directory);
        if (current.Split(';').Any(x => string.Equals(x.Trim().Trim('"').TrimEnd('\\'), directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) return current;
        return current.TrimEnd(';') + (string.IsNullOrEmpty(current) ? "" : ";") + directory;
    }
}
