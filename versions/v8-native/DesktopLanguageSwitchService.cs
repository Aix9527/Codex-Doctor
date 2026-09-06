using System.Diagnostics;

namespace CodexDoctor.Native;

public enum DesktopUiLanguage
{
    ChineseSimplified,
    English
}

public sealed record LanguageBackendResult(
    bool Applied,
    bool Verified,
    bool RequiresRestart,
    string SummaryZh);

public sealed record DesktopLanguageSwitchResult(
    bool Success,
    bool Verified,
    string TargetLanguage,
    string MethodZh,
    string SummaryZh);

public interface ITrustedDesktopLanguageAdapter
{
    bool CanApply(CodexDiscoveryResult discovery);
    Task<LanguageBackendResult> SetAsync(CodexDiscoveryResult discovery, string targetLanguage, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(CodexDiscoveryResult discovery, string targetLanguage, CancellationToken cancellationToken);
}

public interface IUiLanguageAutomationBackend
{
    Task<LanguageBackendResult> SetAsync(CodexDesktopInstallationInfo desktop, string targetLanguage, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(CodexDesktopInstallationInfo desktop, string targetLanguage, CancellationToken cancellationToken);
}

public sealed class DesktopLanguageSwitchService
{
    private readonly ITrustedDesktopLanguageAdapter _trusted;
    private readonly IUiLanguageAutomationBackend _automation;
    private readonly IDesktopRestartBackend? _restart;

    public DesktopLanguageSwitchService(
        ITrustedDesktopLanguageAdapter trusted,
        IUiLanguageAutomationBackend automation,
        IDesktopRestartBackend? restart)
    {
        _trusted = trusted ?? throw new ArgumentNullException(nameof(trusted));
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        _restart = restart;
    }

    public async Task<DesktopLanguageSwitchResult> SwitchAsync(
        CodexDiscoveryResult discovery,
        DesktopUiLanguage language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        var target = language == DesktopUiLanguage.ChineseSimplified ? "zh-CN" : "en-US";
        var desktop = CodexDesktopSelector.SelectPreferred(discovery.DesktopClients);
        if (desktop is null)
            return Fail(target, "未发现经过扫描确认的 Codex/ChatGPT Desktop，未执行语言切换。");

        try
        {
            if (_trusted.CanApply(discovery))
            {
                var applied = await _trusted.SetAsync(discovery, target, cancellationToken).ConfigureAwait(false);
                if (!applied.Applied)
                    return Fail(target, applied.SummaryZh, "可信本地语言适配器");

                if (applied.RequiresRestart)
                {
                    if (_restart is null)
                        return Fail(target, "语言适配器要求重启，但当前没有安全的 Desktop 重启后端。", "可信本地语言适配器");
                    _restart.Restart(desktop.ExecutablePath, desktop.ProcessIds);
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }

                var verified = applied.Verified || await _trusted.VerifyAsync(discovery, target, cancellationToken).ConfigureAwait(false);
                return verified
                    ? new DesktopLanguageSwitchResult(true, true, target, "可信本地语言适配器", applied.SummaryZh)
                    : Fail(target, "已执行语言写入，但无法重新读取并确认目标语言，因此不报告成功。", "可信本地语言适配器");
            }

            var automated = await _automation.SetAsync(desktop, target, cancellationToken).ConfigureAwait(false);
            if (!automated.Applied)
                return Fail(target, automated.SummaryZh, "Windows UI Automation");

            if (automated.RequiresRestart)
            {
                if (_restart is null)
                    return Fail(target, "应用要求重启，但当前没有安全的 Desktop 重启后端。", "Windows UI Automation");
                _restart.Restart(desktop.ExecutablePath, desktop.ProcessIds);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
            }

            var automationVerified = automated.Verified || await _automation.VerifyAsync(desktop, target, cancellationToken).ConfigureAwait(false);
            return automationVerified
                ? new DesktopLanguageSwitchResult(true, true, target, "Windows UI Automation", automated.SummaryZh)
                : Fail(target, "已操作应用语言界面，但无法确认目标语言已经生效，因此不报告成功。", "Windows UI Automation");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(target, $"语言切换失败：{ex.GetType().Name}：{ex.Message}");
        }
    }

    private static DesktopLanguageSwitchResult Fail(string target, string summary, string method = "未执行") =>
        new(false, false, target, method, summary);
}

public sealed class CodexTrustedLanguageAdapter : ITrustedDesktopLanguageAdapter
{
    private readonly CodexLanguageService _service;

