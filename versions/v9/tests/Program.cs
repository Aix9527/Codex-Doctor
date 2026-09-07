using CodexDoctor.V9;
using System.Net;
using System.Net.Sockets;

var tests = new List<(string, Func<Task>)>();
void Test(string name, Action body) => tests.Add((name, () => { body(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> body) => tests.Add((name, body));
void Check(bool ok, string message = "断言失败") { if (!ok) throw new Exception(message); }
string Temp() { var p = Path.Combine(Path.GetTempPath(), "CodexDoctorV9-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
void WithConfig(Action<ProxyConfiguration> body) { var p = Temp(); try { body(new(p)); } finally { Directory.Delete(p, true); } }

Test("替换空格/export/大小写代理并保留其他内容", () => WithConfig(c => {
    File.WriteAllText(c.EnvFile, "# 用户配置\r\nexport HTTP_PROXY = http://127.0.0.1:1111\r\nhttps_proxy='http://127.0.0.1:1111'\r\nCUSTOM=keep\r\nNO_PROXY=corp.example\r\n");
    var before = File.ReadAllBytes(c.EnvFile); var change = c.Write("http://127.0.0.1:2222");
    var text = File.ReadAllText(c.EnvFile);
    Check(!text.Contains(":1111") && text.Contains("CUSTOM=keep") && text.Contains("NO_PROXY=corp.example") && c.Matches("http://127.0.0.1:2222"));
    Check(File.ReadAllBytes(change.BackupPath!).SequenceEqual(before));
    c.Rollback(change); Check(File.ReadAllBytes(c.EnvFile).SequenceEqual(before));
}));
Test("缺失 .env 创建并可回滚", () => WithConfig(c => { var x = c.Write("http://127.0.0.1:2222"); Check(x.BackupPath is null && c.Matches("http://127.0.0.1:2222")); c.Rollback(x); Check(!File.Exists(c.EnvFile)); }));
Test("重复修复幂等", () => WithConfig(c => { c.Write("http://127.0.0.1:2222"); var before = File.ReadAllBytes(c.EnvFile); var x = c.Write("http://127.0.0.1:2222"); Check(!x.Changed && x.BackupPath is null && File.ReadAllBytes(c.EnvFile).SequenceEqual(before)); }));
Test("连续更新备份不会碰撞", () => WithConfig(c => { c.Write("http://localhost:1"); var a = c.Write("http://localhost:2"); var b = c.Write("http://localhost:3"); Check(a.BackupPath != b.BackupPath && File.Exists(a.BackupPath) && File.Exists(b.BackupPath)); }));
Test("拒绝覆盖修复后用户的新更改", () => WithConfig(c => { var x = c.Write("http://localhost:2"); File.AppendAllText(c.EnvFile, "CUSTOM=new\n"); try { c.Rollback(x); throw new Exception("未拒绝覆盖"); } catch (IOException) { Check(File.ReadAllText(c.EnvFile).Contains("CUSTOM=new")); } }));
foreach (var bad in new[] { "http://evil.example:8888", "http://user:password@localhost:80", "http://localhost:80/path", "http://localhost:80?x=1", "socks5://localhost:1080", "http://localhost:0", "http://localhost:80\nHTTP_PROXY=bad" })
    Test("拒绝非法代理 " + bad.Replace('\n', ' '), () => Check(ProxyConfiguration.LocalHttpProxy(bad) is null));
Test("IPv6 本机代理规范化", () => Check(ProxyConfiguration.LocalHttpProxy("http://[::1]:23456") == "http://[::1]:23456"));
AsyncTest("失效旧端口不阻止发现新端口", async () => {
    var finder = new LocalProxyDiscovery((p, ct) => Task.FromResult(new ProxyProbe(p, p.EndsWith(":23456"), 200, "fixture")));
    var found = await finder.FindAsync(["http://localhost:1111", "http://localhost:23456"], null, default);
    Check(found.Selected == "http://localhost:23456" && found.Probes.Count == 2);
});
AsyncTest("无有效代理不写文件", async () => {
    var root = Temp(); try { var c = new ProxyConfiguration(root); var finder = new LocalProxyDiscovery((p, ct) => Task.FromResult(new ProxyProbe(p, false, null, "失败"))); var r = await new ReconnectRepair(c, finder).RunAsync(["http://localhost:2222"], null, default); Check(!r.ConfigurationVerified && !File.Exists(c.EnvFile)); } finally { Directory.Delete(root, true); }
});
AsyncTest("发现有效代理后确实写入且独立复检", async () => {
    var root = Temp(); try { var count = 0; var c = new ProxyConfiguration(root); var finder = new LocalProxyDiscovery((p, ct) => { count++; return Task.FromResult(new ProxyProbe(p, true, 403, "仅隧道")); }); var r = await new ReconnectRepair(c, finder).RunAsync(["http://localhost:23456"], null, default); Check(r.ConfigurationVerified && count == 2 && c.Matches("http://localhost:23456")); Check(!r.Summary.Contains("已恢复")); } finally { Directory.Delete(root, true); }
});
AsyncTest("复检失败恢复原文件", async () => {
    var root = Temp(); try { var n = 0; var c = new ProxyConfiguration(root); File.WriteAllText(c.EnvFile, "CUSTOM=old\n"); var finder = new LocalProxyDiscovery((p, ct) => Task.FromResult(new ProxyProbe(p, ++n == 1, null, "fixture"))); try { await new ReconnectRepair(c, finder).RunAsync(["http://localhost:2222"], null, default); throw new Exception("未抛失败"); } catch (IOException) { Check(File.ReadAllText(c.EnvFile) == "CUSTOM=old\n"); } } finally { Directory.Delete(root, true); }
});
AsyncTest("写入后取消仍回滚", async () => {
    var root = Temp(); using var cts = new CancellationTokenSource(); try { var n = 0; var c = new ProxyConfiguration(root); var finder = new LocalProxyDiscovery((p, ct) => { if (++n > 1) { cts.Cancel(); ct.ThrowIfCancellationRequested(); } return Task.FromResult(new ProxyProbe(p, true, 200, "fixture")); }); try { await new ReconnectRepair(c, finder).RunAsync(["http://localhost:2222"], null, cts.Token); throw new Exception("未取消"); } catch (OperationCanceledException) { Check(!File.Exists(c.EnvFile)); } } finally { Directory.Delete(root, true); }
});
AsyncTest("任意监听端口发现及普通 HTTP 服务不能冒充代理", async () => {
    var root = Temp(); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    try {
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var candidates = LocalProxyDiscovery.Candidates(new(root)); Check(candidates.Contains($"http://127.0.0.1:{port}"));
        var serve = Task.Run(async () => { using var socket = await listener.AcceptTcpClientAsync(); var stream = socket.GetStream(); var buffer = new byte[4096]; await stream.ReadAsync(buffer); await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")); });
        var result = await LocalProxyDiscovery.ProbeAsync($"http://127.0.0.1:{port}", default); await serve; Check(!result.Reachable);
    } finally { listener.Stop(); Directory.Delete(root, true); }
});

Test("Doctor 和捆绑 CLI 不能识别为 Desktop", () => {
    Check(!DesktopService.IsDesktopPath(@"C:\Tools\CodexDoctor.exe"));
    Check(!DesktopService.IsDesktopPath(@"C:\App\resources\codex.exe"));
    Check(DesktopService.IsDesktopPath(@"C:\Apps\Codex\Codex.exe"));
});
AsyncTest("进程大输出不会死锁", async () => {
    var result = await CommandRunner.RunAsync("cmd.exe", ["/d", "/c", "for /L %i in (1,1,6000) do @echo test"], TimeSpan.FromSeconds(10), default);
    Check(result.ExitCode == 0 && !result.TimedOut && result.Output.Length > 1000);
});
AsyncTest("进程超时被终止", async () => {
    var result = await CommandRunner.RunAsync("ping.exe", ["127.0.0.1", "-n", "20"], TimeSpan.FromMilliseconds(200), default);
    Check(result.TimedOut);
});

Test("PATH 补齐不重复且保留原有项", () => {
    Check(InstallationService.AddUserPath(@"C:\Tools", @"C:\Windows;C:\Tools") == @"C:\Windows;C:\Tools");
    Check(InstallationService.AddUserPath(@"C:\Tools", @"C:\Windows") == @"C:\Windows;C:\Tools");
});
Test("迁移拒绝源目标重叠", () => {
    try { DataMigration.ValidatePaths(@"C:\A\.codex", @"C:\A\.codex\nested"); throw new Exception("未拒绝嵌套"); } catch (IOException) { }
    try { DataMigration.ValidatePaths(@"C:\A\.codex", @"C:\A"); throw new Exception("未拒绝父目录"); } catch (IOException) { }
});
AsyncTest("真实文件迁移和恢复保留最新数据", async () => {
    var root = Temp(); try {
        var source = Path.Combine(root, "source"); Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "data.txt"), "original");
        var service = new DataMigration(source, Path.Combine(root, "state.json"), () => false);
        var target = Path.Combine(root, "target"); await service.MigrateAsync(target, default);
        Check(new DirectoryInfo(source).LinkTarget is not null && File.ReadAllText(Path.Combine(source, "data.txt")) == "original");
        File.WriteAllText(Path.Combine(source, "data.txt"), "new");
        await service.RestoreAsync(default); Check(new DirectoryInfo(source).LinkTarget is null && File.ReadAllText(Path.Combine(source, "data.txt")) == "new");
        Check(File.ReadAllText(Path.Combine(target, "data.txt")) == "new");
    } finally { Directory.Delete(root, true); }
});
AsyncTest("迁移不覆盖非空目标", async () => {
    var root = Temp(); try { var source = Path.Combine(root, "source"); var target = Path.Combine(root, "target"); Directory.CreateDirectory(source); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "keep"), "keep"); try { await new DataMigration(source, Path.Combine(root, "state.json"), () => false).MigrateAsync(target, default); throw new Exception("未拒绝"); } catch (IOException) { Check(File.Exists(Path.Combine(target, "keep"))); } } finally { Directory.Delete(root, true); }
});
AsyncTest("客户端运行时拒绝迁移", async () => {
    var root = Temp(); try { var source = Path.Combine(root, "source"); Directory.CreateDirectory(source); try { await new DataMigration(source, Path.Combine(root, "state.json"), () => true).MigrateAsync(Path.Combine(root, "target"), default); throw new Exception("未拒绝"); } catch (IOException) { Check(Directory.Exists(source)); } } finally { Directory.Delete(root, true); }
});

AsyncTest("真实 Git 代理清理保留无关配置和备份", async () => {
    var root = Temp(); try {
        var file = Path.Combine(root, ".gitconfig"); File.WriteAllText(file, "[http]\n proxy = http://127.0.0.1:1234\n[user]\n name = Fixture\n");
        await ToolProxyService.ClearAsync(true, root, default);
        Check(!File.ReadAllText(file).Contains("proxy =") && File.ReadAllText(file).Contains("Fixture"));
        Check(Directory.GetFiles(root, ".gitconfig.doctor-backup-*").Length == 1);
    } finally { Directory.Delete(root, true); }
});
Test("只读配置修复保留内容", () => WithConfig(c => {
    File.WriteAllText(c.EnvFile, "CUSTOM=keep\n"); File.SetAttributes(c.EnvFile, File.GetAttributes(c.EnvFile) | FileAttributes.ReadOnly);
    try { ConfigurationPermissions.MakeWritable(c.EnvFile); Check(!File.GetAttributes(c.EnvFile).HasFlag(FileAttributes.ReadOnly)); Check(File.ReadAllText(c.EnvFile) == "CUSTOM=keep\n"); }
    finally { File.SetAttributes(c.EnvFile, FileAttributes.Normal); }
}));

Test("拒绝把控制台 codex.exe 当成 Desktop", () => {
    var root = Temp(); try {
        var exe = Path.Combine(root, "Codex.exe"); var bytes = new byte[512]; bytes[0] = 77; bytes[1] = 90; BitConverter.GetBytes(128).CopyTo(bytes, 60); bytes[128] = 80; bytes[129] = 69; BitConverter.GetBytes((ushort)0x20b).CopyTo(bytes, 152); BitConverter.GetBytes((ushort)3).CopyTo(bytes, 220); File.WriteAllBytes(exe, bytes);
        Check(!DesktopService.IsDesktopExecutable(exe)); bytes[220] = 2; File.WriteAllBytes(exe, bytes); Check(DesktopService.IsDesktopExecutable(exe));
    } finally { Directory.Delete(root, true); }
});
AsyncTest("恢复时拒绝被替换的 Junction", async () => {
    var root = Temp(); try {
        var source = Path.Combine(root, "source"); var target = Path.Combine(root, "target"); var wrong = Path.Combine(root, "wrong"); Directory.CreateDirectory(source); Directory.CreateDirectory(wrong); File.WriteAllText(Path.Combine(source, "data"), "original");
        var service = new DataMigration(source, Path.Combine(root, "state.json"), () => false); await service.MigrateAsync(target, default);
        Directory.Delete(source, false); await CommandRunner.RunAsync("cmd.exe", ["/d", "/c", "mklink", "/J", source, wrong], TimeSpan.FromSeconds(5), default);
        try { await service.RestoreAsync(default); throw new Exception("未拒绝错误链接"); } catch (IOException) { Check(Directory.Exists(wrong) && new DirectoryInfo(source).LinkTarget is not null); }
        Directory.Delete(source, false);
    } finally { Directory.Delete(root, true); }
});

if (args.Contains("--live-proxy"))
{
    var root = Temp();
    try {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var config = new ProxyConfiguration(root);
        var discovery = new LocalProxyDiscovery();
        var result = await new ReconnectRepair(config, discovery).RunAsync(LocalProxyDiscovery.Candidates(config), null, timeout.Token);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { ConfigurationVerified = result.ConfigurationVerified, Selected = result.Search.Selected, Probed = result.Search.Probes.Count, FileWrittenAndVerified = result.ConfigurationVerified && config.Matches(result.Search.Selected!), FixtureDirectoryOnly = true }));
        return result.ConfigurationVerified ? 0 : 4;
    } finally { Directory.Delete(root, true); }
}

var failed = 0;
foreach (var (name, run) in tests) { try { await run(); Console.WriteLine("通过：" + name); } catch (Exception ex) { failed++; Console.WriteLine("失败：" + name + " / " + ex.Message); } }
Console.WriteLine($"总计 {tests.Count}；通过 {tests.Count - failed}；失败 {failed}");
return failed == 0 ? 0 : 1;
