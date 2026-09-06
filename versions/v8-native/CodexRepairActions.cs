using System.Diagnostics;
using System.Text.Json;

namespace CodexDoctor.Native;

public sealed record ToolProxySettings(string? HttpProxy, string? HttpsProxy)
{
    public static ToolProxySettings Empty { get; } = new(null, null);
    public bool IsEmpty => string.IsNullOrWhiteSpace(HttpProxy) && string.IsNullOrWhiteSpace(HttpsProxy);
}

public interface IProxyToolRepairBackend
{
    ToolProxySettings Capture();
    void Clear();
    void Restore(ToolProxySettings state);
}

public interface IDesktopRestartBackend
{
    void Restart(string executablePath, IReadOnlyCollection<int> processIds);
    bool IsRunning(string executablePath);
}

public sealed class GitProxyRepairAction : ProxyToolRepairActionBase
{
    public GitProxyRepairAction(IProxyToolRepairBackend backend, string backupDirectory)
        : base("git.proxy.clear", "清理 Git 冲突代理", backend, backupDirectory, "git-proxy") { }
}

public sealed class NpmProxyRepairAction : ProxyToolRepairActionBase
{
    public NpmProxyRepairAction(IProxyToolRepairBackend backend, string backupDirectory)
        : base("npm.proxy.clear", "清理 npm 冲突代理", backend, backupDirectory, "npm-proxy") { }
}

public abstract class ProxyToolRepairActionBase : IRepairAction
{
    private readonly IProxyToolRepairBackend _backend;
    private readonly string _backupDirectory;
    private readonly string _backupPrefix;

    protected ProxyToolRepairActionBase(
        string actionId,
        string titleZh,
        IProxyToolRepairBackend backend,
        string backupDirectory,
        string backupPrefix)
    {
        ActionId = actionId;
        TitleZh = titleZh;
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _backupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
        _backupPrefix = backupPrefix;
    }

    public string ActionId { get; }
    public string TitleZh { get; }
    public bool RequiresRestart => false;
    public bool BackupRequired => true;

    public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var original = _backend.Capture();
        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(
            _backupDirectory,
            $"{_backupPrefix}-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        File.WriteAllText(backupPath, JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true }));
        _backend.Clear();
        return Task.FromResult(new RepairActionExecution(backupPath, $"已清理 {TitleZh}，原设置已备份。"));
    }

    public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_backend.Capture().IsEmpty);
    }

    public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(execution.BackupPath) || !File.Exists(execution.BackupPath))
            throw new InvalidOperationException("找不到代理修复备份，无法安全回滚。");
        var state = JsonSerializer.Deserialize<ToolProxySettings>(File.ReadAllText(execution.BackupPath))
            ?? throw new InvalidOperationException("代理修复备份无效，无法安全回滚。");
        _backend.Restore(state);
        return Task.CompletedTask;
    }
}

public sealed class CodexRestartRepairAction : IRepairAction
{
    private readonly IDesktopRestartBackend _backend;
    private readonly string _executablePath;
    private readonly IReadOnlyCollection<int> _processIds;

    public CodexRestartRepairAction(
        IDesktopRestartBackend backend,
        string executablePath,
        IReadOnlyCollection<int>? processIds = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _executablePath = string.IsNullOrWhiteSpace(executablePath)
            ? throw new ArgumentException("Codex Desktop 路径不能为空。", nameof(executablePath))
            : executablePath;
        _processIds = processIds?.Distinct().ToArray() ?? [];
    }

    public string ActionId => "codex.desktop.restart";
    public string TitleZh => "重启 Codex Desktop";
    public bool RequiresRestart => false;
    public bool BackupRequired => false;

