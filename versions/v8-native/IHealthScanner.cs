namespace CodexDoctor.Native;

public interface IHealthScanner
{
    Task<CodexHealthScanResult> ScanAsync(
        IProgress<HealthScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class CodexHealthScannerAdapter : IHealthScanner
{
    private readonly CodexHealthScanner _inner;

    public CodexHealthScannerAdapter(CodexHealthScanner inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<CodexHealthScanResult> ScanAsync(
        IProgress<HealthScanProgress>? progress = null,
        CancellationToken cancellationToken = default) => _inner.ScanAsync(progress, cancellationToken);

    public static CodexHealthScannerAdapter CreateDefault(string? userProfile = null, string? localAppData = null, string? appData = null) =>
        new(CodexHealthScanner.CreateDefault(userProfile, localAppData, appData));
}
