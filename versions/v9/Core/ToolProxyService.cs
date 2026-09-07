namespace CodexDoctor.V9;

public static class ToolProxyService
{
    public static async Task<string> ClearAsync(bool git, string profile, CancellationToken ct)
    {
        var tool = CommandRunner.Find(git ? "git.exe" : "npm.cmd") ?? throw new IOException("所需工具未安装。");
        var file = Path.Combine(profile, git ? ".gitconfig" : ".npmrc");
        // npm may use NPM_CONFIG_USERCONFIG; never modify a different file than the backed-up one.
        if (!git && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NPM_CONFIG_USERCONFIG"))) throw new IOException("npm 使用自定义配置路径；请在终端检查，未修改该配置。");
        if (git && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL"))) throw new IOException("Git 使用自定义全局配置路径；请在终端检查，未修改该配置。");
        if (!File.Exists(file)) return "默认用户配置文件不存在，无需清理。";
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("工具配置是链接，未修改。");
        var backup = file + ".doctor-backup-" + Guid.NewGuid().ToString("N");
        File.Copy(file, backup);
        try
        {
            foreach (var key in git ? new[] { "http.proxy", "https.proxy" } : new[] { "proxy", "https-proxy" })
            {
                string[] args = git ? ["config", "--file", file, "--unset-all", key] : ["config", "delete", key, "--location=user", "--userconfig", file];
                var result = await CommandRunner.RunAsync(tool, args, TimeSpan.FromSeconds(15), ct);
                if (!(git && result.ExitCode == 5)) result.EnsureSuccess("代理清理");
                string[] read = git ? ["config", "--file", file, "--get-all", key] : ["config", "get", key, "--userconfig", file];
                var verify = await CommandRunner.RunAsync(tool, read, TimeSpan.FromSeconds(15), ct);
                if (verify.TimedOut || (git ? verify.ExitCode != 1 : verify.ExitCode != 0 || verify.Output.Trim() != "null")) throw new IOException("代理清理后的独立验证失败。");
            }
            return "代理清理并复检完成，备份：" + backup;
        }
        catch { File.Copy(backup, file, true); throw; }
    }
}
