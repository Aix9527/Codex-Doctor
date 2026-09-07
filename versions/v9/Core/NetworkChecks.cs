using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.Security;

namespace CodexDoctor.V9;

public static class NetworkChecks
{
    public static async Task<IReadOnlyList<HealthItem>> RunAsync(CancellationToken ct)
    {
        async Task<HealthItem> DnsAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try { var result = await Dns.GetHostAddressesAsync("chatgpt.com", timeout.Token); return new("DNS", result.Length > 0 ? "解析成功" : "解析失败", "DNS 失败时仍会独立探测代理；HTTP 代理可以在远端解析域名。"); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException) { return new("DNS", "未通过", "本机域名解析失败或超时；不会自动修改系统 DNS。"); }
        }
        async Task<HealthItem> TlsAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try { using var tcp = new TcpClient(); await tcp.ConnectAsync("chatgpt.com", 443, timeout.Token); using var tls = new SslStream(tcp.GetStream()); await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "chatgpt.com" }, timeout.Token); return new("直连 TLS", "握手成功", "只证明直连 TLS；不阻止用户主动执行代理重连修复。"); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or System.Security.Authentication.AuthenticationException) { return new("直连 TLS", "未通过", "直连握手失败或超时；请结合代理 HTTPS 结果判断，不会关闭证书验证。"); }
        }
        var results = (await Task.WhenAll(DnsAsync(), TlsAsync())).ToList();
        var adapters = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up &&
            new[] { "tun", "wintun", "clash", "mihomo" }.Any(name => (x.Name + x.Description).Contains(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        results.Add(new("TUN 线索", adapters.Length > 0 ? "发现活动虚拟网卡" : "未发现已知网卡", "网卡名称仅作为线索，不等于流量确实经过 TUN；不会修改网卡或代理软件。"));
        return results;
    }
}
