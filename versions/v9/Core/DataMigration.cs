using System.Text.Json;
using System.Diagnostics;

namespace CodexDoctor.V9;

public sealed record MigrationJournal(string Source, string Target, string Backup, string Stage);

public sealed class DataMigration(string source, string stateFile, Func<bool>? clientRunning = null)
{
    private readonly string _source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
    private readonly string _stateFile = Path.GetFullPath(stateFile);
    private readonly Func<bool> _clientRunning = clientRunning ?? (() => {
        foreach (var p in Process.GetProcesses()) using (p) { if (p.ProcessName.Equals("codex", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)) return true; }
        return false;
    });

    public static void ValidatePaths(string source, string target)
    {
        source = Path.GetFullPath(source).TrimEnd('\\'); target = Path.GetFullPath(target).TrimEnd('\\');
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase) || source.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase) || target.StartsWith(source + "\\", StringComparison.OrdinalIgnoreCase))
            throw new IOException("迁移源和目标不能相同或互相包含。");
        foreach (var path in new[] { source, target })
        {
            if (path.StartsWith(@"\\") || path.IndexOfAny(['"', '%', '!', '&', '|', '<', '>', '^', '\r', '\n']) >= 0) throw new IOException("迁移仅支持没有命令特殊字符的本地路径。");
            for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
                if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("迁移路径的父目录包含链接，无法证明源和目标不重叠。");
        }
    }

    public async Task MigrateAsync(string target, CancellationToken ct)
    {
        target = Path.GetFullPath(target); ValidatePaths(_source, target); EnsureStopped();
        if (File.Exists(_stateFile)) throw new IOException("已有迁移记录，请先恢复或检查记录，不能覆盖。");
        if (!Directory.Exists(_source) || new DirectoryInfo(_source).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("源目录不存在或已是链接，不能执行首次迁移。");
        if (Directory.Exists(target) && (new DirectoryInfo(target).Attributes.HasFlag(FileAttributes.ReparsePoint) || Directory.EnumerateFileSystemEntries(target).Any())) throw new IOException("目标不为空或是链接，未覆盖。");
        var backup = _source + ".doctor-migration-" + Guid.NewGuid().ToString("N");
        var state = new MigrationJournal(_source, target, backup, "复制中");
        Save(state);
        try
        {
            await Task.Run(() => CopyVerified(_source, target, ct), ct).ConfigureAwait(false);
            EnsureStopped(); ct.ThrowIfCancellationRequested(); Save(state with { Stage = "准备切换" });
            Directory.Move(_source, backup);
            await JunctionAsync(_source, target).ConfigureAwait(false);
            VerifyLink(target); Save(state with { Stage = "已迁移" });
        }
        catch
        {
            if (Directory.Exists(backup))
            {
                if (new DirectoryInfo(_source).LinkTarget is not null) { VerifyLink(target); Directory.Delete(_source, false); }
                if (!Directory.Exists(_source)) Directory.Move(backup, _source);
            }
            Save(state with { Stage = "失败；原数据或副本已保留" });
            throw;
        }
    }

    public async Task RestoreAsync(CancellationToken ct)
    {
        EnsureStopped();
        var state = JsonSerializer.Deserialize<MigrationJournal>(File.ReadAllText(_stateFile)) ?? throw new IOException("迁移记录无效。");
        var backup = Path.GetFullPath(state.Backup);
        if (!string.Equals(Path.GetFullPath(state.Source), _source, StringComparison.OrdinalIgnoreCase) || !backup.StartsWith(_source + ".doctor-migration-", StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(backup), Path.GetDirectoryName(_source), StringComparison.OrdinalIgnoreCase)) throw new IOException("迁移记录不属于当前数据目录。");
        ValidatePaths(_source, state.Target);
        if (!Directory.Exists(_source) && new DirectoryInfo(_source).LinkTarget is null && state.Stage == "已迁移")
        {
            var recovery = _source + ".doctor-recover-" + Guid.NewGuid().ToString("N");
            await Task.Run(() => CopyVerified(state.Target, recovery, ct), ct).ConfigureAwait(false);
            EnsureStopped(); ct.ThrowIfCancellationRequested(); Directory.Move(recovery, _source); File.Delete(_stateFile); return;
        }
        if (!Directory.Exists(_source) && new DirectoryInfo(_source).LinkTarget is null && Directory.Exists(state.Backup))
        { Directory.Move(state.Backup, _source); File.Delete(_stateFile); return; }
        if (new DirectoryInfo(_source).LinkTarget is null)
        {
            if (state.Stage.StartsWith("失败") && Directory.Exists(_source)) { File.Delete(_stateFile); return; }
            throw new IOException("源不是迁移链接，不能覆盖普通目录。");
        }
        VerifyLink(state.Target);
        var staging = _source + ".doctor-restore-" + Guid.NewGuid().ToString("N");
        await Task.Run(() => CopyVerified(state.Target, staging, ct), ct).ConfigureAwait(false);
        EnsureStopped(); ct.ThrowIfCancellationRequested(); VerifyLink(state.Target);
        // Once unlinking starts, finish the short transaction without cancellation.
        Directory.Delete(_source, false);
        try { Directory.Move(staging, _source); }
        catch { await JunctionAsync(_source, state.Target).ConfigureAwait(false); throw; }
        File.Delete(_stateFile);
    }

    private void EnsureStopped() { if (_clientRunning()) throw new IOException("迁移或恢复前必须完全退出 Codex Desktop 和 CLI，避免复制正在写入的会话数据库。"); }
    private void VerifyLink(string target)
    {
        var resolved = new DirectoryInfo(_source).ResolveLinkTarget(true)?.FullName;
        if (resolved is null || !Path.GetFullPath(resolved).TrimEnd('\\').Equals(Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new IOException("Junction 目标与迁移记录不一致。");
    }
    private static async Task JunctionAsync(string source, string target)
    {
        ValidatePaths(source, target);
        var result = await CommandRunner.RunAsync("cmd.exe", ["/d", "/c", "mklink", "/J", source, target], TimeSpan.FromSeconds(10), CancellationToken.None);
        result.EnsureSuccess("创建数据目录 Junction");
    }
    private void Save(MigrationJournal state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
        var temp = _stateFile + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(state)); File.Move(temp, _stateFile, true);
    }
    private static void CopyVerified(string source, string target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(target);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("源目录包含嵌套链接，不能安全迁移：" + entry.Name);
            var destination = Path.Combine(target, entry.Name);
            if (entry is DirectoryInfo) CopyVerified(entry.FullName, destination, ct);
            else
            {
                File.Copy(entry.FullName, destination, false);
                if (ProxyConfiguration.Hash(entry.FullName) != ProxyConfiguration.Hash(destination)) throw new IOException("迁移文件校验失败：" + entry.Name);
            }
        }
    }
}
