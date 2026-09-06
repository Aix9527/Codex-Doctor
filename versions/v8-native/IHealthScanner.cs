namespace CodexDoctor.Native;

public interface IHealthScanner
{
    Task<CodexHealthScanResult> ScanAsync(
        IProgress<HealthScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
