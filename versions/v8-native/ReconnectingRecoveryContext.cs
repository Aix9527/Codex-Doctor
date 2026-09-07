namespace CodexDoctor.Native;

public static class ReconnectingRecoveryContext
{
    private static readonly object Sync = new();
    private static ReconnectingRecoveryResult? _latest;

    public static ReconnectingRecoveryResult? Latest
    {
        get
        {
            lock (Sync) return _latest;
        }
    }

    public static void Record(ReconnectingRecoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (Sync) _latest = result;
    }

    public static void Clear()
    {
        lock (Sync) _latest = null;
    }
}