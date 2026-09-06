using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV812ReportSaveContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var options = ReportSaveDefaults.Create(new DateTime(2026, 9, 6, 22, 25, 29));
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexDoctorV8",
            "reports");

        Require(string.Equals(options.InitialDirectory, expectedRoot, StringComparison.OrdinalIgnoreCase),
            "V8.1.2 报告另存为窗口默认目录必须是 %LOCALAPPDATA%\\CodexDoctorV8\\reports。");
        Require(options.FileName == "CodexDoctor-V8.1-Report-20260906-222529.json",
            "V8.1.2 报告默认文件名必须保持 CodexDoctor-V8.1-Report-yyyyMMdd-HHmmss.json。 ");
        Require(options.Filter.Contains("JSON", StringComparison.OrdinalIgnoreCase) && options.Filter.Contains("*.json", StringComparison.OrdinalIgnoreCase),
            "报告另存为窗口必须只突出 JSON 报告类型。");

        var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
        while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
        if (sourceRoot is null) throw new Exception("无法定位 V8 源码目录。");

        var main = File.ReadAllText(Path.Combine(sourceRoot.FullName, "MainForm.cs"));
        Require(main.Contains("SaveFileDialog", StringComparison.Ordinal),
            "V8.1.2 导出完整报告必须弹出 Windows 另存为窗口，而不是直接写死默认目录。");
        Require(main.Contains("InitialDirectory", StringComparison.Ordinal),
            "报告另存为窗口必须设置默认目录。");
        Require(main.Contains("FileName", StringComparison.Ordinal),
            "报告另存为窗口必须设置默认文件名。");
        Require(main.Contains("DialogResult.OK", StringComparison.Ordinal),
            "用户取消另存为时必须零写入；只有确认保存后才导出。");

        var exportStart = main.IndexOf("private void ExportReport()", StringComparison.Ordinal);
        var confirmSave = main.IndexOf("if (dialog.ShowDialog(this) != DialogResult.OK) return;", exportStart, StringComparison.Ordinal);
        var createDirectory = main.IndexOf("Directory.CreateDirectory", exportStart, StringComparison.Ordinal);
        Require(exportStart >= 0 && confirmSave > exportStart,
            "必须能够定位报告导出确认边界。");
        Require(createDirectory < 0 || createDirectory > confirmSave,
            "用户确认保存之前不得创建目录或执行其它文件系统写入；取消另存为必须真正零写入。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
