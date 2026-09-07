using System.Text;

namespace CodexDoctor.Native;

public sealed record ReconnectHealthSnapshot(
    bool DnsOk,
    bool DirectTlsOk,
    bool ProxyOk,
    string ProxyUrl,
    bool CodexEnvExists,
    string HttpProxy,
    string HttpsProxy);

public sealed record ReconnectRepairResult(
    bool Success,
    bool Verified,
    bool ProxyChanged,
    bool Restarted,
    string SummaryZh,
    ReconnectHealthSnapshot? Before,
    ReconnectHealthSnapshot? After,
    string? BackupPath);

public interface IReconnectHealthProbe
{
    Task<ReconnectHealthSnapshot> ProbeAsync(CancellationToken cancellationToken);
}

public interface IReconnectRepairBackend
{
    string ApplyValidatedProxy(string proxyUrl);
    string ClearCodexProxy();
    void RestartDesktop(CodexDesktopInstallationInfo desktop);
}

public sealed class ReconnectRepairService
{
    private readonly IReconnectHealthProbe _probe;
    private readonly IReconnectRepairBackend _backend;

    public ReconnectRepairService(IReconnectHealthProbe probe, IReconnectRepairBackend backend)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task<ReconnectRepairResult> RepairAsync(
        CodexDiscoveryResult discovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        var desktop = CodexDesktopSelector.SelectPreferred(discovery.DesktopClients);
        if (desktop is null)
            return Failed("未发现经过扫描确认的 Codex/ChatGPT Desktop，无法执行重连修复。");

        var before = await _probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (!before.DnsOk)
            return Failed("DNS 当前不可用；在基础域名解析恢复前，不执行代理写入或 Desktop 重启。", before);

        var proxyChanged = false;
        string? backup = null;

        if (before.ProxyOk && !string.IsNullOrWhiteSpace(before.ProxyUrl))
        {
            if (!EnvMatches(before, before.ProxyUrl))
            {
                backup = _backend.ApplyValidatedProxy(before.ProxyUrl);
                proxyChanged = true;
            }
        }
        else if (before.DirectTlsOk)
        {
            if (HasCodexProxy(before))
            {
                backup = _backend.ClearCodexProxy();
                proxyChanged = true;
            }
        }
        else
        {
            return Failed(
                "当前 OpenAI/ChatGPT 直连与已检测代理链路均不可用；没有经过验证的健康网络路径，因此未修改配置。",
                before);
        }

        _backend.RestartDesktop(desktop);
        var after = await _probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        var verified = IsHealthyForCodex(after);
        if (!verified)
        {
            return new ReconnectRepairResult(
                false, false, proxyChanged, true,
                "已执行受控重连修复并重启 Desktop，但复检仍未得到可供 Codex 使用的健康网络路径，因此不报告成功。",
                before, after, backup);
        }

        var path = HasCodexProxy(after) ? "Codex 专用代理" : "直接 TLS";
        return new ReconnectRepairResult(
            true, true, proxyChanged, true,
            $"重连修复已完成并通过网络复检。当前有效路径：{path}。",
            before, after, backup);
    }

    private static bool IsHealthyForCodex(ReconnectHealthSnapshot state)
    {
        if (!state.DnsOk) return false;
        if (HasCodexProxy(state))
            return state.ProxyOk &&
                   !string.IsNullOrWhiteSpace(state.HttpProxy) &&
                   string.Equals(state.HttpProxy, state.HttpsProxy, StringComparison.OrdinalIgnoreCase);
        return state.DirectTlsOk;
    }

    private static bool HasCodexProxy(ReconnectHealthSnapshot state) =>
        !string.IsNullOrWhiteSpace(state.HttpProxy) || !string.IsNullOrWhiteSpace(state.HttpsProxy);

    private static bool EnvMatches(ReconnectHealthSnapshot state, string proxyUrl) =>
        state.CodexEnvExists &&
        string.Equals(state.HttpProxy, proxyUrl, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(state.HttpsProxy, proxyUrl, StringComparison.OrdinalIgnoreCase);

    private static ReconnectRepairResult Failed(string summary, ReconnectHealthSnapshot? before = null) =>
        new(false, false, false, false, summary, before, null, null);
}

public sealed class DiagnosisReconnectHealthProbe : IReconnectHealthProbe
{
    private readonly DiagnosisService _diagnosis;

    public DiagnosisReconnectHealthProbe(DiagnosisService diagnosis)
    {
        _diagnosis = diagnosis ?? throw new ArgumentNullException(nameof(diagnosis));
    }

    public async Task<ReconnectHealthSnapshot> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = await _diagnosis.DiagnoseAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        return new ReconnectHealthSnapshot(
            result.Dns.Ok,
            result.DirectTls.Ok,
            result.Proxy.Ok,
            result.ProxyUrl,
            result.Env.Exists,
            result.Env.HttpProxy,
            result.Env.HttpsProxy);
    }
}

public sealed class ReconnectRepairBackend : IReconnectRepairBackend
{
    private readonly RepairService _repair;

    public ReconnectRepairBackend(RepairService repair)
    {
        _repair = repair ?? throw new ArgumentNullException(nameof(repair));
    }

    public string ApplyValidatedProxy(string proxyUrl) => _repair.WriteCodexProxyEnv(proxyUrl, false);

    public string ClearCodexProxy()
    {
        var path = _repair.EnvFile;
        if (!File.Exists(path)) return string.Empty;

        var backup = path + ".backup_" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(path, backup, true);

        var keep = new List<string>();
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            var trimmed = line.TrimStart();
            if (IsProxyAssignment(trimmed)) continue;
            keep.Add(line);
        }
        File.WriteAllLines(path, keep, new UTF8Encoding(false));
        return backup;
    }

    public void RestartDesktop(CodexDesktopInstallationInfo desktop) =>
        _repair.RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds);

    private static bool IsProxyAssignment(string line) =>
        line.StartsWith("HTTP_PROXY=", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("HTTPS_PROXY=", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("ALL_PROXY=", StringComparison.OrdinalIgnoreCase);
}
