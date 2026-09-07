namespace CodexDoctor.V9;

public sealed record ReconnectResult(bool ConfigurationVerified, string Summary, ProxySearch Search, ConfigChange? Change);

public sealed class ReconnectRepair(ProxyConfiguration configuration, LocalProxyDiscovery discovery)
{
    public async Task<ReconnectResult> RunAsync(IEnumerable<string> candidates, IProgress<string>? progress, CancellationToken ct)
    {
        var search = await discovery.FindAsync(candidates, progress, ct).ConfigureAwait(false);
        if (search.Selected is null) return new(false, "没有找到通过 HTTPS 验证的本地 HTTP 代理，未修改 .env。请启动代理软件并启用 HTTP/混合端口后重试。", search, null);
        ct.ThrowIfCancellationRequested();
        progress?.Report($"已验证 {search.Selected}，正在备份并更新 HTTP_PROXY / HTTPS_PROXY……");
        var change = configuration.Write(search.Selected);
        try
        {
            var after = await discovery.VerifyAsync(search.Selected, ct).ConfigureAwait(false);
            if (!after.Reachable || !configuration.Matches(search.Selected)) throw new IOException("写入后代理复检失败。");
            return new(true, change.Changed
                ? "代理配置已写入并通过复检。请完全退出并重新打开 Codex Desktop，再验证 Reconnecting 是否消失。"
                : "代理配置已正确，无需重复写入；HTTPS 复检通过。若仍在 Reconnecting，请重启客户端并检查登录、额度或服务状态。", search, change);
        }
        catch
        {
            // Rollback must run even if cancellation caused the failed verification.
            configuration.Rollback(change);
            throw;
        }
    }
}
