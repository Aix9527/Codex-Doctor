namespace CodexDoctor.Native;

public sealed class ProxyEnvRepairAction : IRepairAction
{
    private readonly RepairService _repair;
    private readonly string _proxyUrl;
    private readonly bool _existedBefore;

    public ProxyEnvRepairAction(string userProfile, string proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(userProfile)) throw new ArgumentException("用户目录不能为空。", nameof(userProfile));
        if (string.IsNullOrWhiteSpace(proxyUrl)) throw new ArgumentException("代理地址不能为空。", nameof(proxyUrl));
        _repair = new RepairService(userProfile);
        _proxyUrl = proxyUrl;
        _existedBefore = File.Exists(_repair.EnvFile);
    }

    public string ActionId => "codex.proxy.env";
    public string TitleZh => "修复 Codex 专用代理配置";
    public bool RequiresRestart => true;
    public bool BackupRequired => _existedBefore;

    public Task<RepairActionExecution> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backup = _repair.WriteCodexProxyEnv(_proxyUrl, false);
        return Task.FromResult(new RepairActionExecution(
            string.IsNullOrWhiteSpace(backup) ? null : backup,
            "已更新 Codex 专用 HTTP/HTTPS 代理；未修改 Windows 用户级代理环境变量。"));
    }

    public Task<bool> VerifyAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_repair.EnvFile)) return Task.FromResult(false);
        var http = "";
        var https = "";
        foreach (var raw in File.ReadAllLines(_repair.EnvFile))
        {
            var line = raw.Trim();
            if (line.StartsWith("HTTP_PROXY=", StringComparison.OrdinalIgnoreCase)) http = line[(line.IndexOf('=') + 1)..].Trim();
            if (line.StartsWith("HTTPS_PROXY=", StringComparison.OrdinalIgnoreCase)) https = line[(line.IndexOf('=') + 1)..].Trim();
        }
        return Task.FromResult(
            string.Equals(http, _proxyUrl, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(https, _proxyUrl, StringComparison.OrdinalIgnoreCase));
    }

    public Task RollbackAsync(RepairActionExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_existedBefore)
        {
            if (string.IsNullOrWhiteSpace(execution.BackupPath) || !File.Exists(execution.BackupPath))
                throw new InvalidOperationException("找不到 .env 备份，无法安全回滚。");
            File.Copy(execution.BackupPath, _repair.EnvFile, true);
        }
        else if (File.Exists(_repair.EnvFile))
        {
            File.Delete(_repair.EnvFile);
        }
        return Task.CompletedTask;
    }
}
