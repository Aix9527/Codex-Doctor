using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;

namespace CodexDoctor.V9;

public sealed record ProxyProbe(string Address, bool Reachable, int? StatusCode, string Detail);
public sealed record ProxySearch(string? Selected, IReadOnlyList<ProxyProbe> Probes);

public sealed class LocalProxyDiscovery
{
    private readonly Func<string, CancellationToken, Task<ProxyProbe>> _probe;
    public LocalProxyDiscovery(Func<string, CancellationToken, Task<ProxyProbe>>? probe = null) => _probe = probe ?? ProbeAsync;

    public static IReadOnlyList<string> Candidates(ProxyConfiguration configuration)
    {
        var list = new List<string>();
        void Add(string? raw) { if (ProxyConfiguration.LocalHttpProxy(raw) is { } p && !list.Contains(p)) list.Add(p); }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (Convert.ToInt32(key?.GetValue("ProxyEnable", 0)) == 1)
                foreach (var item in (key?.GetValue("ProxyServer") as string ?? "").Split(';'))
                {
                    var parts = item.Split('=', 2);
                    if (parts.Length == 1 || parts[0] is "http" or "https") Add(parts[^1]);
                }
        }
        catch (System.Security.SecurityException) { }
        foreach (var name in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY" }) Add(Environment.GetEnvironmentVariable(name));
        foreach (var item in configuration.ReadCandidates()) Add(item);
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        // Enumerate actual listening sockets; arbitrary proxy ports are supported, including IPv6.
        foreach (var endpoint in listeners.OrderBy(x => x.Port))
        {
            if (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any))
                Add($"http://127.0.0.1:{endpoint.Port}");
            if (endpoint.Address.Equals(IPAddress.IPv6Loopback) || endpoint.Address.Equals(IPAddress.IPv6Any))
                Add($"http://[::1]:{endpoint.Port}");
        }
        return list;
    }

    public async Task<ProxySearch> FindAsync(IEnumerable<string> candidates, IProgress<string>? progress, CancellationToken ct)
    {
        var ordered = candidates.Select(ProxyConfiguration.LocalHttpProxy).OfType<string>().Distinct().ToArray();
        var results = new List<ProxyProbe>();
        // Batches bound resource use, retain priority order and avoid a failed first candidate blocking discovery.
        foreach (var batch in ordered.Chunk(8))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"正在验证本地代理：已检查 {results.Count}/{ordered.Length} 个监听地址……");
            var probes = await Task.WhenAll(batch.Select(p => _probe(p, ct))).ConfigureAwait(false);
            results.AddRange(probes);
            if (probes.FirstOrDefault(x => x.Reachable) is { } good) return new(good.Address, results);
        }
        return new(null, results);
    }

    public Task<ProxyProbe> VerifyAsync(string address, CancellationToken ct) => _probe(address, ct);

    public static async Task<ProxyProbe> ProbeAsync(string address, CancellationToken ct)
    {
        if (ProxyConfiguration.LocalHttpProxy(address) is null) return new(address, false, null, "不是支持的本地 HTTP 代理。");
        try
        {
            using var handler = new HttpClientHandler { Proxy = new WebProxy(address), UseProxy = true, AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
            using var request = new HttpRequestMessage(HttpMethod.Head, "https://chatgpt.com/");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            // A 403 proves a TLS tunnel but does not prove an account, region or conversation is usable.
            var ok = status is >= 200 and < 500 && status != 407 && status != 429;
            return new(address, ok, status, ok ? $"HTTPS 隧道可用（HTTP {status}）；不代表账号或会话已恢复。" : $"HTTPS 返回 HTTP {status}，需要进一步处理。");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(address, false, null, "代理验证超时。"); }
        catch (HttpRequestException) { return new(address, false, null, "HTTP CONNECT / TLS 验证失败。"); }
    }
}
