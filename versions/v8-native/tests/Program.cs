using CodexDoctor.Native;
using System.Text.Encodings.Web;
using System.Text.Json;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Console.OutputEncoding = System.Text.Encoding.UTF8;

// 2026-09-05 实机样本：DNS 正常、直连 TLS 超时、127.0.0.1:7897 代理可用、.env 缺失。
var sample = DiagnosisService.Classify(dnsOk: true, directTlsOk: false, proxyOk: true, envExists: false);
Assert(sample == FailureClass.ProxyRequired, "实机样本必须判定为 ProxyRequired，而不是 TLS 故障。");
var sampleDesc = DiagnosisService.Describe(sample);
Assert(sampleDesc.NameZh == "需要配置代理", "实机样本中文故障名称不正确。");
Assert(sampleDesc.RecommendationZh.Contains("Codex 专用 .env"), "实机样本应建议写入 Codex 专用 .env。");

var proxied = DiagnosisService.Classify(dnsOk: true, directTlsOk: false, proxyOk: true, envExists: true);
Assert(proxied == FailureClass.DirectNetworkBlocked, "已配置可用代理时应标识为直连受限，而不是错误。");

var dnsFail = DiagnosisService.Classify(dnsOk: false, directTlsOk: false, proxyOk: false, envExists: false);
Assert(dnsFail == FailureClass.DnsFailure, "DNS 失败分类错误。");

