using Microsoft.Win32;
using System.Text.Json;

namespace CodexDoctor.Native;

public interface IHealthCheck
{
    string Id { get; }
    string NameZh { get; }
    Task<IReadOnlyList<CodexIssue>> RunAsync(CancellationToken cancellationToken);
}

public sealed record HealthScanProgress(string StageId, string StageNameZh, int Completed, int Total);

public sealed class CodexHealthScanner
{
    private readonly IReadOnlyList<IHealthCheck> _checks;
    private readonly Func<Task<CodexDiscoveryResult>> _discoverySnapshot;
    private readonly Func<Task<DiagnosisResult?>> _diagnosisSnapshot;

    public CodexHealthScanner(IEnumerable<IHealthCheck> checks)
        : this(checks, () => Task.FromResult(CodexDiscoveryResult.Empty()), () => Task.FromResult<DiagnosisResult?>(null))
    {
    }

    private CodexHealthScanner(
        IEnumerable<IHealthCheck> checks,
        Func<Task<CodexDiscoveryResult>> discoverySnapshot,
        Func<Task<DiagnosisResult?>> diagnosisSnapshot)
    {
        _checks = checks?.ToArray() ?? throw new ArgumentNullException(nameof(checks));
        _discoverySnapshot = discoverySnapshot;
        _diagnosisSnapshot = diagnosisSnapshot;
    }

    public static CodexHealthScanner CreateDefault(string? userProfile = null, string? localAppData = null, string? appData = null)
    {
        var discoveryService = new CodexDiscoveryService(userProfile, localAppData, appData);
        var diagnosisService = new DiagnosisService(userProfile);
        var languageService = new CodexLanguageService();

        var discovery = new Lazy<Task<CodexDiscoveryResult>>(() => discoveryService.ScanAsync());
        var diagnosis = new Lazy<Task<DiagnosisResult?>>(() => RunDiagnosisAsync(diagnosisService));
        var checks = BuildDefaultChecks(discovery, diagnosis, languageService, localAppData);

        return new CodexHealthScanner(checks, () => discovery.Value, () => diagnosis.Value);
    }

    public async Task<CodexHealthScanResult> ScanAsync(
        IProgress<HealthScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<CodexIssue>();
        var total = _checks.Count;
        var completed = 0;

        foreach (var check in _checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var checkIssues = await check.RunAsync(cancellationToken).ConfigureAwait(false);
                issues.AddRange(checkIssues);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                issues.Add(Issue(
                    $"check-failed:{check.Id}",
                    "scanner",
                    CodexIssueSeverity.Warning,
                    $"{check.NameZh}检查未完成",
                    "该检查发生异常，其他检查已继续执行。",
                    $"异常类型：{ex.GetType().Name}",
                    "该项目需要人工复核，不能据此执行自动修复。",
                    CodexIssueStatus.ManualRequired,
                    check.Id));
            }
            finally
            {
                completed++;
                progress?.Report(new HealthScanProgress(check.Id, check.NameZh, completed, total));
            }
        }

        CodexDiscoveryResult discovery;
        DiagnosisResult? diagnosis;
        try { discovery = await _discoverySnapshot().ConfigureAwait(false); }
        catch { discovery = CodexDiscoveryResult.Empty(); }
        try { diagnosis = await _diagnosisSnapshot().ConfigureAwait(false); }
        catch { diagnosis = null; }

