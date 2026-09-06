using System.Runtime.CompilerServices;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81AdminContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = FindSourceRoot();
        var manifest = Path.Combine(root, "app.manifest");
        Require(File.Exists(manifest), "V8.1 必须包含 app.manifest。");
        var xml = File.ReadAllText(manifest);
        Require(xml.Contains("requireAdministrator"), "manifest 必须强制 requireAdministrator。");

        var csproj = File.ReadAllText(Path.Combine(root, "CodexDoctor.Native.csproj"));
        foreach (var token in new[] { "<Version>8.1.0</Version>", "<Company>Aix</Company>", "<Authors>Aix</Authors>" })
            Require(csproj.Contains(token), $"缺少产品元数据：{token}");

        var program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        Require(program.Contains("EnsureAdministratorOrExit"), "Program 启动主界面前必须做管理员运行时校验。");
    }

    private static string FindSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodexDoctor.Native.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("无法定位 V8 源码目录。");
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
}
