using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexDoctor.Native;

public enum DesktopWindowBindingMethod
{
    None,
    ExactScannedPid,
    ProcessTree,
    ExactExecutablePath
}

public sealed record DesktopWindowCandidate(nint Handle, int ProcessId, bool IsVisible);

public sealed record DesktopProcessSnapshot(int ProcessId, int? ParentProcessId, string? ExecutablePath);

public sealed record DesktopWindowBindingResult(
    bool Found,
    nint Handle,
    int ProcessId,
    DesktopWindowBindingMethod Method,
    string DiagnosticZh)
{
    public static DesktopWindowBindingResult NotFound(string diagnosticZh) =>
        new(false, 0, 0, DesktopWindowBindingMethod.None, diagnosticZh);
}

public interface IDesktopWindowLocator
{
    DesktopWindowBindingResult Locate(CodexDesktopInstallationInfo desktop);
}

public static class DesktopWindowBindingSelector
{
    public static DesktopWindowBindingResult Select(
        CodexDesktopInstallationInfo desktop,
        IReadOnlyCollection<DesktopWindowCandidate> windows,
        IReadOnlyCollection<DesktopProcessSnapshot> processes)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(processes);

        var visible = windows.Where(x => x.IsVisible && x.Handle != 0 && x.ProcessId > 0).ToArray();
        if (visible.Length == 0)
            return DesktopWindowBindingResult.NotFound("未发现可见顶层窗口。");

        var processMap = processes
            .Where(x => x.ProcessId > 0)
            .GroupBy(x => x.ProcessId)
            .ToDictionary(x => x.Key, x => x.First());

        var expectedPath = SafeFullPath(desktop.ExecutablePath);
        var scannedPids = desktop.ProcessIds.Where(x => x > 0).ToHashSet();

        foreach (var window in visible)
        {
            if (!scannedPids.Contains(window.ProcessId)) continue;
            if (processMap.TryGetValue(window.ProcessId, out var process) &&
                !string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                !PathEquals(process.ExecutablePath, expectedPath))
                continue;

            return Found(window, DesktopWindowBindingMethod.ExactScannedPid,
                "已通过扫描确认 PID 的可见顶层窗口绑定 Desktop。");
        }

        var trustedRoots = scannedPids
            .Where(pid => !processMap.TryGetValue(pid, out var root) ||
                          string.IsNullOrWhiteSpace(root.ExecutablePath) ||
                          PathEquals(root.ExecutablePath, expectedPath))
            .ToHashSet();

        foreach (var window in visible)
        {
            if (trustedRoots.Contains(window.ProcessId)) continue;
            if (!IsDescendantOfTrustedRoot(window.ProcessId, trustedRoots, processMap)) continue;

            return Found(window, DesktopWindowBindingMethod.ProcessTree,
                "已通过扫描确认 Desktop 的进程树绑定子进程可见顶层窗口。");
        }

        if (!string.IsNullOrWhiteSpace(expectedPath))
        {
            foreach (var window in visible)
            {
                if (!processMap.TryGetValue(window.ProcessId, out var process)) continue;
                if (!PathEquals(process.ExecutablePath, expectedPath)) continue;

                return Found(window, DesktopWindowBindingMethod.ExactExecutablePath,
                    "已通过与扫描结果完全相同的 EXE 路径绑定多进程 Desktop 顶层窗口。");
            }
        }

        return DesktopWindowBindingResult.NotFound(
            $"枚举到 {visible.Length} 个可见顶层窗口，但没有窗口通过扫描 PID、进程树或完全相同 EXE 路径验证。");
    }

    private static bool IsDescendantOfTrustedRoot(
        int processId,
        IReadOnlySet<int> trustedRoots,
        IReadOnlyDictionary<int, DesktopProcessSnapshot> processMap)
    {
        if (trustedRoots.Count == 0) return false;

        var seen = new HashSet<int>();
        var current = processId;
        for (var depth = 0; depth < 32; depth++)
        {
            if (!seen.Add(current)) return false;
            if (!processMap.TryGetValue(current, out var process) || process.ParentProcessId is null || process.ParentProcessId <= 0)
                return false;

            var parent = process.ParentProcessId.Value;
            if (trustedRoots.Contains(parent)) return true;
            current = parent;
        }
        return false;
    }

    private static DesktopWindowBindingResult Found(
        DesktopWindowCandidate window,
        DesktopWindowBindingMethod method,
        string diagnosticZh) =>
        new(true, window.Handle, window.ProcessId, method, diagnosticZh);

    private static bool PathEquals(string? left, string? right)
    {
        var a = SafeFullPath(left);
        var b = SafeFullPath(right);
        return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
               string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string? SafeFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }
}

public sealed class WindowsDesktopWindowLocator : IDesktopWindowLocator
{
    public DesktopWindowBindingResult Locate(CodexDesktopInstallationInfo desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        try
        {
            var processes = SnapshotProcesses();
            var windows = EnumerateTopLevelWindows();
            var result = DesktopWindowBindingSelector.Select(desktop, windows, processes);
            if (result.Found)
            {
                return result with
                {
                    DiagnosticZh = $"{result.DiagnosticZh} HWND=0x{result.Handle:X}，PID={result.ProcessId}，方式={result.Method}。"
                };
            }

            return result with
            {
                DiagnosticZh = $"{result.DiagnosticZh} 扫描 PID={desktop.ProcessIds.Count}，进程快照={processes.Count}，顶层窗口={windows.Count}。"
            };
        }
        catch (Exception ex)
        {
            return DesktopWindowBindingResult.NotFound($"Desktop 窗口枚举失败：{ex.GetType().Name}：{ex.Message}");
        }
    }

    private static IReadOnlyList<DesktopWindowCandidate> EnumerateTopLevelWindows()
    {
        var result = new List<DesktopWindowCandidate>();
        EnumWindowsProc callback = (handle, _) =>
        {
            try
            {
                _ = GetWindowThreadProcessId(handle, out var pid);
                if (pid > 0)
                    result.Add(new DesktopWindowCandidate(handle, unchecked((int)pid), IsWindowVisible(handle)));
            }
            catch { }
            return true;
        };

        if (!EnumWindows(callback, 0))
            throw new InvalidOperationException($"EnumWindows 失败，Win32={Marshal.GetLastWin32Error()}。");
        GC.KeepAlive(callback);
        return result;
    }

    private static IReadOnlyList<DesktopProcessSnapshot> SnapshotProcesses()
    {
        var parents = SnapshotParentProcessIds();
        var result = new List<DesktopProcessSnapshot>(parents.Count);
        foreach (var pair in parents)
        {
            string? path = null;
            try
            {
                using var process = Process.GetProcessById(pair.Key);
                path = process.MainModule?.FileName;
            }
            catch { }
            result.Add(new DesktopProcessSnapshot(pair.Key, pair.Value, path));
        }
        return result;
    }

    private static Dictionary<int, int?> SnapshotParentProcessIds()
    {
        var result = new Dictionary<int, int?>();
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
            return result;

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref entry)) return result;
            do
            {
                var pid = unchecked((int)entry.ProcessId);
                int? parent = entry.ParentProcessId == 0 ? null : unchecked((int)entry.ParentProcessId);
                if (pid > 0) result[pid] = parent;
                entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }
        return result;
    }

    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }
}