        return new CodexHealthScanResult(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            CodexIssueClassifier.SortIssues(issues),
            discovery,
            diagnosis);
    }

    private static IReadOnlyList<IHealthCheck> BuildDefaultChecks(
        Lazy<Task<CodexDiscoveryResult>> discovery,
        Lazy<Task<DiagnosisResult?>> diagnosis,
        CodexLanguageService language,
        string? localAppData)
    {
        var migrationRoot = Path.Combine(
            localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexDoctorV8");
        var migrationStatePath = Path.Combine(migrationRoot, "migration-state.json");

        return new IHealthCheck[]
        {
            Check("admin-system", "管理员权限与系统", _ => Task.FromResult<IReadOnlyList<CodexIssue>>([CheckAdminAndSystem()])),
            Check("desktop-install", "Codex Desktop 安装", async _ => [CheckDesktopInstall(await discovery.Value.ConfigureAwait(false))]),
            Check("desktop-running", "Codex Desktop 运行状态", async _ => [CheckDesktopRunning(await discovery.Value.ConfigureAwait(false))]),
            Check("cli", "Codex CLI", async _ => [CheckCli(await discovery.Value.ConfigureAwait(false))]),
            Check("codex-data", ".codex 数据目录", async _ => [CheckDataDirectory(await discovery.Value.ConfigureAwait(false))]),
            Check("codex-link", ".codex Junction", async _ => [CheckDataLink(await discovery.Value.ConfigureAwait(false))]),
            Check("migration-state", "迁移状态", async _ => [CheckMigrationState(await discovery.Value.ConfigureAwait(false), migrationStatePath)]),
            Check("config-files", "Codex 配置文件", async _ => [CheckConfigFiles(await discovery.Value.ConfigureAwait(false))]),
            Check("codex-proxy-env", "Codex 代理配置", async _ => [CheckCodexProxy(await diagnosis.Value.ConfigureAwait(false))]),
            Check("windows-user-proxy-env", "Windows 用户代理环境变量", _ => Task.FromResult<IReadOnlyList<CodexIssue>>([CheckWindowsUserProxy()])),
            Check("windows-system-proxy", "Windows 系统代理", _ => Task.FromResult<IReadOnlyList<CodexIssue>>([CheckWindowsSystemProxy()])),
            Check("git-proxy", "Git 代理", async _ => [CheckGitProxy(await diagnosis.Value.ConfigureAwait(false))]),
            Check("npm-proxy", "npm 代理", async _ => [CheckNpmProxy(await diagnosis.Value.ConfigureAwait(false))]),
            Check("dns", "DNS", async _ => [CheckDns(await diagnosis.Value.ConfigureAwait(false))]),
            Check("direct-tls", "直接 TLS", async _ => [CheckDirectTls(await diagnosis.Value.ConfigureAwait(false))]),
            Check("proxy-https", "代理 HTTPS", async _ => [CheckProxyHttps(await diagnosis.Value.ConfigureAwait(false))]),
            Check("proxy-process", "代理软件进程", async _ => [CheckProxyProcess(await diagnosis.Value.ConfigureAwait(false))]),
            Check("tun", "TUN 网卡", async _ => [CheckTun(await diagnosis.Value.ConfigureAwait(false))]),
            Check("network-path", "Codex 可用网络路径", async _ => [CheckNetworkPath(await diagnosis.Value.ConfigureAwait(false))]),
            Check("language", "Codex 中文状态", async _ => [CheckLanguage(language, await discovery.Value.ConfigureAwait(false))]),
            Check("config-write", "Codex 配置权限", async _ => [CheckConfigWritable(await discovery.Value.ConfigureAwait(false))]),
            Check("launch-path", "Codex 启动路径", async _ => [CheckLaunchPath(await discovery.Value.ConfigureAwait(false))])
        };
    }

    private static IHealthCheck Check(
        string id,
        string nameZh,
        Func<CancellationToken, Task<IReadOnlyList<CodexIssue>>> run) => new DelegateHealthCheck(id, nameZh, run);

    private static async Task<DiagnosisResult?> RunDiagnosisAsync(DiagnosisService service)
    {
        try { return await service.DiagnoseAsync(string.Empty).ConfigureAwait(false); }
        catch { return null; }
    }

    private static CodexIssue CheckAdminAndSystem()
    {
        var admin = AdminGuard.IsAdministrator();
        var supported = OperatingSystem.IsWindows();
        return admin && supported
            ? Ok("admin-system", "system", "管理员权限与系统正常", $"Windows={Environment.OSVersion.VersionString}；管理员=是", "admin-system")
            : Issue("admin-system", "system", CodexIssueSeverity.Critical, "运行权限或系统不满足要求", "V8.1 需要 Windows 管理员上下文。", $"Windows={supported}；管理员={admin}", "维修动作可能失败。", CodexIssueStatus.ManualRequired, "admin-system");
    }

    private static CodexIssue CheckDesktopInstall(CodexDiscoveryResult d) => d.DesktopClients.Count > 0
        ? Ok("desktop-install", "desktop", "已发现 Codex Desktop", $"发现 {d.DesktopClients.Count} 个客户端。", "desktop-install")
        : Issue("desktop-missing", "desktop", CodexIssueSeverity.Critical, "未发现 Codex Desktop", "未扫描到 Codex/ChatGPT Desktop 可执行文件。", "Desktop=0", "无法从维修中心启动或重启桌面客户端。", CodexIssueStatus.ManualRequired, "desktop-install");

    private static CodexIssue CheckDesktopRunning(CodexDiscoveryResult d)
    {
        if (d.DesktopClients.Count == 0) return Info("desktop-running-na", "desktop", "未检查运行状态", "尚未发现 Desktop。", "desktop-running", CodexIssueStatus.NotApplicable);
        return d.DesktopClients.Any(x => x.IsRunning)
            ? Ok("desktop-running", "desktop", "Codex Desktop 正在运行", "至少一个已发现实例正在运行。", "desktop-running")
            : Info("desktop-stopped", "desktop", "Codex Desktop 当前未运行", "安装已发现，可由启动按钮启动。", "desktop-running");
    }

    private static CodexIssue CheckCli(CodexDiscoveryResult d) => d.Cli.Found
        ? Ok("cli-found", "cli", "已发现 Codex CLI", d.Cli.PathCallable ? "CLI 可从 PATH 调用。" : "CLI 已发现但不在 PATH。", "cli")
        : Info("cli-missing", "cli", "未发现 Codex CLI", "Codex Desktop 可独立使用；codex doctor 功能将不可用。", "cli");

    private static CodexIssue CheckDataDirectory(CodexDiscoveryResult d) => d.DataDirectory.Exists
        ? Ok("codex-data", "data", ".codex 数据目录存在", $"文件数={d.DataDirectory.FileCount}；大小={d.DataDirectory.SizeBytes} 字节。", "codex-data")
        : Info("codex-data-missing", "data", ".codex 数据目录尚不存在", "首次运行 Codex 前可能属于正常状态。", "codex-data");

    private static CodexIssue CheckDataLink(CodexDiscoveryResult d)
    {
        var data = d.DataDirectory;
        if (!data.Exists || !data.IsReparsePoint) return Ok("codex-link-normal", "migration", ".codex 当前为普通目录", "未检测到 Junction/Reparse Point。", "codex-link");
        var targetOk = !string.IsNullOrWhiteSpace(data.LinkTarget) && Directory.Exists(ResolveLinkTarget(data.Path, data.LinkTarget));
        return targetOk
            ? Ok("codex-link-valid", "migration", ".codex Junction 有效", "链接目标存在。", "codex-link")
            : Issue("codex-link-broken", "migration", CodexIssueSeverity.Critical, ".codex Junction 目标无效", "检测到重解析点但目标不存在或无法解析。", "Junction=是；目标有效=否", "Codex 可能无法读取配置与会话数据。", CodexIssueStatus.ManualRequired, "codex-link");
    }

    private static CodexIssue CheckMigrationState(CodexDiscoveryResult d, string statePath)
    {
        if (!File.Exists(statePath)) return Ok("migration-state-none", "migration", "没有待处理迁移事务", "未发现迁移状态文件。", "migration-state");
        try
        {
            var state = JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(statePath));
            if (state is null) throw new InvalidDataException();
            var coherent = d.DataDirectory.IsReparsePoint && Directory.Exists(state.Target);
            return coherent
                ? Ok("migration-state-valid", "migration", "迁移状态一致", "迁移记录与当前 Junction 状态一致。", "migration-state")
                : Issue("migration-state-inconsistent", "migration", CodexIssueSeverity.Urgent, "迁移状态需要处理", "检测到迁移记录与当前目录状态不一致。", "存在 migration-state.json；状态不一致", "继续迁移/恢复前应先确认恢复路径。", CodexIssueStatus.ManualRequired, "migration-state");
        }
        catch
        {
            return Issue("migration-state-invalid", "migration", CodexIssueSeverity.Warning, "迁移状态文件无法解析", "迁移状态记录无效。", "migration-state.json 解析失败", "自动迁移/恢复将被禁止。", CodexIssueStatus.ManualRequired, "migration-state");
        }
    }

    private static CodexIssue CheckConfigFiles(CodexDiscoveryResult d)
    {
        var existing = d.ConfigFiles.Count(x => x.Exists);
        return existing > 0
            ? Ok("config-files", "config", "已发现 Codex 配置", $"已发现 {existing} 个已知配置文件。", "config-files")
            : Info("config-files-none", "config", "未发现已知 Codex 配置文件", ".env/config.toml 当前均不存在。", "config-files");
    }

    private static CodexIssue CheckCodexProxy(DiagnosisResult? n)
    {
        if (n is null) return Manual("codex-proxy-unknown", "network", "Codex 代理检查未完成", "未获得网络诊断快照。", "codex-proxy-env");
        var env = n.Env;
        if (env.Exists && !string.IsNullOrWhiteSpace(env.HttpProxy) && string.Equals(env.HttpProxy, env.HttpsProxy, StringComparison.OrdinalIgnoreCase))
            return Ok("codex-proxy-ok", "network", "Codex 专用代理配置一致", "HTTP_PROXY 与 HTTPS_PROXY 一致。", "codex-proxy-env");
        if (n.Proxy.Ok && !string.IsNullOrWhiteSpace(n.ProxyUrl))
            return Repairable("codex-proxy-fix", "network", CodexIssueSeverity.Urgent, "Codex 专用代理需要修复", "本机存在已验证代理，但 Codex 专用代理缺失或不一致。", "codex.proxy.env", "codex-proxy-env", true);
        return Info("codex-proxy-unused", "network", "当前没有可验证的 Codex 专用代理", "没有已验证代理可供自动写入。", "codex-proxy-env");
    }

    private static CodexIssue CheckWindowsUserProxy()
    {
        var http = Environment.GetEnvironmentVariable("HTTP_PROXY", EnvironmentVariableTarget.User);
        var https = Environment.GetEnvironmentVariable("HTTPS_PROXY", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(http) && string.IsNullOrWhiteSpace(https))
            return Ok("windows-user-proxy-none", "network", "Windows 用户代理环境变量未设置", "V8.1 默认不会写入这些变量。", "windows-user-proxy-env");
        if (string.Equals(http, https, StringComparison.OrdinalIgnoreCase))
            return Info("windows-user-proxy-present", "network", "Windows 用户代理环境变量已配置", "HTTP/HTTPS 用户代理一致；扫描只读。", "windows-user-proxy-env");
        return Issue("windows-user-proxy-mismatch", "network", CodexIssueSeverity.Warning, "Windows 用户代理环境变量不一致", "HTTP_PROXY 与 HTTPS_PROXY 不一致。", "用户级代理存在不一致", "可能影响其他命令行程序；默认一键修复不会修改它。", CodexIssueStatus.ManualRequired, "windows-user-proxy-env");
    }

    private static CodexIssue CheckWindowsSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false);
            var enabled = Convert.ToInt32(key?.GetValue("ProxyEnable", 0) ?? 0) == 1;
            return enabled
                ? Info("windows-system-proxy-on", "network", "Windows 系统代理已启用", "系统代理存在；扫描阶段未修改注册表。", "windows-system-proxy")
                : Ok("windows-system-proxy-off", "network", "Windows 系统代理未启用", "未检测到启用状态。", "windows-system-proxy");
        }
        catch
        {
            return Manual("windows-system-proxy-unknown", "network", "无法读取 Windows 系统代理", "注册表只读检查失败。", "windows-system-proxy");
        }
    }

    private static CodexIssue CheckGitProxy(DiagnosisResult? n)
    {
        if (n is null) return Manual("git-proxy-unknown", "git", "Git 代理检查未完成", "未获得网络诊断快照。", "git-proxy");
        return n.Git.Conflict
            ? Repairable("git-proxy-conflict", "git", CodexIssueSeverity.Warning, "Git 全局代理与 Codex 网络路径冲突", "Git 代理与当前已验证 Codex 代理不一致。", "git.proxy.cleanup", "git-proxy", true)
            : Ok("git-proxy-ok", "git", "Git 代理无冲突", "未发现与 Codex 当前网络路径冲突。", "git-proxy");
    }

    private static CodexIssue CheckNpmProxy(DiagnosisResult? n)
    {
        if (n is null) return Manual("npm-proxy-unknown", "npm", "npm 代理检查未完成", "未获得网络诊断快照。", "npm-proxy");
        return n.Npm.Conflict
            ? Repairable("npm-proxy-conflict", "npm", CodexIssueSeverity.Warning, "npm 代理与 Codex 网络路径冲突", "npm 代理与当前已验证 Codex 代理不一致。", "npm.proxy.cleanup", "npm-proxy", true)
            : Ok("npm-proxy-ok", "npm", "npm 代理无冲突", "未发现与 Codex 当前网络路径冲突。", "npm-proxy");
    }

    private static CodexIssue CheckDns(DiagnosisResult? n) => n?.Dns.Ok == true
        ? Ok("dns-ok", "network", "DNS 正常", "OpenAI/ChatGPT 域名可解析。", "dns")
        : Issue("dns-fail", "network", CodexIssueSeverity.Critical, "DNS 故障", "OpenAI/ChatGPT 域名解析失败或未完成。", n?.Dns.Error ?? "无诊断结果", "Codex 无法建立可靠网络连接。", CodexIssueStatus.ManualRequired, "dns");

    private static CodexIssue CheckDirectTls(DiagnosisResult? n)
    {
        if (n?.DirectTls.Ok == true) return Ok("tls-direct-ok", "network", "OpenAI 直接 TLS 正常", n.DirectTls.Protocol ?? "TLS 成功", "direct-tls");
        if (n?.Proxy.Ok == true) return Info("tls-direct-blocked", "network", "OpenAI 直连受限，但代理可用", "存在可用代理链路，因此无需把直连失败视为严重故障。", "direct-tls");
        return Issue("tls-direct-fail", "network", CodexIssueSeverity.Critical, "直接 TLS 不可用", "直连 TLS 失败且当前没有已验证代理替代。", n?.DirectTls.Error ?? "无诊断结果", "Codex 联网可能完全不可用。", CodexIssueStatus.ManualRequired, "direct-tls");
    }

    private static CodexIssue CheckProxyHttps(DiagnosisResult? n) => n?.Proxy.Ok == true
        ? Ok("proxy-https-ok", "network", "代理 HTTPS 可用", "已通过 HTTPS 实际验证。", "proxy-https")
        : Info("proxy-https-none", "network", "未验证到可用代理 HTTPS", n?.Proxy.Error ?? "未发现代理", "proxy-https");

    private static CodexIssue CheckProxyProcess(DiagnosisResult? n) => n?.Tun.ProcessDetected == true
        ? Info("proxy-process-found", "network", "检测到代理相关进程", string.Join(", ", n.Tun.Processes), "proxy-process")
        : Info("proxy-process-none", "network", "未检测到已知代理进程", "Clash/Mihomo/sing-box 未命中已知进程名。", "proxy-process");

    private static CodexIssue CheckTun(DiagnosisResult? n) => n?.Tun.AdapterDetected == true
        ? Info("tun-on", "network", "检测到 TUN 网卡", string.Join(", ", n.Tun.Adapters), "tun")
        : Info("tun-off", "network", "未检测到 TUN 网卡", "HTTP 代理可用时 TUN 并非必需。", "tun");

    private static CodexIssue CheckNetworkPath(DiagnosisResult? n)
    {
        if (n is null) return Manual("network-path-unknown", "network", "无法确定 Codex 网络路径", "网络诊断未完成。", "network-path");
        if (n.DirectTls.Ok || n.Proxy.Ok) return Ok("network-path-ok", "network", "Codex 存在可用网络路径", n.DirectTls.Ok ? "可直接连接。" : "可通过已验证代理连接。", "network-path");
        return Issue("network-path-fail", "network", CodexIssueSeverity.Critical, "Codex 没有可用网络路径", "直连与代理链路均不可用。", "DirectTLS=失败；ProxyHTTPS=失败", "Codex 可能持续 Reconnecting 或完全无法联网。", CodexIssueStatus.ManualRequired, "network-path");
    }

    private static CodexIssue CheckLanguage(CodexLanguageService service, CodexDiscoveryResult d)
    {
        var state = service.Detect(d);
        if (state.Applied || state.UiLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            return Ok("language-zh", "language", "Codex 已是简体中文", "检测到可信 UI 语言状态为 zh-CN。", "language");
        if (!state.NeedsUserAction)
            return Repairable("language-auto", "language", CodexIssueSeverity.Info, "Codex 可自动设置简体中文", state.MethodZh, "codex.language.zh-cn", "language", true);
        return Info("language-manual", "language", "Codex 中文设置需要用户操作", state.MethodZh, "language", CodexIssueStatus.ManualRequired);
    }

    private static CodexIssue CheckConfigWritable(CodexDiscoveryResult d)
    {
        foreach (var file in d.ConfigFiles.Where(x => x.Exists))
        {
            try
            {
                if (File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly))
                    return Issue("config-readonly", "config", CodexIssueSeverity.Urgent, "Codex 配置文件为只读", "已知配置文件带有 ReadOnly 属性。", Path.GetFileName(file.Path), "自动修复无法安全写入配置。", CodexIssueStatus.ManualRequired, "config-write");
            }
            catch
            {
                return Manual("config-permission-unknown", "config", "Codex 配置权限无法确认", "读取文件属性失败。", "config-write");
            }
        }
        return Ok("config-write-ok", "config", "Codex 配置权限未发现只读阻断", "已知配置文件未标记 ReadOnly。", "config-write");
    }

    private static CodexIssue CheckLaunchPath(CodexDiscoveryResult d)
    {
        if (d.DesktopClients.Count == 0) return Manual("launch-path-missing", "desktop", "没有可用 Codex 启动路径", "未发现 Desktop 安装。", "launch-path", CodexIssueSeverity.Critical);
        var valid = d.DesktopClients.Count(x => !string.IsNullOrWhiteSpace(x.ExecutablePath) && File.Exists(x.ExecutablePath));
        return valid > 0
            ? Ok("launch-path-ok", "desktop", "Codex 启动路径有效", $"有效路径数={valid}。", "launch-path")
            : Manual("launch-path-invalid", "desktop", "已发现的 Codex 启动路径无效", "所有已发现 EXE 当前均不存在。", "launch-path", CodexIssueSeverity.Critical);
    }

    private static string? ResolveLinkTarget(string sourcePath, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        try
        {
            return Path.IsPathRooted(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, target));
        }
        catch { return null; }
    }

    private static CodexIssue Ok(string id, string category, string title, string evidence, string source) =>
        Issue(id, category, CodexIssueSeverity.Ok, title, title, evidence, "", CodexIssueStatus.Detected, source);

    private static CodexIssue Info(string id, string category, string title, string summary, string source, CodexIssueStatus status = CodexIssueStatus.Detected) =>
        Issue(id, category, CodexIssueSeverity.Info, title, summary, summary, "", status, source);

    private static CodexIssue Manual(string id, string category, string title, string summary, string source, CodexIssueSeverity severity = CodexIssueSeverity.Warning) =>
        Issue(id, category, severity, title, summary, summary, "需要人工确认。", CodexIssueStatus.ManualRequired, source);

    private static CodexIssue Repairable(
        string id,
        string category,
        CodexIssueSeverity severity,
        string title,
        string summary,
        string actionId,
        string source,
        bool needsConfirmation) =>
        new(id, category, severity, title, summary, summary, "可通过已知白名单动作修复。", CodexIssueStatus.Repairable, true, actionId, true, false, needsConfirmation, true, actionId + ".verify", source);

    private static CodexIssue Issue(
        string id,
        string category,
        CodexIssueSeverity severity,
        string title,
        string summary,
        string evidence,
        string impact,
        CodexIssueStatus status,
        string source) =>
        new(id, category, severity, title, summary, evidence, impact, status, false, null, false, false, false, false, null, source);

    private sealed class DelegateHealthCheck(
        string id,
        string nameZh,
        Func<CancellationToken, Task<IReadOnlyList<CodexIssue>>> run) : IHealthCheck
    {
        public string Id => id;
        public string NameZh => nameZh;
        public Task<IReadOnlyList<CodexIssue>> RunAsync(CancellationToken cancellationToken) => run(cancellationToken);
    }
}
