using System.Text.Json.Serialization;

namespace CodexDoctor.Native;

public sealed record CodexProcessInfo(
    [property: JsonPropertyName("进程ID")] int ProcessId,
    [property: JsonPropertyName("进程名称")] string ProcessName,
    [property: JsonPropertyName("可执行文件路径")] string? ExecutablePath);

public sealed record CodexDesktopInstallationInfo(
    [property: JsonPropertyName("产品类型")] string ProductType,
    [property: JsonPropertyName("安装来源")] string Source,
    [property: JsonPropertyName("可执行文件路径")] string ExecutablePath,
    [property: JsonPropertyName("版本")] string? Version,
    [property: JsonPropertyName("正在运行")] bool IsRunning,
    [property: JsonPropertyName("进程ID")] IReadOnlyList<int> ProcessIds);

public sealed record CodexCliInfo(
    [property: JsonPropertyName("已发现")] bool Found,
    [property: JsonPropertyName("路径")] string? Path,
    [property: JsonPropertyName("PATH可调用")] bool PathCallable,
    [property: JsonPropertyName("版本")] string? Version,
    [property: JsonPropertyName("支持doctor")] bool SupportsDoctor);

public sealed record CodexDataDirectoryInfo(
    [property: JsonPropertyName("路径")] string Path,
    [property: JsonPropertyName("存在")] bool Exists,
    [property: JsonPropertyName("重解析点")] bool IsReparsePoint,
    [property: JsonPropertyName("链接目标")] string? LinkTarget,
    [property: JsonPropertyName("文件数量")] int FileCount,
    [property: JsonPropertyName("大小字节")] long SizeBytes);

public sealed record CodexConfigEntryInfo
{
    [JsonPropertyName("名称")]
    public string Name { get; init; }

    [JsonPropertyName("值")]
    public string? Value { get; init; }

    [JsonPropertyName("敏感")]
    public bool Sensitive { get; init; }

    [JsonPropertyName("已配置")]
    public bool Configured { get; init; }

    public CodexConfigEntryInfo(string name, string? value, bool sensitive, bool configured)
    {
        Name = name;
        Sensitive = sensitive;
        Configured = configured;
        Value = sensitive ? null : value;
    }
}

public sealed record CodexConfigFileInfo(
    [property: JsonPropertyName("路径")] string Path,
    [property: JsonPropertyName("存在")] bool Exists,
    [property: JsonPropertyName("配置项")] IReadOnlyList<CodexConfigEntryInfo> Entries);

public sealed record CodexLanguageCapability(
    [property: JsonPropertyName("可自动设置界面语言")] bool CanSetUiLanguage,
    [property: JsonPropertyName("可自动设置回答语言")] bool CanSetResponseLanguage,
    [property: JsonPropertyName("可自动设置CLI语言")] bool CanSetCliLanguage,
    [property: JsonPropertyName("说明")] string DescriptionZh);

public sealed record CodexLanguageBackup(
    [property: JsonPropertyName("配置路径")] string ConfigPath,
    [property: JsonPropertyName("原界面语言")] string? PreviousUiLanguage,
    [property: JsonPropertyName("备份时间")] DateTimeOffset CreatedAt);

public sealed record CodexLanguageState(
    [property: JsonPropertyName("当前界面语言")] string UiLanguage,
    [property: JsonPropertyName("回答语言偏好")] string ResponseLanguagePreference,
    [property: JsonPropertyName("CLI输出偏好")] string CliLanguagePreference,
    [property: JsonPropertyName("已应用")] bool Applied,
    [property: JsonPropertyName("需要用户操作")] bool NeedsUserAction,
    [property: JsonPropertyName("设置方式")] string MethodZh);

public sealed record CodexDiscoveryResult(
    [property: JsonPropertyName("桌面客户端")] IReadOnlyList<CodexDesktopInstallationInfo> DesktopClients,
    [property: JsonPropertyName("CodexCLI")] CodexCliInfo Cli,
    [property: JsonPropertyName("数据目录")] CodexDataDirectoryInfo DataDirectory,
    [property: JsonPropertyName("配置文件")] IReadOnlyList<CodexConfigFileInfo> ConfigFiles,
    [property: JsonPropertyName("语言状态")] CodexLanguageState LanguageState);