    public CodexTrustedLanguageAdapter(CodexLanguageService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public bool CanApply(CodexDiscoveryResult discovery) => !_service.Detect(discovery).NeedsUserAction;

    public Task<LanguageBackendResult> SetAsync(
        CodexDiscoveryResult discovery,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _service.ApplyUiLanguage(discovery, targetLanguage);
        return Task.FromResult(new LanguageBackendResult(
            state.Applied && !state.NeedsUserAction,
            false,
            false,
            state.MethodZh));
    }

    public Task<bool> VerifyAsync(CodexDiscoveryResult discovery, string targetLanguage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var refreshedFiles = discovery.ConfigFiles
            .Select(file => File.Exists(file.Path) ? CodexDiscoveryService.ScanConfigFile(file.Path) : file)
            .ToArray();
        var refreshed = discovery with { ConfigFiles = refreshedFiles };
        var state = _service.Detect(refreshed);
        return Task.FromResult(state.UiLanguage.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class WindowsUiAutomationLanguageBackend : IUiLanguageAutomationBackend
{
    private const int NamePropertyId = 30005;
    private const int ValuePatternId = 10002;
    private const int InvokePatternId = 10000;
    private const int ExpandCollapsePatternId = 10005;
    private const int SelectionItemPatternId = 10010;
    private const int TreeScopeDescendants = 4;

    public async Task<LanguageBackendResult> SetAsync(
        CodexDesktopInstallationInfo desktop,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var root = BindConfirmedRoot(desktop);
            if (root is null)
                return new(false, false, false, "没有找到与扫描结果匹配的 Desktop 主窗口，未执行 UI Automation。 ");

            dynamic automation = CreateAutomation();
            dynamic currentRoot = root;

            var settings = FindByNames(automation, currentRoot, ["Settings", "设置"]);
            if (settings is null)
            {
                var account = FindByNames(automation, currentRoot, ["Profile", "Account", "账户", "个人资料", "个人中心"]);
                if (account is not null)
                {
                    TryInvokeOrExpand(account);
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                    currentRoot = BindConfirmedRoot(desktop) ?? currentRoot;
                    settings = FindByNames(automation, currentRoot, ["Settings", "设置"]);
                }
            }
            if (settings is null || !TryInvokeOrExpand(settings))
                return new(false, false, false, "未能在已确认 Desktop 窗口中找到或打开 Settings/设置。 ");

            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            currentRoot = BindConfirmedRoot(desktop) ?? currentRoot;

            var general = FindByNames(automation, currentRoot, ["General", "通用"]);
            if (general is not null) TryInvokeOrExpand(general);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);

            currentRoot = BindConfirmedRoot(desktop) ?? currentRoot;
            var language = FindByNames(automation, currentRoot, ["Language", "语言"]);
            if (language is null || !TryInvokeOrExpand(language))
                return new(false, false, false, "未能在 Settings → General 中找到可自动操作的 Language/语言控件。 ");

            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            currentRoot = BindConfirmedRoot(desktop) ?? currentRoot;
            var targetNames = TargetNames(targetLanguage);
            var target = FindByNames(automation, currentRoot, targetNames);
            if (target is null || !TrySelectOrInvoke(target))
                return new(false, false, false, $"已打开语言控件，但没有找到可选择的目标语言 {targetLanguage}。 ");

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            var verified = await VerifyAsync(desktop, targetLanguage, cancellationToken).ConfigureAwait(false);
            return new(true, verified, false, verified ? $"已通过应用自身语言设置切换并验证 {targetLanguage}。" : $"已选择 {targetLanguage}，但尚无法验证是否已生效。");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, false, false, $"Windows UI Automation 失败：{ex.GetType().Name}：{ex.Message}");
        }
    }

    public Task<bool> VerifyAsync(
        CodexDesktopInstallationInfo desktop,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var root = BindConfirmedRoot(desktop);
            if (root is null) return Task.FromResult(false);
            dynamic automation = CreateAutomation();
            var target = FindByNames(automation, root, TargetNames(targetLanguage));
            if (target is not null && IsSelected(target)) return Task.FromResult(true);

            var language = FindByNames(automation, root, ["Language", "语言"]);
            if (language is not null)
            {
                var value = TryCurrentValue(language);
                if (!string.IsNullOrWhiteSpace(value) && TargetNames(targetLanguage).Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(true);
            }
        }
        catch { }
        return Task.FromResult(false);
    }

    private static dynamic CreateAutomation()
    {
        var type = Type.GetTypeFromProgID("UIAutomationClient.CUIAutomation")
            ?? throw new InvalidOperationException("当前 Windows 无法创建 UI Automation COM 对象。");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法初始化 UI Automation。");
    }

    private static dynamic? BindConfirmedRoot(CodexDesktopInstallationInfo desktop)
    {
        if (desktop.ProcessIds.Count == 0) return null;
        string expected;
        try { expected = Path.GetFullPath(desktop.ExecutablePath); }
        catch { return null; }

        foreach (var pid in desktop.ProcessIds.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                var actual = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(actual) &&
                    !string.Equals(Path.GetFullPath(actual), expected, StringComparison.OrdinalIgnoreCase))
                    continue;

                dynamic automation = CreateAutomation();
                return automation.ElementFromHandle(process.MainWindowHandle);
            }
            catch { }
        }
        return null;
    }

    private static dynamic? FindByNames(dynamic automation, dynamic root, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            try
            {
                dynamic condition = automation.CreatePropertyCondition(NamePropertyId, name);
                dynamic? element = root.FindFirst(TreeScopeDescendants, condition);
                if (element is not null) return element;
            }
            catch { }
        }
        return null;
    }

    private static bool TryInvokeOrExpand(dynamic element)
    {
        try { dynamic p = element.GetCurrentPattern(InvokePatternId); p.Invoke(); return true; } catch { }
        try { dynamic p = element.GetCurrentPattern(ExpandCollapsePatternId); p.Expand(); return true; } catch { }
        try { dynamic p = element.GetCurrentPattern(SelectionItemPatternId); p.Select(); return true; } catch { }
        return false;
    }

    private static bool TrySelectOrInvoke(dynamic element)
    {
        try { dynamic p = element.GetCurrentPattern(SelectionItemPatternId); p.Select(); return true; } catch { }
        try { dynamic p = element.GetCurrentPattern(InvokePatternId); p.Invoke(); return true; } catch { }
        return false;
    }

    private static bool IsSelected(dynamic element)
    {
        try { dynamic p = element.GetCurrentPattern(SelectionItemPatternId); return (bool)p.CurrentIsSelected; }
        catch { return false; }
    }

    private static string? TryCurrentValue(dynamic element)
    {
        try { dynamic p = element.GetCurrentPattern(ValuePatternId); return (string?)p.CurrentValue; }
        catch { return null; }
    }

    private static string[] TargetNames(string targetLanguage) => targetLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
        ? ["简体中文", "中文", "Chinese (Simplified)", "Chinese"]
        : ["English", "English (US)", "English (United States)"];
}
