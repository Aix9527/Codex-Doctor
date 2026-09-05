using System.Diagnostics;
using System.Text.Json;

namespace CodexDoctor.Native;

public sealed record MigrationState(string Source, string Target, string Backup, DateTime CreatedAt);

public sealed class MigrationService
{
    private readonly string _codexDir;
    private readonly string _stateFile;

    public MigrationService(string? userProfile = null, string? localAppData = null)
    {
        var profile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _codexDir = Path.Combine(profile, ".codex");
        var root = Path.Combine(appData, "CodexDoctorV8");
        Directory.CreateDirectory(root);
        _stateFile = Path.Combine(root, "migration-state.json");
    }

    public MigrationState Migrate(string targetRoot)
    {
        if (string.IsNullOrWhiteSpace(targetRoot)) throw new InvalidOperationException("迁移目标不能为空。");
        Directory.CreateDirectory(targetRoot);
        var target = Path.Combine(targetRoot, ".codex");
        if (Directory.Exists(_codexDir) && IsReparsePoint(_codexDir)) throw new InvalidOperationException("当前 .codex 已经是链接目录，请勿重复迁移。");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new InvalidOperationException($"迁移目标不为空：{target}");
        Directory.CreateDirectory(target);

        var backup = "";
        if (Directory.Exists(_codexDir))
        {
            CopyDirectory(_codexDir, target);
            backup = _codexDir + ".pre-migrate-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.Move(_codexDir, backup);
        }

        RunCmd($"mklink /J \"{_codexDir}\" \"{target}\"");
        if (!Directory.Exists(_codexDir) || !IsReparsePoint(_codexDir))
        {
            if (!string.IsNullOrWhiteSpace(backup) && Directory.Exists(backup) && !Directory.Exists(_codexDir)) Directory.Move(backup, _codexDir);
            throw new InvalidOperationException("创建 Junction 失败，已尝试回滚。");
        }

        var state = new MigrationState(_codexDir, target, backup, DateTime.Now);
        File.WriteAllText(_stateFile, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        return state;
    }

    public void Restore()
    {
        if (!File.Exists(_stateFile)) throw new InvalidOperationException("没有找到可恢复的迁移状态。");
        var state = JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(_stateFile)) ?? throw new InvalidOperationException("迁移状态文件无效。");
        if (!Directory.Exists(state.Source) || !IsReparsePoint(state.Source)) throw new InvalidOperationException("源目录不是 Junction，为避免误删已拒绝恢复。");
        RunCmd($"rmdir \"{state.Source}\"");
        if (!string.IsNullOrWhiteSpace(state.Backup) && Directory.Exists(state.Backup)) Directory.Move(state.Backup, state.Source);
        else Directory.CreateDirectory(state.Source);
        if (Directory.Exists(state.Target)) CopyDirectory(state.Target, state.Source);
        File.Delete(_stateFile);
    }

    private static bool IsReparsePoint(string path) => (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var info = new DirectoryInfo(dir);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            CopyDirectory(dir, Path.Combine(destination, info.Name));
        }
    }

    private static void RunCmd(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });
        if (process is null) throw new InvalidOperationException("无法启动 Windows 命令处理器。");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
    }
}
