using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodexDoctor.Native;

public sealed class CodexDiscoveryService
{
    private static readonly Regex SensitiveName = new("TOKEN|KEY|SECRET|PASSWORD|COOKIE|AUTH|SESSION", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> VisibleConfigNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
        "http_proxy", "https_proxy", "all_proxy", "no_proxy",
        "language", "locale", "ui_language", "response_language", "cli_language"
    };

    private readonly string _userProfile;
    private readonly string _localAppData;
    private readonly string _appData;

    public CodexDiscoveryService(string? userProfile = null, string? localAppData = null, string? appData = null)
    {
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _localAppData = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appData = appData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    public Task<CodexDiscoveryResult> ScanAsync()
    {
        return Task.Run(() =>
        {
            var desktopPaths = new List<string>();
            desktopPaths.AddRange(DiscoverRunningDesktopPaths());
            desktopPaths.AddRange(DiscoverAppPathRegistry());
            desktopPaths.AddRange(new[]
            {
                Path.Combine(_localAppData, "Programs", "Codex"),
                Path.Combine(_localAppData, "Programs", "ChatGPT"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Codex"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ChatGPT"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Codex"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ChatGPT")
            });

            var desktops = DiscoverDesktopCandidates(desktopPaths)
                .GroupBy(x => x.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => MergeDesktop(g.ToList()))
                .ToArray();

            var cli = DiscoverCliFromPath(Environment.GetEnvironmentVariable("PATH") ?? "", DiscoverCommonCliCandidates(_appData));
            if (!cli.Found)
            {
                var where = TryWhereCodex();
                if (!string.IsNullOrWhiteSpace(where))
                    cli = DiscoverCliFromPath("", [where]);
            }

            var dataDirectory = ScanDataDirectory(Path.Combine(_userProfile, ".codex"));
            var configFiles = new List<CodexConfigFileInfo>();
            foreach (var name in new[] { ".env", "config.toml" })
            {
                var path = Path.Combine(dataDirectory.Path, name);
                if (File.Exists(path)) configFiles.Add(ScanConfigFile(path));
                else configFiles.Add(new CodexConfigFileInfo(path, false, []));
            }

            var language = new CodexLanguageState("未知", "未知", "未知", false, true, "尚未检测语言配置");
            return new CodexDiscoveryResult(desktops, cli, dataDirectory, configFiles, language);
        });
    }

    public static IReadOnlyList<CodexDesktopInstallationInfo> DiscoverDesktopCandidates(IEnumerable<string> candidates)
    {
        var result = new List<CodexDesktopInstallationInfo>();
        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                if (File.Exists(candidate))
                {
                    AddDesktopFile(candidate, "路径发现", result);
                    continue;
                }

                if (!Directory.Exists(candidate)) continue;
                foreach (var name in new[] { "Codex.exe", "ChatGPT.exe" })
                {
                    var path = Path.Combine(candidate, name);
                    if (File.Exists(path)) AddDesktopFile(path, "目录发现", result);
                }
            }
            catch
            {
                // 单个候选失败不应中断整次只读扫描。
            }
        }
        return result;
    }

    public static CodexCliInfo DiscoverCliFromPath(string pathValue, IEnumerable<string>? additionalCandidates = null)
    {
        var pathDirs = (pathValue ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var dir in pathDirs)
        {
            foreach (var name in new[] { "codex.exe", "codex.cmd", "codex.bat" })
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return BuildCli(candidate, true);
            }
        }

        if (additionalCandidates is not null)
        {
            foreach (var candidate in additionalCandidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (File.Exists(candidate)) return BuildCli(candidate, false);
                if (Directory.Exists(candidate))
                {
                    foreach (var name in new[] { "codex.exe", "codex.cmd", "codex.bat" })
                    {
                        var nested = Path.Combine(candidate, name);
                        if (File.Exists(nested)) return BuildCli(nested, false);
                    }
                }
            }
        }

        return new CodexCliInfo(false, null, false, null, false);
    }

    public static CodexConfigFileInfo ScanConfigFile(string path)
    {
        if (!File.Exists(path)) return new CodexConfigFileInfo(path, false, []);
        var entries = new List<CodexConfigEntryInfo>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            var name = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim().Trim('"', '\'');
            var sensitive = SensitiveName.IsMatch(name);
            var configured = !string.IsNullOrWhiteSpace(value);
            var visibleValue = !sensitive && VisibleConfigNames.Contains(name) ? value : null;
            entries.Add(new CodexConfigEntryInfo(name, visibleValue, sensitive, configured));
        }
        return new CodexConfigFileInfo(path, true, entries);
    }

    public static CodexDataDirectoryInfo ScanDataDirectory(string path, int maxFiles = 10000)
    {
        if (!Directory.Exists(path)) return new CodexDataDirectoryInfo(path, false, false, null, 0, 0);

        var directory = new DirectoryInfo(path);
        var isReparse = directory.Attributes.HasFlag(FileAttributes.ReparsePoint);
        string? target = null;
        try { target = directory.LinkTarget; } catch { }

        var count = 0;
        long size = 0;
        try
        {
            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (++count > maxFiles) break;
                try { size += file.Length; } catch { }
            }
        }
        catch { }

        return new CodexDataDirectoryInfo(path, true, isReparse, target, Math.Min(count, maxFiles), size);
    }

    private static void AddDesktopFile(string path, string source, List<CodexDesktopInstallationInfo> result)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (!fileName.Contains("Codex", StringComparison.OrdinalIgnoreCase) &&
            !fileName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase)) return;

        string? version = null;
        try { version = FileVersionInfo.GetVersionInfo(path).ProductVersion ?? FileVersionInfo.GetVersionInfo(path).FileVersion; } catch { }
        var runningPids = GetRunningPidsForPath(path);
        var product = fileName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase) ? "ChatGPT Desktop" : "Codex Desktop";
        result.Add(new CodexDesktopInstallationInfo(product, source, Path.GetFullPath(path), version, runningPids.Count > 0, runningPids));
    }

    private static IReadOnlyList<int> GetRunningPidsForPath(string path)
    {
        var pids = new List<int>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(processPath) && string.Equals(Path.GetFullPath(processPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    pids.Add(process.Id);
            }
            catch { }
            finally { process.Dispose(); }
        }
        return pids;
    }

    private static CodexDesktopInstallationInfo MergeDesktop(IReadOnlyList<CodexDesktopInstallationInfo> items)
    {
        var first = items[0];
        var pids = items.SelectMany(x => x.ProcessIds).Distinct().ToArray();
        return first with { IsRunning = pids.Length > 0 || items.Any(x => x.IsRunning), ProcessIds = pids };
    }

    private static CodexCliInfo BuildCli(string path, bool pathCallable)
    {
        var version = TryRunCli(path, "--version");
        return new CodexCliInfo(true, Path.GetFullPath(path), pathCallable, string.IsNullOrWhiteSpace(version) ? null : version, true);
    }

    private static string? TryRunCli(string path, string arguments)
    {
        try
        {
            ProcessStartInfo psi;
            var ext = Path.GetExtension(path);
            if (ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                psi = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = $"/d /s /c \"\"{path}\" {arguments}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                psi = new ProcessStartInfo(path)
                {
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            using var process = Process.Start(psi);
            if (process is null) return null;
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(true); } catch { }
                return null;
            }
            var stdout = process.StandardOutput.ReadToEnd().Trim();
            var stderr = process.StandardError.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        }
        catch { return null; }
    }

    private static IEnumerable<string> DiscoverRunningDesktopPaths()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!process.ProcessName.Contains("Codex", StringComparison.OrdinalIgnoreCase) &&
                    !process.ProcessName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase)) continue;
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path)) yield return path;
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private static IEnumerable<string> DiscoverAppPathRegistry()
    {
        var result = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var exe in new[] { "Codex.exe", "ChatGPT.exe" })
            {
                try
                {
                    using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}", false);
                    if (key?.GetValue(null) is string value && !string.IsNullOrWhiteSpace(value)) result.Add(value.Trim('"'));
                }
                catch { }
            }
        }
        return result;
    }

    private static IEnumerable<string> DiscoverCommonCliCandidates(string appData)
    {
        yield return Path.Combine(appData, "npm", "codex.cmd");
        yield return Path.Combine(appData, "npm", "codex.exe");
    }

    private static string? TryWhereCodex()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("where.exe")
            {
                Arguments = "codex",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null) return null;
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(true); } catch { }
                return null;
            }
            if (process.ExitCode != 0) return null;
            return process.StandardOutput.ReadToEnd()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(File.Exists);
        }
        catch { return null; }
    }
}
