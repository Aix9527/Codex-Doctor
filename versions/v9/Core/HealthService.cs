namespace CodexDoctor.V9;

public sealed record HealthItem(string Item, string Status, string Detail);
public sealed record HealthSnapshot(DateTimeOffset Time, string DataDirectory, IReadOnlyList<DesktopInstallation> Desktops, ProxySearch Proxy, IReadOnlyList<HealthItem> Items);

public static class HealthService
{
    public static async Task<HealthSnapshot> ScanAsync(string codexHome, string? desktopPath, IProgress<string>? progress, CancellationToken ct)
    {
        var items = new List<HealthItem>();
        var networkTask = NetworkChecks.RunAsync(ct);
        progress?.Report("正在识别 Desktop、CLI 与数据目录……");
        var desktops = DesktopService.Discover(desktopPath);
        items.Add(new("Desktop 安装", desktops.Count > 0 ? "已发现" : "待处理", desktops.Count > 0 ? $"发现 {desktops.Count} 个客户端，运行 {desktops.Count(x => x.ProcessIds.Count > 0)} 个。" : "未发现；可选择实际 Codex.exe 或通过官方安装入口安装。"));
        try { items.Add(new("CLI", "已检查", await InstallationService.CliVersionAsync(ct))); } catch (IOException ex) { items.Add(new("CLI", "待处理", ex.Message)); }
        foreach (var tool in new[] { "git.exe", "node.exe", "npm.cmd", "winget.exe" }) items.Add(new(tool, CommandRunner.Find(tool) is null ? "未发现" : "可调用", CommandRunner.Find(tool) is null ? "相关功能需要先安装此工具。" : "已发现 PATH 入口。"));
        items.Add(new("数据目录", Directory.Exists(codexHome) ? "存在" : "尚未创建", codexHome));
        foreach (var name in new[] { ".env", "config.toml" })
        {
            var file = Path.Combine(codexHome, name);
            var status = !File.Exists(file) ? "未创建" : File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly) ? "只读" : "存在";
            items.Add(new(name, status, name == ".env" ? "一键修复重连可创建或更新代理配置。" : "仅检查文件状态，不覆盖用户模型、MCP 或其他配置。"));
        }
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(codexHome))!);
        if (drive.IsReady) items.Add(new("磁盘空间", drive.AvailableFreeSpace < 1024L * 1024 * 1024 ? "不足 1 GB" : "充足", $"剩余 {drive.AvailableFreeSpace / (1024d * 1024 * 1024):F1} GB。"));
        var configuration = new ProxyConfiguration(codexHome);
        var search = await new LocalProxyDiscovery().FindAsync(LocalProxyDiscovery.Candidates(configuration), progress, ct);
        items.AddRange(await networkTask);
        items.Add(new("本地代理 HTTPS", search.Selected is null ? "未找到可用代理" : "隧道已验证", search.Selected ?? "启动代理软件，启用 HTTP/混合端口后重试；仅 SOCKS 端口不会写为 HTTP 代理。"));
        items.Add(new("Codex 代理配置", search.Selected is not null && configuration.Matches(search.Selected) ? "与有效代理一致" : "需要检查", "修复入口会重新验证当前代理，并写入 HTTP_PROXY / HTTPS_PROXY。"));
        items.Add(new("登录 / 额度 / 会话", "需在客户端验证", "本地网络检查不能证明登录状态、额度和服务可用性；不会读取登录凭据。"));
        return new(DateTimeOffset.Now, codexHome, desktops, search, items);
    }
}
