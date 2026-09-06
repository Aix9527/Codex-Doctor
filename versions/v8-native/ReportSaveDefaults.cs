namespace CodexDoctor.Native;

public sealed record ReportSaveDialogOptions(
    string InitialDirectory,
    string FileName,
    string Filter,
    string DefaultExt);

public static class ReportSaveDefaults
{
    public static ReportSaveDialogOptions Create(DateTime now)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexDoctorV8",
            "reports");

        return new ReportSaveDialogOptions(
            root,
            $"CodexDoctor-V8.1-Report-{now:yyyyMMdd-HHmmss}.json",
            "JSON 报告 (*.json)|*.json|所有文件 (*.*)|*.*",
            "json");
    }
}
