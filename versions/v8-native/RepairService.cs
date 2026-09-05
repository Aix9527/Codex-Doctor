using System.Diagnostics;
using System.Text;

namespace CodexDoctor.Native;

public sealed class RepairService
{
    private readonly string _userProfile;
    public string EnvFile { get; }

    public RepairService(string? userProfile = null)
    {
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        EnvFile = Path.Combine(_userProfile, ".codex", ".env");
    }

    public string WriteCodexProxyEnv(string proxyUrl, bool writeUserEnvironment)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl)) throw new InvalidOperationException("没有已验证代理，无法写入配置。");
        Directory.CreateDirectory(Path.GetDirectoryName(EnvFile)!);
        var backup = "";
        var keep = new List<string>();
        if (File.Exists(EnvFile))
        {
            backup = EnvFile + ".backup_" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(EnvFile, backup, true);
            foreach (var line in File.ReadAllLines(EnvFile, Encoding.UTF8))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("HTTP_PROXY=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("HTTPS_PROXY=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("ALL_PROXY=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("NO_PROXY=", StringComparison.OrdinalIgnoreCase))
                    continue;
                keep.Add(line);
            }
        }

        keep.Add($"HTTP_PROXY={proxyUrl}");
        keep.Add($"HTTPS_PROXY={proxyUrl}");
        keep.Add($"http_proxy={proxyUrl}");
        keep.Add($"https_proxy={proxyUrl}");
        keep.Add("NO_PROXY=localhost,127.0.0.1,::1");
        keep.Add("no_proxy=localhost,127.0.0.1,::1");
        File.WriteAllLines(EnvFile, keep, new UTF8Encoding(false));

        if (writeUserEnvironment)
        {
            Environment.SetEnvironmentVariable("HTTP_PROXY", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("http_proxy", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("https_proxy", proxyUrl, EnvironmentVariableTarget.User);
        }
        return backup;
    }

    public void ClearGitProxy()
    {
        Run("git", "config --global --unset-all http.proxy", true);
        Run("git", "config --global --unset-all https.proxy", true);
    }

    public void ClearNpmProxy()
    {
        Run("npm.cmd", "config delete proxy", true);
        Run("npm.cmd", "config delete https-proxy", true);
    }

    public string RunCodexDoctor()
    {
        return Run("codex", "doctor", false);
    }

    public void RestartCodexDesktop()
    {
        foreach (var p in Process.GetProcesses())
        {
            if (p.ProcessName.Contains("Codex", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase))
            {
                try { p.Kill(true); } catch { }
            }
        }
        Thread.Sleep(700);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var path in new[]
        {
            Path.Combine(local, "Programs", "Codex", "Codex.exe"),
            Path.Combine(local, "Programs", "ChatGPT", "ChatGPT.exe")
        })
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }
        }
        try { Process.Start(new ProcessStartInfo("chatgpt:") { UseShellExecute = true }); } catch { }
    }

    private static string Run(string fileName, string arguments, bool allowNonZero)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null) throw new InvalidOperationException($"无法启动 {fileName}。");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            if (!allowNonZero && process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"{fileName} 执行失败，退出码 {process.ExitCode}。" : error.Trim());
            return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"无法执行 {fileName}：{ex.Message}", ex);
        }
    }
}
