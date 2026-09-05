using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace CodexDoctor.Native;

public sealed class DiagnosisService
{
    private readonly string _userProfile;
    private readonly string _envFile;
    private static readonly int[] CommonPorts = [7897, 7890, 7891, 10809, 10808, 20171, 20170];

    public DiagnosisService(string? userProfile = null)
    {
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _envFile = Path.Combine(_userProfile, ".codex", ".env");
    }

    public static FailureClass Classify(bool dnsOk, bool directTlsOk, bool proxyOk, bool envExists, bool envConflict = false)
    {
        if (!dnsOk) return FailureClass.DnsFailure;
        if (envConflict) return FailureClass.EnvironmentConflict;
        if (proxyOk && !envExists) return FailureClass.ProxyRequired;
        if (proxyOk && envExists && !directTlsOk) return FailureClass.DirectNetworkBlocked;
        if (directTlsOk && proxyOk) return FailureClass.Healthy;
        if (directTlsOk && envExists && !proxyOk) return FailureClass.ProxyMisconfigured;
        if (!directTlsOk && !proxyOk) return FailureClass.TlsFailure;
        return FailureClass.Healthy;
    }

    public static (HealthState Health, string NameZh, string RecommendationZh) Describe(FailureClass failure)
    {
        return failure switch
        {
            FailureClass.Healthy => (HealthState.Healthy, "健康", "网络与代理检查通过。"),
            FailureClass.ProxyRequired => (HealthState.Warning, "需要配置代理", "检测到 OpenAI 直连不可用或不稳定，但本机代理可用。建议将已验证代理写入 Codex 专用 .env。"),
            FailureClass.ProxyMisconfigured => (HealthState.Error, "代理配置异常", "Codex 已存在代理配置，但当前代理无法完成 HTTPS 请求。建议更新或移除失效代理。"),
            FailureClass.DirectNetworkBlocked => (HealthState.Warning, "直连受限，代理可用", "OpenAI 直连不可用，但 Codex 专用代理已配置且代理链路可用。可继续使用代理模式。"),
            FailureClass.DnsFailure => (HealthState.Error, "DNS 故障", "无法解析 OpenAI/ChatGPT 域名。请先检查 DNS、网络出口或代理软件的 DNS 设置。"),
            FailureClass.TlsFailure => (HealthState.Error, "TLS/网络连接故障", "直接 TLS 与代理 HTTPS 均不可用。请检查网络出口、代理软件、HTTPS 检查、证书链和系统时间。"),
            FailureClass.EnvironmentConflict => (HealthState.Warning, "环境代理冲突", "Git 或 npm 的全局代理与 Codex 当前代理不一致。建议确认后清理或统一代理配置。"),
            _ => (HealthState.Error, "未知故障", "请导出诊断报告后进一步分析。")
        };
    }

    public async Task<DiagnosisResult> DiagnoseAsync(string requestedProxy, CancellationToken cancellationToken = default)
    {
        var dns = await TestDnsAsync(cancellationToken);
        var tls = dns.Ok ? await TestDirectTlsAsync(cancellationToken) : new ProbeResult(false, "DNS 失败，跳过直接 TLS 检查。");
        var env = ReadCodexEnv();
        var effectiveProxy = requestedProxy.Trim();
        if (string.IsNullOrWhiteSpace(effectiveProxy)) effectiveProxy = env.HttpProxy;
        if (string.IsNullOrWhiteSpace(effectiveProxy)) effectiveProxy = await DetectValidatedProxyAsync(cancellationToken);
        var proxy = string.IsNullOrWhiteSpace(effectiveProxy)
            ? new ProbeResult(false, "未检测到可用代理。")
            : await TestProxyAsync(effectiveProxy, cancellationToken);
        var git = ReadGitProxy(effectiveProxy);
        var npm = ReadNpmProxy(effectiveProxy);
        var tun = ReadTunState();
        var failure = Classify(dns.Ok, tls.Ok, proxy.Ok, env.Exists, git.Conflict || npm.Conflict);
        var description = Describe(failure);
        return new DiagnosisResult(
            "8.0",
            description.Health,
            failure,
            description.NameZh,
            description.RecommendationZh,
            dns,
            tls,
            proxy,
            effectiveProxy,
            env,
            git,
            npm,
            tun,
            CountCodexProcesses());
    }

