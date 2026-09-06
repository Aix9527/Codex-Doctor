using System.Text;
using System.Text.Json;

namespace CodexDoctor.Native;

public sealed class CodexLanguageService
{
    private static readonly HashSet<string> SupportedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "language", "locale", "ui_language"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".toml", ".env"
    };

    private static readonly HashSet<string> SupportedUiLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN", "en-US"
    };

    private readonly string _backupFile;
    private readonly HashSet<string> _approvedUiConfigPaths;

    public CodexLanguageService(string? backupFile = null, IEnumerable<string>? approvedUiConfigPaths = null)
    {
        _backupFile = backupFile ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexDoctorV8",
            "language-backup.json");

        _approvedUiConfigPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (approvedUiConfigPaths is not null)
        {
            foreach (var path in approvedUiConfigPaths.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try { _approvedUiConfigPaths.Add(Path.GetFullPath(path)); }
                catch { }
            }
        }
    }

    public CodexLanguageState Detect(CodexDiscoveryResult discovery)
    {
        var candidate = FindSupportedCandidate(discovery);
        if (candidate is null)
        {
            return new CodexLanguageState(
                "未知",
                "未知",
                "未知",
                false,
                true,
                "当前客户端没有已验证的本地界面语言适配器，可由 V8.1.1 尝试应用自身 Settings → General → Language 的 UI Automation 路径");
        }

        var (_, entry) = candidate.Value;
        var ui = string.IsNullOrWhiteSpace(entry.Value) ? "未知" : entry.Value!;
        var preference = LanguageLabel(ui);
        return new CodexLanguageState(
            ui,
            preference,
            preference,
            SupportedUiLanguages.Contains(ui),
            false,
            "已识别经过明确批准的可逆文本语言配置");
    }

    public CodexLanguageState ApplySimplifiedChinese(CodexDiscoveryResult discovery) =>
        ApplyUiLanguage(discovery, "zh-CN");

    public CodexLanguageState ApplyUiLanguage(CodexDiscoveryResult discovery, string targetLanguage)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        if (!SupportedUiLanguages.Contains(targetLanguage))
            return new CodexLanguageState("未知", "未知", "未知", false, true, "目标语言不在可信适配器白名单中，未执行修改");

        var label = LanguageLabel(targetLanguage);
        var candidate = FindSupportedCandidate(discovery);
        if (candidate is null)
        {
            return new CodexLanguageState(
                "未知",
                label,
                label,
                false,
                true,
                "当前客户端没有已验证的本地界面语言适配器；不会把 .codex 中普通 language/locale 字段冒充 Desktop 界面设置，也不会修改内部数据库或安装资源");
        }

        var (file, entry) = candidate.Value;
        if (!File.Exists(file.Path))
            return new CodexLanguageState("未知", label, label, false, true, "语言配置文件已不存在，请重新扫描本机 Codex");

        var previous = ReadCurrentValue(file.Path, entry.Name);
        if (previous is null)
            return new CodexLanguageState("未知", label, label, false, true, "无法安全读取当前语言值，未执行修改");

        var backup = new LanguageBackupDto(file.Path, entry.Name, previous, DateTimeOffset.Now);
        var backupDirectory = Path.GetDirectoryName(_backupFile);
        if (!string.IsNullOrWhiteSpace(backupDirectory)) Directory.CreateDirectory(backupDirectory);
        File.WriteAllText(_backupFile, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        ReplaceValue(file.Path, entry.Name, targetLanguage);
        return new CodexLanguageState(targetLanguage, label, label, true, false, $"已通过经过明确批准的可逆语言适配器设置为 {targetLanguage}");
    }

    public CodexLanguageState RestorePreviousLanguage()
    {
        if (!File.Exists(_backupFile))
            return new CodexLanguageState("未知", "未知", "未知", false, true, "没有可恢复的语言备份");

        LanguageBackupDto? backup;
        try
        {
            backup = JsonSerializer.Deserialize<LanguageBackupDto>(File.ReadAllText(_backupFile, Encoding.UTF8));
        }
        catch
        {
            return new CodexLanguageState("未知", "未知", "未知", false, true, "语言备份无法读取，未执行恢复");
        }

        if (backup is null ||
            !File.Exists(backup.ConfigPath) ||
            !SupportedExtensions.Contains(Path.GetExtension(backup.ConfigPath)) ||
            !SupportedKeys.Contains(backup.Key) ||
            !_approvedUiConfigPaths.Contains(SafeFullPath(backup.ConfigPath)))
        {
            return new CodexLanguageState("未知", "未知", "未知", false, true, "语言备份不符合当前安全适配器的恢复条件");
        }

        ReplaceValue(backup.ConfigPath, backup.Key, backup.PreviousValue);
        var label = LanguageLabel(backup.PreviousValue);
        return new CodexLanguageState(
            backup.PreviousValue,
            label,
            label,
            true,
            false,
            "已恢复原语言");
    }

    private (CodexConfigFileInfo File, CodexConfigEntryInfo Entry)? FindSupportedCandidate(CodexDiscoveryResult discovery)
    {
        foreach (var file in discovery.ConfigFiles)
        {
            if (!file.Exists || !SupportedExtensions.Contains(Path.GetExtension(file.Path))) continue;
            if (!_approvedUiConfigPaths.Contains(SafeFullPath(file.Path))) continue;

            foreach (var entry in file.Entries)
            {
                if (entry.Sensitive || !entry.Configured || !SupportedKeys.Contains(entry.Name)) continue;
                return (file, entry);
            }
        }
        return null;
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return string.Empty; }
    }

    private static string? ReadCurrentValue(string path, string key)
    {
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            var name = line[..equals].Trim();
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            return line[(equals + 1)..].Trim().Trim('"', '\'');
        }
        return null;
    }

    private static void ReplaceValue(string path, string key, string newValue)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var replaced = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0) continue;
            var name = raw[..equals].Trim();
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            var prefix = raw[..(equals + 1)];
            lines[i] = prefix + newValue;
            replaced = true;
            break;
        }
        if (!replaced) throw new InvalidOperationException("语言配置项已发生变化，未执行写入。");
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static string LanguageLabel(string value) => value.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
        ? "简体中文"
        : value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "English" : "未知";

    private sealed record LanguageBackupDto(string ConfigPath, string Key, string PreviousValue, DateTimeOffset CreatedAt);
}
