using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexDoctor.Native;

public sealed class HealthReportExporter
{
    private readonly string _userProfile;
    private readonly Func<bool> _isAdministrator;

    private static readonly Regex BearerPattern = new(
        @"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OpenAiKeyAssignmentPattern = new(
        @"(?i)(OPENAI_API_KEY\s*[:=]\s*)[^\s\"",;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OpenAiStyleKeyPattern = new(
        @"(?i)\bsk-[A-Za-z0-9_-]{6,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GenericSecretAssignmentPattern = new(
        @"(?i)\b(TOKEN|SECRET|PASSWORD|COOKIE|AUTHORIZATION|SESSION)\s*[:=]\s*[^\s\"",;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public HealthReportExporter(string? userProfile = null, Func<bool>? isAdministrator = null)
    {
        _userProfile = string.IsNullOrWhiteSpace(userProfile)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Path.GetFullPath(userProfile);
        _isAdministrator = isAdministrator ?? AdminGuard.IsAdministrator;
    }

    public string BuildJson(
        CodexHealthScanResult currentScan,
        RepairAndRescanResult? repairAndRescan = null,
        RepairPlan? repairPlan = null)
    {
        ArgumentNullException.ThrowIfNull(currentScan);

        var payload = new
        {
            版本 = "8.1.2",
            软件作者 = "Aix",
            生成时间 = DateTimeOffset.Now,
            管理员权限 = _isAdministrator(),
            当前扫描 = currentScan,
            修复计划 = repairPlan,
            修复执行 = repairAndRescan?.Repair,
            修复前扫描 = repairAndRescan?.BeforeScan,
            修复后扫描 = repairAndRescan?.AfterScan
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(payload, options);
        return SanitizeSerializedJson(json, options);
    }

    public string Export(
        string filePath,
        CodexHealthScanResult currentScan,
        RepairAndRescanResult? repairAndRescan = null,
        RepairPlan? repairPlan = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("报告路径不能为空。", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, BuildJson(currentScan, repairAndRescan, repairPlan), new System.Text.UTF8Encoding(false));
        return fullPath;
    }

    private string SanitizeSerializedJson(string json, JsonSerializerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(_userProfile))
        {
            var escapedProfile = JsonSerializer.Serialize(_userProfile, options).Trim('"');
            if (!string.IsNullOrWhiteSpace(escapedProfile))
                json = json.Replace(escapedProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

            var normalizedProfile = _userProfile.Replace('\\', '/');
            var escapedNormalized = JsonSerializer.Serialize(normalizedProfile, options).Trim('"');
            if (!string.IsNullOrWhiteSpace(escapedNormalized))
                json = json.Replace(escapedNormalized, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        json = OpenAiKeyAssignmentPattern.Replace(json, "$1[已脱敏]");
        json = BearerPattern.Replace(json, "Bearer [已脱敏]");
        json = OpenAiStyleKeyPattern.Replace(json, "[已脱敏]");
        json = GenericSecretAssignmentPattern.Replace(json, "$1=[已脱敏]");
        return json;
    }
}
