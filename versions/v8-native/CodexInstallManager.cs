using System.Diagnostics;

namespace CodexDoctor.Native;

public sealed record InstallCommandCall(string FileName, string Arguments);

public sealed record InstallCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

public sealed record InstallOperationResult(
    bool Success,
    bool Verified,
    bool ManualRequired,
    string SummaryZh,
    int? ExitCode = null);

public interface IInstallCommandRunner
{
    bool IsAvailable(string fileName);
    Task<InstallCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IInstallDiscoveryProvider
{
    Task<CodexDiscoveryResult> ScanAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexInstallManager
{
    public const string DesktopStorePackageId = "9NT1R1C2HH7J";
    public const string CliNpmPackage = "@openai/codex";

    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(20);

    private readonly IInstallCommandRunner _runner;
    private readonly IInstallDiscoveryProvider _discovery;

    public CodexInstallManager(IInstallCommandRunner? runner = null, IInstallDiscoveryProvider? discovery = null)
    {
        _runner = runner ?? new InstallCommandRunner();
        _discovery = discovery ?? new InstallDiscoveryProvider();
    }

    public async Task<InstallOperationResult> InstallDesktopAsync(CancellationToken cancellationToken = default)
    {
        if (!_runner.IsAvailable("winget.exe"))
            return Manual("未发现 winget.exe。不会从第三方下载 Desktop；请先恢复 Windows App Installer / Microsoft Store 后再试。");

        const string args = "install --id 9NT1R1C2HH7J --source msstore --accept-package-agreements --accept-source-agreements --silent";
        var command = await _runner.RunAsync("winget.exe", args, InstallTimeout, cancellationToken).ConfigureAwait(false);
        if (!CommandSucceeded(command)) return CommandFailure("Desktop 安装命令失败", command);

        var after = await _discovery.ScanAsync(cancellationToken).ConfigureAwait(false);
        var verified = after.DesktopClients.Count > 0;
        return verified
            ? Ok("Desktop 已通过 Microsoft Store/winget 安装，并经重新扫描确认。", command.ExitCode)
            : Fail("winget 返回成功，但重新扫描仍未发现 ChatGPT/Codex Desktop，因此不报告安装成功。", command.ExitCode);
    }

    public async Task<InstallOperationResult> UninstallDesktopAsync(CancellationToken cancellationToken = default)
    {
        if (!_runner.IsAvailable("winget.exe"))
            return Manual("未发现 winget.exe，无法使用 Windows 官方包管理器卸载 Desktop。不会直接删除应用目录。");

        const string args = "uninstall --id 9NT1R1C2HH7J --source msstore --silent --accept-source-agreements";
        var command = await _runner.RunAsync("winget.exe", args, InstallTimeout, cancellationToken).ConfigureAwait(false);
        if (!CommandSucceeded(command)) return CommandFailure("Desktop 卸载命令失败", command);

        var after = await _discovery.ScanAsync(cancellationToken).ConfigureAwait(false);
        var verified = after.DesktopClients.Count == 0;
        return verified
            ? Ok("Desktop 已通过 Windows/winget 卸载；用户配置和项目数据未由安装管理器删除。", command.ExitCode)
            : Fail("winget 返回成功，但重新扫描仍发现 Desktop，因此不报告卸载成功。", command.ExitCode);
    }

    public async Task<InstallOperationResult> InstallCliAsync(CancellationToken cancellationToken = default)
    {
        if (!_runner.IsAvailable("npm.cmd"))
            return Manual("未发现 npm.cmd。Codex Doctor 不会自动安装 Node.js；请先从可信来源安装 Node/npm。");

        var command = await _runner.RunAsync("npm.cmd", "install -g @openai/codex", InstallTimeout, cancellationToken).ConfigureAwait(false);
        if (!CommandSucceeded(command)) return CommandFailure("Codex CLI 安装命令失败", command);

        var after = await _discovery.ScanAsync(cancellationToken).ConfigureAwait(false);
        if (!after.Cli.Found || string.IsNullOrWhiteSpace(after.Cli.Path))
            return Fail("npm 返回成功，但重新扫描没有发现 Codex CLI，因此不报告安装成功。", command.ExitCode);

        var version = await _runner.RunAsync(after.Cli.Path!, "--version", VerifyTimeout, cancellationToken).ConfigureAwait(false);
        if (!CommandSucceeded(version))
            return Fail("已发现 Codex CLI，但 codex --version 验证失败，因此不报告安装成功。", version.ExitCode);

        return Ok("Codex CLI 已安装，并通过重新发现及 codex --version 验证。", command.ExitCode);
    }

    public async Task<InstallOperationResult> UninstallCliAsync(CancellationToken cancellationToken = default)
    {
        if (!_runner.IsAvailable("npm.cmd"))
            return Manual("未发现 npm.cmd，无法使用 npm 官方包管理方式卸载 Codex CLI。不会直接删除用户数据目录。");

        var command = await _runner.RunAsync("npm.cmd", "uninstall -g @openai/codex", InstallTimeout, cancellationToken).ConfigureAwait(false);
        if (!CommandSucceeded(command)) return CommandFailure("Codex CLI 卸载命令失败", command);

        var after = await _discovery.ScanAsync(cancellationToken).ConfigureAwait(false);
        var verified = !after.Cli.Found;
        return verified
            ? Ok("Codex CLI 已通过 npm 卸载，并经重新扫描确认。", command.ExitCode)
            : Fail("npm 返回成功，但重新扫描仍发现 Codex CLI，因此不报告卸载成功。", command.ExitCode);
    }

    private static bool CommandSucceeded(InstallCommandResult result) => !result.TimedOut && result.ExitCode == 0;

    private static InstallOperationResult Ok(string summary, int exitCode) => new(true, true, false, summary, exitCode);
    private static InstallOperationResult Fail(string summary, int? exitCode = null) => new(false, false, false, summary, exitCode);
    private static InstallOperationResult Manual(string summary) => new(false, false, true, summary, null);

    private static InstallOperationResult CommandFailure(string prefix, InstallCommandResult result)
    {
        var details = result.TimedOut
            ? "执行超时。"
            : string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return Fail($"{prefix}：{Limit(details, 1000)}", result.ExitCode);
    }

    private static string Limit(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "无详细输出";
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }
}

public sealed class InstallDiscoveryProvider : IInstallDiscoveryProvider
{
    private readonly CodexDiscoveryService _service;

    public InstallDiscoveryProvider(CodexDiscoveryService? service = null)
    {
        _service = service ?? new CodexDiscoveryService();
    }

    public async Task<CodexDiscoveryResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scan = _service.ScanAsync();
        return await scan.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InstallCommandRunner : IInstallCommandRunner
{
    private const int OutputLimit = 16_384;

    public bool IsAvailable(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        if (Path.IsPathRooted(fileName)) return File.Exists(fileName);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var raw in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(raw.Trim('"'), fileName);
                if (File.Exists(candidate)) return true;
            }
            catch { }
        }
        return false;
    }

    public async Task<InstallCommandResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("命令不能为空。", nameof(fileName));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var extension = Path.GetExtension(fileName);
        var isCommandScript = extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo
        {
            FileName = isCommandScript ? "cmd.exe" : fileName,
            Arguments = isCommandScript ? BuildCmdArguments(fileName, arguments) : arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"无法启动命令：{fileName}");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            try { process.Kill(true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stdout = await SafeRead(stdoutTask).ConfigureAwait(false);
        var stderr = await SafeRead(stderrTask).ConfigureAwait(false);
        var exitCode = timedOut ? -1 : process.ExitCode;
        return new InstallCommandResult(exitCode, LimitOutput(stdout), LimitOutput(stderr), timedOut);
    }

    private static string BuildCmdArguments(string fileName, string arguments)
    {
        var command = "\"" + fileName.Replace("\"", "\"\"") + "\"";
        if (!string.IsNullOrWhiteSpace(arguments)) command += " " + arguments;
        return "/d /s /c \"" + command.Replace("\"", "\\\"") + "\"";
    }

    private static async Task<string> SafeRead(Task<string> task)
    {
        try { return await task.ConfigureAwait(false); }
        catch { return string.Empty; }
    }

    private static string LimitOutput(string value) => value.Length <= OutputLimit ? value : value[..OutputLimit] + "…";
}
