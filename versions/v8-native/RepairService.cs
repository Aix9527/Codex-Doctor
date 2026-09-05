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

    public string RunCodexDoctor(string cliPath)
    {
        if (string.IsNullOrWhiteSpace(cliPath) || !File.Exists(cliPath))
            throw new InvalidOperationException("未找到可用的 Codex CLI。请先执行“扫描本机 Codex”。");

        var extension = Path.GetExtension(cliPath);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var escaped = cliPath.Replace("\"", "\"\"");
            return Run("cmd.exe", $"/d /s /c \"\"{escaped}\" doctor\"", false);
        }
        return Run(cliPath, "doctor", false);
    }

    public string RunCodexDoctor()
    {
        var discovery = new CodexDiscoveryService(_userProfile).ScanAsync().GetAwaiter().GetResult();
        if (!discovery.Cli.Found || string.IsNullOrWhiteSpace(discovery.Cli.Path))
            throw new InvalidOperationException("已检测到 Codex Desktop 的可能安装环境，但未检测到可执行的 Codex CLI；此项可跳过，不影响网络诊断与代理修复。");
        return RunCodexDoctor(discovery.Cli.Path);
    }

    public void RestartCodexDesktop(string executablePath, IReadOnlyCollection<int> processIds)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            throw new InvalidOperationException("未找到可启动的 Codex/ChatGPT Desktop 程序。请先执行“扫描本机 Codex”。");

        foreach (var id in processIds.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill(true);
                process.WaitForExit(3000);
            }
            catch
            {
                // 进程可能已退出或权限受限；继续尝试启动已确认的 Desktop 路径。
            }
        }

        Thread.Sleep(300);
        try
        {
            Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("已找到 Codex Desktop，但重新启动失败：" + ex.Message, ex);
        }
    }

    public void RestartCodexDesktop()
    {
        var discovery = new CodexDiscoveryService(_userProfile).ScanAsync().GetAwaiter().GetResult();
        var desktop = discovery.DesktopClients
            .OrderByDescending(x => x.IsRunning)
            .ThenByDescending(x => x.ProcessIds.Count)
            .FirstOrDefault();
        if (desktop is null)
            throw new InvalidOperationException("未检测到 Codex/ChatGPT Desktop。请先执行“扫描本机 Codex”。");
        RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds);
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
            if (!process.WaitForExit(10000))
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException($"{Path.GetFileName(fileName)} 执行超时。");
            }
            if (!allowNonZero && process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"{Path.GetFileName(fileName)} 执行失败，退出码 {process.ExitCode}。" : error.Trim());
            return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"无法执行 {Path.GetFileName(fileName)}：{ex.Message}", ex);
        }
    }
}
