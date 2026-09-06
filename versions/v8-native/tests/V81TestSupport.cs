namespace CodexDoctor.Native.Tests;

internal static class V81TestSupport
{
    public static DirectoryInfo SourceRoot()
    {
        var current = Directory.GetParent(AppContext.BaseDirectory)!;
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CodexDoctor.Native.csproj")))
            current = current.Parent;
        return current ?? throw new Exception("无法定位 V8 源码目录。");
    }

    public static DirectoryInfo RepoRoot() => SourceRoot().Parent!.Parent!;

    public static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
}
