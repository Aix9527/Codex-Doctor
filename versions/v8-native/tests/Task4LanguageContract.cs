using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class Task4LanguageContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodexDoctorLanguageTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = Path.Combine(root, "config.toml");
            var backup = Path.Combine(root, "language-backup.json");
            File.WriteAllText(config, "theme=dark\nlanguage=en-US\n");
            var discovery = MakeDiscovery(config, "language", "en-US");
            var service = new CodexLanguageService(backup);

            var before = service.Detect(discovery);
            Require(before.UiLanguage == "en-US", "应检测到初始界面语言 en-US。");

            var applied = service.ApplySimplifiedChinese(discovery);
            Require(applied.Applied && !applied.NeedsUserAction, "受支持配置必须能自动切换到简体中文。");
            Require(applied.UiLanguage == "zh-CN", "自动切换后界面语言应为 zh-CN。");
            Require(File.Exists(backup), "修改语言前必须创建可恢复备份。");
            var text = File.ReadAllText(config);
            Require(text.Contains("language=zh-CN"), "受支持配置应写入 language=zh-CN。");
            Require(text.Contains("theme=dark"), "语言修改不得破坏无关配置。");

            var restored = service.RestorePreviousLanguage();
            Require(restored.UiLanguage == "en-US", "恢复后应回到原始语言 en-US。");
            Require(File.ReadAllText(config).Contains("language=en-US"), "恢复必须把原语言写回配置。");

            var unknownConfig = Path.Combine(root, "unknown.db");
            File.WriteAllText(unknownConfig, "binary-ish-language=en-US");
            var unsupported = MakeDiscovery(unknownConfig, "language", "en-US");
            var unsupportedState = service.ApplySimplifiedChinese(unsupported);
            Require(!unsupportedState.Applied && unsupportedState.NeedsUserAction, "未知/不稳定配置必须要求用户操作，不能伪报成功。");
            Require(File.ReadAllText(unknownConfig) == "binary-ish-language=en-US", "未知配置不得被自动修改。");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static CodexDiscoveryResult MakeDiscovery(string configPath, string name, string value)
    {
        return new CodexDiscoveryResult(
            [],
            new CodexCliInfo(false, null, false, null, false),
            new CodexDataDirectoryInfo(Path.GetDirectoryName(configPath)!, true, false, null, 1, 1),
            [new CodexConfigFileInfo(configPath, true, [new CodexConfigEntryInfo(name, value, false, true)])],
            new CodexLanguageState("未知", "未知", "未知", false, true, "尚未检测"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