    public async Task<ProbeResult> TestDnsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var host in new[] { "chatgpt.com", "api.openai.com" })
            {
                var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                if (addresses.Length == 0) return new ProbeResult(false, $"{host} 未解析到地址。");
            }
            return new ProbeResult(true);
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, ex.Message);
        }
    }

    public static async Task<ProbeResult> TestDirectTlsAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("chatgpt.com", 443, timeout.Token);
            await using var ssl = new SslStream(client.GetStream(), false);
            var options = new SslClientAuthenticationOptions
            {
                TargetHost = "chatgpt.com",
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };
            await ssl.AuthenticateAsClientAsync(options, timeout.Token);
            return new ProbeResult(true, Protocol: ssl.SslProtocol.ToString());
        }
        catch (OperationCanceledException)
        {
            return new ProbeResult(false, "TCP/TLS 连接超时。");
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, ex.Message);
        }
    }

    public static async Task<ProbeResult> TestProxyAsync(string proxyUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUrl),
                UseProxy = true,
                AllowAutoRedirect = false
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            using var request = new HttpRequestMessage(HttpMethod.Head, "https://chatgpt.com");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;
            return new ProbeResult(status >= 200 && status < 500, StatusCode: status);
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, ex.Message);
        }
    }

    public ProxyEnvironmentState ReadCodexEnv()
    {
        if (!File.Exists(_envFile)) return new ProxyEnvironmentState(false, "", "");
        var http = "";
        var https = "";
        foreach (var raw in File.ReadAllLines(_envFile))
        {
            var line = raw.Trim();
            if (line.StartsWith("HTTP_PROXY=", StringComparison.OrdinalIgnoreCase)) http = line[(line.IndexOf('=') + 1)..].Trim();
            if (line.StartsWith("HTTPS_PROXY=", StringComparison.OrdinalIgnoreCase)) https = line[(line.IndexOf('=') + 1)..].Trim();
        }
        return new ProxyEnvironmentState(true, http, https);
    }

    private async Task<string> DetectValidatedProxyAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var systemProxy = ReadWindowsSystemProxy();
        if (!string.IsNullOrWhiteSpace(systemProxy)) candidates.Add(systemProxy);
        foreach (var port in CommonPorts)
        {
            if (await IsLocalPortOpenAsync(port, cancellationToken)) candidates.Add($"http://127.0.0.1:{port}");
        }
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if ((await TestProxyAsync(candidate, cancellationToken)).Ok) return candidate;
        }
        return "";
    }

    private static string ReadWindowsSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (Convert.ToInt32(key?.GetValue("ProxyEnable", 0) ?? 0) != 1) return "";
            var raw = Convert.ToString(key?.GetValue("ProxyServer", "")) ?? "";
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var item = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                          .Select(x => x.Contains('=') ? x[(x.IndexOf('=') + 1)..] : x)
                          .FirstOrDefault(x => x.Contains("127.0.0.1") || x.Contains("localhost"));
            if (string.IsNullOrWhiteSpace(item)) return "";
            return item.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? item : $"http://{item}";
        }
        catch { return ""; }
    }

    private static async Task<bool> IsLocalPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(350));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            return client.Connected;
        }
        catch { return false; }
    }

    private static ConflictState ReadGitProxy(string expected)
    {
        var http = RunCapture("git", "config --global --get http.proxy");
        var https = RunCapture("git", "config --global --get https.proxy");
        var values = new[] { http, https }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var conflict = !string.IsNullOrWhiteSpace(expected) && values.Any(x => !string.Equals(x, expected, StringComparison.OrdinalIgnoreCase));
        return new ConflictState(http, https, conflict);
    }

    private static ConflictState ReadNpmProxy(string expected)
    {
        var http = NormalizeNull(RunCapture("npm.cmd", "config get proxy"));
        var https = NormalizeNull(RunCapture("npm.cmd", "config get https-proxy"));
        var values = new[] { http, https }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var conflict = !string.IsNullOrWhiteSpace(expected) && values.Any(x => !string.Equals(x, expected, StringComparison.OrdinalIgnoreCase));
        return new ConflictState(http, https, conflict);
    }

    private static string NormalizeNull(string value) => string.Equals(value.Trim(), "null", StringComparison.OrdinalIgnoreCase) ? "" : value.Trim();

    private static string RunCapture(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null) return "";
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2500);
            return output;
        }
        catch { return ""; }
    }

    private static TunState ReadTunState()
    {
        var processNames = new[] { "clash-verge", "clash-verge-service", "mihomo", "clash", "sing-box" };
        var foundProcesses = Process.GetProcesses()
            .Select(p => p.ProcessName)
            .Where(n => processNames.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        (n.Name.Contains("TUN", StringComparison.OrdinalIgnoreCase) ||
                         n.Name.Contains("Clash", StringComparison.OrdinalIgnoreCase) ||
                         n.Name.Contains("Mihomo", StringComparison.OrdinalIgnoreCase) ||
                         n.Description.Contains("Wintun", StringComparison.OrdinalIgnoreCase)))
            .Select(n => n.Name)
            .ToArray();
        return new TunState(foundProcesses.Length > 0, adapters.Length > 0, foundProcesses, adapters);
    }

    private static int CountCodexProcesses()
    {
        return Process.GetProcesses().Count(p => p.ProcessName.Contains("Codex", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase));
    }
}
