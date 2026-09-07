using System.Diagnostics;
using System.Text;

namespace CodexDoctor.V9;

public sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut)
{
    public void EnsureSuccess(string operation) { if (TimedOut || ExitCode != 0) throw new IOException($"{operation}失败：" + (TimedOut ? "执行超时，进程已停止。" : $"退出码 {ExitCode}。请在终端检查该命令或导出报告。")); }
}

public static class CommandRunner
{
    public static string? Find(string name)
    {
        foreach (var raw in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var path = Path.Combine(Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"')), name); if (File.Exists(path)) return path; } catch (ArgumentException) { }
        }
        return null;
    }

    public static async Task<CommandResult> RunAsync(string executable, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetExtension(executable).Equals(".cmd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(executable).Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            static string Quote(string input)
            {
                if (input.IndexOfAny(['"', '%', '!', '&', '|', '<', '>', '^', '\r', '\n']) >= 0) throw new ArgumentException("批处理路径或参数含不支持的命令字符。");
                return "\"" + input + "\"";
            }
            start.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            start.Arguments = "/d /s /c \"" + Quote(executable) + " " + string.Join(" ", args.Select(Quote)) + "\"";
        }
        else foreach (var arg in args) start.ArgumentList.Add(arg);
        ct.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("无法启动子进程。");
        var output = DrainAsync(process.StandardOutput);
        var error = DrainAsync(process.StandardError);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        var timedOut = false;
        try { await process.WaitForExitAsync(cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            try { process.Kill(true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        var stdout = await output.ConfigureAwait(false); var stderr = await error.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return new(process.ExitCode, stdout, stderr, timedOut);
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var buffer = new char[4096]; var result = new StringBuilder(); int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            if (result.Length < 65536) result.Append(buffer, 0, Math.Min(count, 65536 - result.Length));
        return result.ToString().Trim();
    }
}
