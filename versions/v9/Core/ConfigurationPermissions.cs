namespace CodexDoctor.V9;

public static class ConfigurationPermissions
{
    public static void MakeWritable(string path)
    {
        if (Path.GetFileName(path) is not ".env" and not "config.toml") throw new IOException("仅支持已知 Codex 配置文件。");
        var before = File.GetAttributes(path);
        if (before.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("配置是链接，未修改属性。");
        if (!before.HasFlag(FileAttributes.ReadOnly)) return;
        try
        {
            File.SetAttributes(path, before & ~FileAttributes.ReadOnly);
            using var verification = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly)) throw new IOException("只读属性修复后验证失败。");
        }
        catch { File.SetAttributes(path, before); throw; }
    }
}