    public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _backend.Restart(_executablePath, _processIds);
        return Task.FromResult(new RepairActionExecution(null, "已通过扫描确认的 Desktop 路径执行重启。"));
    }

    public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_backend.IsRunning(_executablePath));
    }

    public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class LanguageRepairAction : IRepairAction
{
    private readonly CodexLanguageService _service;
    private readonly CodexDiscoveryResult _discovery;

    public LanguageRepairAction(CodexLanguageService service, CodexDiscoveryResult discovery)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public string ActionId => "codex.language.zh-cn";
    public string TitleZh => "设置 Codex 简体中文";
    public bool RequiresRestart => false;
    public bool BackupRequired => false;

    public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _service.ApplySimplifiedChinese(_discovery);
        if (!state.Applied || state.NeedsUserAction)
            throw new InvalidOperationException(state.MethodZh);
        return Task.FromResult(new RepairActionExecution(null, state.MethodZh));
    }

    public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var refreshedFiles = _discovery.ConfigFiles
            .Select(file => File.Exists(file.Path) ? CodexDiscoveryService.ScanConfigFile(file.Path) : file)
            .ToArray();
        var refreshed = _discovery with { ConfigFiles = refreshedFiles };
        var state = _service.Detect(refreshed);
        return Task.FromResult(state.Applied && state.UiLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase));
    }

    public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _service.RestorePreviousLanguage();
        if (state.NeedsUserAction)
            throw new InvalidOperationException(state.MethodZh);
        return Task.CompletedTask;
    }
}

public sealed class DesktopRestartBackend : IDesktopRestartBackend
{
    private readonly RepairService _repair;

    public DesktopRestartBackend(RepairService? repair = null)
    {
        _repair = repair ?? new RepairService();
    }

    public void Restart(string executablePath, IReadOnlyCollection<int> processIds)
        => _repair.RestartCodexDesktop(executablePath, processIds);

    public bool IsRunning(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        string expected;
        try { expected = Path.GetFullPath(executablePath); }
        catch { return false; }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var actual = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(actual) &&
                    string.Equals(Path.GetFullPath(actual), expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return false;
    }
}

public sealed class GitProxyToolBackend : ProcessProxyToolBackend
{
    public GitProxyToolBackend() : base(
        "git",
        "config --global --get http.proxy",
        "config --global --get https.proxy",
        ["config --global --unset-all http.proxy", "config --global --unset-all https.proxy"],
        "config --global http.proxy {0}",
        "config --global https.proxy {0}") { }
}

public sealed class NpmProxyToolBackend : ProcessProxyToolBackend
{
    public NpmProxyToolBackend() : base(
        "npm.cmd",
        "config get proxy",
        "config get https-proxy",
        ["config delete proxy", "config delete https-proxy"],
        "config set proxy {0}",
        "config set https-proxy {0}") { }
}

public abstract class ProcessProxyToolBackend : IProxyToolRepairBackend
{
    private readonly string _fileName;
    private readonly string _getHttpArgs;
    private readonly string _getHttpsArgs;
    private readonly IReadOnlyList<string> _clearArgs;
    private readonly string _setHttpTemplate;
    private readonly string _setHttpsTemplate;

    protected ProcessProxyToolBackend(
        string fileName,
        string getHttpArgs,
        string getHttpsArgs,
        IReadOnlyList<string> clearArgs,
        string setHttpTemplate,
        string setHttpsTemplate)
    {
        _fileName = fileName;
        _getHttpArgs = getHttpArgs;
        _getHttpsArgs = getHttpsArgs;
        _clearArgs = clearArgs;
        _setHttpTemplate = setHttpTemplate;
        _setHttpsTemplate = setHttpsTemplate;
    }

    public ToolProxySettings Capture()
        => new(Normalize(Read(_getHttpArgs)), Normalize(Read(_getHttpsArgs)));

    public void Clear()
    {
        foreach (var args in _clearArgs) Run(args, allowNonZero: true);
    }

    public void Restore(ToolProxySettings state)
    {
        Clear();
        if (!string.IsNullOrWhiteSpace(state.HttpProxy))
            Run(string.Format(_setHttpTemplate, Quote(state.HttpProxy!)), allowNonZero: false);
        if (!string.IsNullOrWhiteSpace(state.HttpsProxy))
            Run(string.Format(_setHttpsTemplate, Quote(state.HttpsProxy!)), allowNonZero: false);
    }

    private string? Read(string args)
    {
        try { return Run(args, allowNonZero: true); }
        catch { return null; }
    }

    private string Run(string args, bool allowNonZero)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = _fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"无法启动 {_fileName}。");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(5000))
        {
            try { process.Kill(true); } catch { }
            throw new InvalidOperationException($"{_fileName} 执行超时。");
        }
        if (!allowNonZero && process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"{_fileName} 执行失败。" : error.Trim());
        return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
