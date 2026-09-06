namespace CodexDoctor.Native;

public sealed class CodexHealthScannerAdapter : IHealthScanner
{
    private readonly CodexHealthScanner _inner;

    public CodexHealthScannerAdapter(CodexHealthScanner inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<CodexHealthScanResult> ScanAsync(
        IProgress<HealthScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => _inner.ScanAsync(progress, cancellationToken);
}
