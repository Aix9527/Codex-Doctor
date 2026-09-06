using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV811LanguageSwitchContract
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var desktop = new CodexDesktopInstallationInfo("ChatGPT", "test", @"C:\Apps\ChatGPT.exe", "1.0", true, [42]);
        var discovery = CodexDiscoveryResult.Empty() with { DesktopClients = [desktop] };

        var trusted = new FakeTrustedAdapter(canApply: true, verified: true);
        var automation = new FakeAutomationBackend(verified: true);
        var service = new DesktopLanguageSwitchService(trusted, automation, null);
        var chinese = await service.SwitchAsync(discovery, DesktopUiLanguage.ChineseSimplified);
        Require(chinese.Success && chinese.Verified, "可信语言适配器切换中文后必须验证成功。");
        Require(trusted.SetCalls == 1, "存在可信适配器时必须优先使用可信适配器。");
        Require(automation.SetCalls == 0, "可信适配器可用时不得同时操作 UI Automation。");
        Require(trusted.LastTarget == "zh-CN", "中文目标必须使用 zh-CN。");

        trusted = new FakeTrustedAdapter(canApply: false, verified: false);
        automation = new FakeAutomationBackend(verified: true);
        service = new DesktopLanguageSwitchService(trusted, automation, null);
        var english = await service.SwitchAsync(discovery, DesktopUiLanguage.English);
        Require(english.Success && english.Verified, "无可信适配器时应通过 UI Automation 切换 English 并验证。");
        Require(automation.SetCalls == 1 && automation.LastTarget == "en-US", "English 目标必须通过 UI Automation 使用 en-US。");
        Require(automation.LastDesktop?.ExecutablePath == desktop.ExecutablePath, "UI Automation 必须绑定扫描确认的 Desktop 实例。");

        automation = new FakeAutomationBackend(verified: false);
        service = new DesktopLanguageSwitchService(new FakeTrustedAdapter(false, false), automation, null);
        var unverified = await service.SwitchAsync(discovery, DesktopUiLanguage.ChineseSimplified);
        Require(!unverified.Success && !unverified.Verified, "无法验证语言状态时禁止返回成功。");

        var noDesktop = await service.SwitchAsync(CodexDiscoveryResult.Empty(), DesktopUiLanguage.English);
        Require(!noDesktop.Success, "未发现 Desktop 时不得尝试伪切换语言。");
    }

    private sealed class FakeTrustedAdapter(bool canApply, bool verified) : ITrustedDesktopLanguageAdapter
    {
        public int SetCalls { get; private set; }
        public string? LastTarget { get; private set; }
        public bool CanApply(CodexDiscoveryResult discovery) => canApply;
        public Task<LanguageBackendResult> SetAsync(CodexDiscoveryResult discovery, string targetLanguage, CancellationToken cancellationToken)
        {
            SetCalls++;
            LastTarget = targetLanguage;
            return Task.FromResult(new LanguageBackendResult(true, verified, false, "trusted"));
        }
        public Task<bool> VerifyAsync(CodexDiscoveryResult discovery, string targetLanguage, CancellationToken cancellationToken) => Task.FromResult(verified);
    }

    private sealed class FakeAutomationBackend(bool verified) : IUiLanguageAutomationBackend
    {
        public int SetCalls { get; private set; }
        public string? LastTarget { get; private set; }
        public CodexDesktopInstallationInfo? LastDesktop { get; private set; }
        public Task<LanguageBackendResult> SetAsync(CodexDesktopInstallationInfo desktop, string targetLanguage, CancellationToken cancellationToken)
        {
            SetCalls++;
            LastTarget = targetLanguage;
            LastDesktop = desktop;
            return Task.FromResult(new LanguageBackendResult(true, verified, false, "automation"));
        }
        public Task<bool> VerifyAsync(CodexDesktopInstallationInfo desktop, string targetLanguage, CancellationToken cancellationToken) => Task.FromResult(verified);
    }
}
