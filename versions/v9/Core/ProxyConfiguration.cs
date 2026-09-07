using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace CodexDoctor.V9;

public sealed record ConfigChange(string EnvFile, string? BackupPath, bool Changed, string WrittenHash);

public sealed class ProxyConfiguration(string codexHome)
{
    public string EnvFile { get; } = Path.Combine(Path.GetFullPath(codexHome), ".env");
    private static readonly Regex Assignment = new(@"^\s*(?:export\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)$", RegexOptions.CultureInvariant);
    private static bool Managed(string name) => name.Equals("HTTP_PROXY", StringComparison.OrdinalIgnoreCase) || name.Equals("HTTPS_PROXY", StringComparison.OrdinalIgnoreCase);

    public static string? LocalHttpProxy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().Trim('"', '\'');
        if (!value.Contains("://")) value = "http://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/" || uri.Port <= 0 || uri.Port > 65535)
            return null;
        var host = uri.DnsSafeHost.Trim('[', ']');
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            !(System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip))) return null;
        return $"http://{(host.Contains(':') ? "[" + host + "]" : host)}:{uri.Port}";
    }

    public IReadOnlyList<string> ReadCandidates()
    {
        if (!File.Exists(EnvFile)) return [];
        var candidates = new List<string>();
        foreach (var line in File.ReadLines(EnvFile))
        {
            var match = Assignment.Match(line);
            if (!match.Success || !Managed(match.Groups["name"].Value)) continue;
            var value = match.Groups["value"].Value.Split(" #", 2)[0];
            if (LocalHttpProxy(value) is { } proxy) candidates.Add(proxy);
        }
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool Matches(string proxy)
    {
        if (!File.Exists(EnvFile)) return false;
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(EnvFile))
        {
            var match = Assignment.Match(line);
            if (!match.Success || !Managed(match.Groups["name"].Value)) continue;
            if (LocalHttpProxy(match.Groups["value"].Value) != proxy) return false;
            if (!found.Add(match.Groups["name"].Value)) return false;
        }
        return found.Contains("HTTP_PROXY") && found.Contains("HTTPS_PROXY");
    }

    public ConfigChange Write(string proxy)
    {
        proxy = LocalHttpProxy(proxy) ?? throw new InvalidOperationException("只接受无凭据的本机 HTTP 代理地址。");
        Directory.CreateDirectory(Path.GetDirectoryName(EnvFile)!);
        // The home directory may legitimately be a junction. The file itself must not redirect writes.
        if (File.Exists(EnvFile) && File.GetAttributes(EnvFile).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException(".env 是文件链接，无法确认写入目标。");
        if (Matches(proxy)) return new(EnvFile, null, false, Hash(EnvFile));
        var existed = File.Exists(EnvFile);
        var before = existed ? File.ReadAllBytes(EnvFile) : [];
        var original = new UTF8Encoding(false, true).GetString(before).TrimStart('\uFEFF');
        var newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var lines = original.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        lines.RemoveAll(line => { var m = Assignment.Match(line); return m.Success && Managed(m.Groups["name"].Value); });
        lines.AddRange([$"HTTP_PROXY={proxy}", $"HTTPS_PROXY={proxy}", $"http_proxy={proxy}", $"https_proxy={proxy}"]);
        var output = new UTF8Encoding(false).GetBytes(string.Join(newline, lines) + newline);
        var temp = EnvFile + ".doctor-write-" + Guid.NewGuid().ToString("N");
        var backup = existed ? EnvFile + ".doctor-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N") : null;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(output); stream.Flush(true); }
            if (existed)
            {
                if (!File.ReadAllBytes(EnvFile).SequenceEqual(before)) throw new IOException(".env 在修复过程中被其他程序修改，已取消写入。");
                File.Replace(temp, EnvFile, backup);
            }
            else File.Move(temp, EnvFile, false);
            if (!Matches(proxy)) throw new IOException("写入后的代理配置复检失败。");
            return new(EnvFile, backup, true, Hash(EnvFile));
        }
        catch
        {
            if (backup is not null && File.Exists(backup)) File.Copy(backup, EnvFile, true);
            throw;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Rollback(ConfigChange change)
    {
        if (!change.Changed) return;
        if (!Path.GetFullPath(change.EnvFile).Equals(EnvFile, StringComparison.OrdinalIgnoreCase)) throw new IOException("备份不属于当前配置。");
        if (!File.Exists(EnvFile) || Hash(EnvFile) != change.WrittenHash) throw new IOException("修复后配置又有变化，未覆盖新内容；请从备份手动恢复。");
        if (change.BackupPath is null) File.Delete(EnvFile);
        else
        {
            var backup = Path.GetFullPath(change.BackupPath);
            if (!backup.StartsWith(EnvFile + ".doctor-backup-", StringComparison.OrdinalIgnoreCase)) throw new IOException("备份路径无效。");
            var temp = EnvFile + ".doctor-restore-" + Guid.NewGuid().ToString("N");
            try { File.Copy(backup, temp); File.Replace(temp, EnvFile, null); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
}