// .env 修复必须保留无关行、替换旧代理并生成备份。
var temp = Path.Combine(Path.GetTempPath(), "CodexDoctorV8Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(temp, ".codex"));
var envFile = Path.Combine(temp, ".codex", ".env");
File.WriteAllText(envFile, "FOO=bar\nHTTP_PROXY=http://127.0.0.1:1111\nHTTPS_PROXY=http://127.0.0.1:1111\n");
var repair = new RepairService(temp);
var backup = repair.WriteCodexProxyEnv("http://127.0.0.1:7897", false);
var text = File.ReadAllText(envFile);
Assert(File.Exists(backup), "修复前必须生成 .env 备份。");
Assert(text.Contains("FOO=bar"), "必须保留无关 .env 配置。");
Assert(text.Contains("HTTP_PROXY=http://127.0.0.1:7897"), "必须写入新 HTTP_PROXY。");
Assert(!text.Contains("127.0.0.1:1111"), "旧代理必须被移除。");
Directory.Delete(temp, true);

// 导出报告必须直接显示中文字段，而不是 \\uXXXX 转义，也不能暴露英文模型字段。
var reportSample = new DiagnosisResult(
    "8.0.1",
    HealthState.Warning,
    FailureClass.ProxyRequired,
    "需要配置代理",
    "建议写入 Codex 专用 .env。",
    new ProbeResult(true),
    new ProbeResult(false, "TCP/TLS 连接超时。"),
    new ProbeResult(true, StatusCode: 403),
    "http://127.0.0.1:7897",
    new ProxyEnvironmentState(false, "", ""),
    new ConflictState("", "", false),
    new ConflictState("", "", false),
    new TunState(true, false, ["clash-verge"], []),
    2);
var reportOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var reportJson = JsonSerializer.Serialize(reportSample, reportOptions);
foreach (var field in new[] { "版本", "健康状态", "故障分类", "诊断建议", "DNS诊断", "直连TLS诊断", "代理HTTPS诊断", "代理地址", "Codex专用环境文件", "Git代理", "npm代理", "TUN状态", "Codex进程数量" })
    Assert(reportJson.Contains($"\"{field}\""), $"报告缺少中文字段：{field}");
Assert(!reportJson.Contains("\\u7248\\u672c", StringComparison.OrdinalIgnoreCase), "中文报告不应把“版本”转义成 Unicode 序列。");
Assert(!reportJson.Contains("\"FailureClass\""), "报告不应暴露英文 FailureClass 字段。");
Assert(!reportJson.Contains("\"Health\""), "报告不应暴露英文 Health 字段。");

// Task 1：本机发现模型必须使用中文 JSON 字段，并保证敏感值永不进入序列化输出。
var sensitiveEntry = new CodexConfigEntryInfo("OPENAI_API_KEY", null, true, true);
var discoverySample = new CodexDiscoveryResult(
    [new CodexDesktopInstallationInfo("Codex Desktop", "进程发现", @"C:\\Apps\\Codex.exe", "1.0.0", true, [1234])],
    new CodexCliInfo(true, @"C:\\Users\\X\\AppData\\Roaming\\npm\\codex.cmd", true, "codex 1.0.0", true),
    new CodexDataDirectoryInfo(@"C:\\Users\\X\\.codex", true, false, null, 3, 1024),
    [new CodexConfigFileInfo(@"C:\\Users\\X\\.codex\\.env", true, [sensitiveEntry])],
    new CodexLanguageState("未知", "简体中文", "简体中文", false, true, "需要用户操作"));
var discoveryJson = JsonSerializer.Serialize(discoverySample, reportOptions);
foreach (var field in new[] { "桌面客户端", "CodexCLI", "数据目录", "配置文件", "语言状态" })
    Assert(discoveryJson.Contains($"\"{field}\""), $"发现报告缺少中文字段：{field}");
Assert(discoveryJson.Contains("\"已配置\":true"), "敏感配置必须只标记为已配置。");
Assert(!discoveryJson.Contains("sk-test-secret", StringComparison.OrdinalIgnoreCase), "敏感配置值不得进入发现报告。");
Assert(!discoveryJson.Contains("OPENAI_API_KEY=", StringComparison.OrdinalIgnoreCase), "发现报告不得输出敏感键值对原文。");

// Task 2：Desktop、CLI 与配置文件必须可通过可测试 helper 自动发现并安全脱敏。
var discoveryTemp = Path.Combine(Path.GetTempPath(), "CodexDoctorDiscoveryTests-" + Guid.NewGuid().ToString("N"));
var desktopDir = Path.Combine(discoveryTemp, "Programs", "Codex");
var npmDir = Path.Combine(discoveryTemp, "AppData", "Roaming", "npm");
var codexDir = Path.Combine(discoveryTemp, ".codex");
Directory.CreateDirectory(desktopDir);
Directory.CreateDirectory(npmDir);
Directory.CreateDirectory(codexDir);
var fakeDesktop = Path.Combine(desktopDir, "Codex.exe");
var fakeCli = Path.Combine(npmDir, "codex.cmd");
var fakeEnv = Path.Combine(codexDir, ".env");
File.WriteAllBytes(fakeDesktop, [0x4D, 0x5A]);
File.WriteAllText(fakeCli, "@echo off\r\necho codex-test\r\n");
File.WriteAllText(fakeEnv, "HTTP_PROXY=http://127.0.0.1:7897\nOPENAI_API_KEY=sk-super-secret\n");
var desktopCandidates = CodexDiscoveryService.DiscoverDesktopCandidates([desktopDir]);
Assert(desktopCandidates.Any(x => string.Equals(x.ExecutablePath, fakeDesktop, StringComparison.OrdinalIgnoreCase)), "应从候选目录找到 Codex Desktop。");
var cliFromPath = CodexDiscoveryService.DiscoverCliFromPath(npmDir);
Assert(cliFromPath.Found && string.Equals(cliFromPath.Path, fakeCli, StringComparison.OrdinalIgnoreCase), "应从模拟 PATH 找到 Codex CLI。");
Assert(cliFromPath.PathCallable, "PATH 内 CLI 应标记为可调用。");
var cliOutsidePath = CodexDiscoveryService.DiscoverCliFromPath("", [fakeCli]);
Assert(cliOutsidePath.Found && !cliOutsidePath.PathCallable, "非 PATH 候选 CLI 应被发现但标记为不可直接调用。");
var scannedEnv = CodexDiscoveryService.ScanConfigFile(fakeEnv);
var proxyEntry = scannedEnv.Entries.FirstOrDefault(x => x.Name.Equals("HTTP_PROXY", StringComparison.OrdinalIgnoreCase));
var apiKeyEntry = scannedEnv.Entries.FirstOrDefault(x => x.Name.Equals("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase));
Assert(proxyEntry is not null && proxyEntry.Value == "http://127.0.0.1:7897", "代理配置应允许在扫描结果中显示。");
Assert(apiKeyEntry is not null && apiKeyEntry.Sensitive && apiKeyEntry.Configured && apiKeyEntry.Value is null, "API key 必须只显示已配置状态并隐藏值。");
var scannedJson = JsonSerializer.Serialize(scannedEnv, reportOptions);
Assert(!scannedJson.Contains("sk-super-secret", StringComparison.OrdinalIgnoreCase), "扫描报告不得包含 API key 原值。");
Directory.Delete(discoveryTemp, true);

// 源码根目录。
var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
Assert(sourceRoot is not null, "无法定位 V8 源码目录。");

// Task 3：doctor / restart 必须使用扫描到的显式路径，不能裸调用 codex 或 chatgpt:。
var repairSource = File.ReadAllText(Path.Combine(sourceRoot!.FullName, "RepairService.cs"));
Assert(repairSource.Contains("RunCodexDoctor(string cliPath)"), "RepairService 必须支持显式 CLI 路径运行 doctor。");
Assert(repairSource.Contains("RestartCodexDesktop(string executablePath"), "RepairService 必须支持显式 Desktop 路径重启。");
Assert(!repairSource.Contains("Run(\"codex\", \"doctor\"", StringComparison.OrdinalIgnoreCase), "不得再裸调用 codex doctor。");
Assert(!repairSource.Contains("ProcessStartInfo(\"chatgpt:\"", StringComparison.OrdinalIgnoreCase), "不得再依赖 chatgpt: URL 协议重启。");

// Task 4 安全 RED：普通 .codex 配置中的 language/locale 不是已知 Desktop UI 语言入口，不能自动改写并宣称成功。
var languageTemp = Path.Combine(Path.GetTempPath(), "CodexDoctorLanguageSafety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(languageTemp, ".codex"));
var languageConfig = Path.Combine(languageTemp, ".codex", "config.toml");
File.WriteAllText(languageConfig, "language=en-US\n");
var untrustedLanguageFile = CodexDiscoveryService.ScanConfigFile(languageConfig);
var languageDiscovery = new CodexDiscoveryResult(
    [],
    new CodexCliInfo(false, null, false, null, false),
    new CodexDataDirectoryInfo(Path.Combine(languageTemp, ".codex"), true, false, null, 1, new FileInfo(languageConfig).Length),
    [untrustedLanguageFile],
    new CodexLanguageState("未知", "未知", "未知", false, true, "尚未检测"));
var languageBackup = Path.Combine(languageTemp, "language-backup.json");
var languageService = new CodexLanguageService(languageBackup);
var unsafeApply = languageService.ApplySimplifiedChinese(languageDiscovery);
Assert(!unsafeApply.Applied && unsafeApply.NeedsUserAction, "未经明确白名单的 .codex language 配置不得被当作 Desktop UI 语言入口。");
Assert(File.ReadAllText(languageConfig).Contains("language=en-US"), "不可信语言配置必须保持原样。");
Assert(!File.Exists(languageBackup), "未执行语言写入时不应生成伪备份。");
Directory.Delete(languageTemp, true);

// Task 5：GUI 必须包含完整本机扫描/中文设置能力，并复用发现结果。
var mainForm = File.ReadAllText(Path.Combine(sourceRoot.FullName, "MainForm.cs"));
var discoveryFormPath = Path.Combine(sourceRoot.FullName, "CodexDiscoveryForm.cs");
Assert(File.Exists(discoveryFormPath), "必须存在 CodexDiscoveryForm.cs。");
var discoveryForm = File.ReadAllText(discoveryFormPath);
foreach (var phrase in new[] { "扫描本机 Codex", "一键设置 Codex 为简体中文", "恢复原语言", "当前界面语言", "回答语言偏好", "CLI 输出偏好", "打开安装目录", "打开 .codex 目录", "复制扫描摘要", "导出扫描报告" })
    Assert((mainForm + "\n" + discoveryForm).Contains(phrase), $"缺少 V8.0.1 GUI 文案：{phrase}");
Assert(mainForm.Contains("RunCodexDoctor(_lastDiscovery.Cli.Path"), "主界面 doctor 必须复用扫描到的 CLI 路径。");
Assert(mainForm.Contains("RestartCodexDesktop(desktop.ExecutablePath, desktop.ProcessIds)"), "主界面重启必须复用扫描到的 Desktop 路径。");

// 全中文 GUI 基础文案合同。
foreach (var phrase in new[] { "一键诊断", "修复建议项", "重启 Codex", "迁移 .codex", "恢复 .codex", "导出报告", "诊断失败", "修复失败", "有冲突", "无冲突" })
    Assert(mainForm.Contains(phrase), $"缺少中文 GUI 文案：{phrase}");
Assert(mainForm.Contains("UnsafeRelaxedJsonEscaping"), "导出报告必须配置直接可读的中文 JSON 编码。");

// Task 6：发布文档和工作流合同。
var repoRoot = sourceRoot.Parent!.Parent!;
var readme = File.ReadAllText(Path.Combine(sourceRoot.FullName, "README.md"));
var releaseNotesPath = Path.Combine(repoRoot.FullName, "RELEASE_NOTES_V8.0.1.md");
var releaseWorkflowPath = Path.Combine(repoRoot.FullName, ".github", "workflows", "release-v8.0.1.yml");
Assert(File.Exists(releaseNotesPath), "必须存在 V8.0.1 发布说明。");
Assert(File.Exists(releaseWorkflowPath), "必须存在 V8.0.1 发布工作流。");
var releaseNotes = File.ReadAllText(releaseNotesPath);
foreach (var phrase in new[] { "只读", "敏感", "Desktop", "CLI", "MSIX" })
    Assert((readme + "\n" + releaseNotes).Contains(phrase, StringComparison.OrdinalIgnoreCase), $"V8.0.1 文档缺少安全/客户端边界说明：{phrase}");
var releaseWorkflow = File.ReadAllText(releaseWorkflowPath);
Assert(releaseWorkflow.Contains("v8.0.1"), "发布工作流必须绑定 v8.0.1。");
Assert(releaseWorkflow.Contains("PublishSingleFile=true"), "发布工作流必须保持单文件发布。");
Assert(releaseWorkflow.Contains("IncludeNativeLibrariesForSelfExtract=true"), "发布工作流必须打包原生库。");

// 原生运行链合同：C# 运行时代码不得启动 PowerShell 或引用 ps1/psm1 作为依赖。
var csFiles = Directory.GetFiles(sourceRoot.FullName, "*.cs", SearchOption.TopDirectoryOnly);
var allCs = string.Join("\n", csFiles.Select(File.ReadAllText));
foreach (var forbidden in new[] { "powershell.exe", "pwsh.exe", ".ps1", ".psm1" })
    Assert(!allCs.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"原生运行时代码包含禁止依赖：{forbidden}");

Console.WriteLine("V8 Native 回归测试全部通过。");
