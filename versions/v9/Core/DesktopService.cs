using Microsoft.Win32;
using System.Diagnostics;

namespace CodexDoctor.V9;

public sealed record DesktopInstallation(string Path, string Version, IReadOnlyList<int> ProcessIds)
{
    public override string ToString() => System.IO.Path.GetFileName(Path) + " — " + Path;
}

public static class DesktopService
{
    public static bool IsDesktopExecutable(string path)
    {
        if (!IsDesktopPath(path) || !File.Exists(path)) return false;
        try
        {
            using var file = File.OpenRead(path); using var reader = new BinaryReader(file);
            if (file.Length < 64 || reader.ReadUInt16() != 0x5a4d) return false;
            file.Position = 60; var offset = reader.ReadInt32(); if (offset < 64 || offset + 94L > file.Length) return false;
            file.Position = offset; if (reader.ReadUInt32() != 0x00004550) return false;
            file.Position = offset + 24; var magic = reader.ReadUInt16(); if (magic is not 0x10b and not 0x20b) return false;
            file.Position = offset + 24 + 68; return reader.ReadUInt16() == 2;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public static bool IsDesktopPath(string path)
    {
        var name = Path.GetFileName(path);
        return (name.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)) &&
            !path.Contains("\\resources\\", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\node_modules\\", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<DesktopInstallation> Discover(string? selectedPath = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path) { if (!string.IsNullOrWhiteSpace(path) && IsDesktopExecutable(path)) paths.Add(Path.GetFullPath(path)); }
        Add(selectedPath);
        foreach (var process in Process.GetProcesses())
        {
            using (process) try { if (process.ProcessName is "Codex" or "codex" or "ChatGPT") Add(process.MainModule?.FileName); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Programs", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            foreach (var name in new[] { "Codex", "ChatGPT" }) Add(Path.Combine(root, name, name + ".exe"));
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (var name in new[] { "Codex.exe", "ChatGPT.exe" })
                using (var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name)) Add((key?.GetValue(null) as string)?.Trim('"'));
        // Read registration metadata, not protected package contents or user credentials.
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var packages = hive.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
                foreach (var name in packages?.GetSubKeyNames().Where(x => x.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) || x.StartsWith("OpenAI.ChatGPT_", StringComparison.OrdinalIgnoreCase)) ?? [])
                {
                    using var package = packages!.OpenSubKey(name);
                    if (package?.GetValue("PackageRootFolder") is string root)
                        foreach (var exe in new[] { "Codex.exe", "ChatGPT.exe" }) { Add(Path.Combine(root, "app", exe)); Add(Path.Combine(root, exe)); }
                }
            }
            catch (System.Security.SecurityException) { }
        }
        return paths.Select(path => new DesktopInstallation(path, FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "未知", Running(path))).OrderByDescending(x => x.ProcessIds.Count).ToArray();
    }

    public static IReadOnlyList<int> Running(string path)
    {
        var ids = new List<int>();
        foreach (var p in Process.GetProcesses())
            using (p) try { if (string.Equals(p.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)) ids.Add(p.Id); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        return ids;
    }

    public static async Task StartAsync(DesktopInstallation desktop, bool restart, string? proxy, CancellationToken ct)
    {
        if (!IsDesktopExecutable(desktop.Path)) throw new IOException("客户端路径已失效或不是 Desktop 图形程序，请重新扫描或选择 Codex.exe。");
        if (restart)
        {
            foreach (var id in Running(desktop.Path))
            {
                ct.ThrowIfCancellationRequested();
                using var process = Process.GetProcessById(id);
                if (!string.Equals(process.MainModule?.FileName, desktop.Path, StringComparison.OrdinalIgnoreCase)) throw new IOException("进程身份发生变化，已停止重启。");
                if (process.MainWindowHandle != 0) process.CloseMainWindow();
            }
            for (var attempt = 0; attempt < 40 && Running(desktop.Path).Count > 0; attempt++) await Task.Delay(250, ct).ConfigureAwait(false);
            if (Running(desktop.Path).Count > 0) throw new IOException("客户端尚未完全退出。请保存任务并手动退出，再点击启动；没有强制结束进程。");
        }
        else if (Running(desktop.Path).Count > 0) throw new IOException("客户端已经运行。若要使代理环境生效，请使用重启操作。");
        ct.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(desktop.Path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(desktop.Path)! };
        if (ProxyConfiguration.LocalHttpProxy(proxy) is { } p)
        {
            foreach (var key in new[] { "HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy" }) start.Environment[key] = p;
        }
        using var launched = Process.Start(start) ?? throw new IOException("客户端进程未能启动。");
        for (var attempt = 0; attempt < 30; attempt++) { await Task.Delay(250, ct).ConfigureAwait(false); if (Running(desktop.Path).Count > 0) return; }
        throw new IOException("启动后未找到客户端进程，不能报告启动成功。");
    }
}
