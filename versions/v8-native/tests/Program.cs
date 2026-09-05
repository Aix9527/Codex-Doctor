using CodexDoctor.Native;
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

// 导出报告的用户可见字段必须为中文，不暴露英文模型属性名。
var reportSample = new DiagnosisResult(
    "8.0",
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
var reportJson = JsonSerializer.Serialize(reportSample);
foreach (var field in new[] { "版本", "健康状态", "故障分类", "诊断建议", "DNS诊断", "直连TLS诊断", "代理HTTPS诊断", "代理地址", "Codex专用环境文件", "Git代理", "npm代理", "TUN状态", "Codex进程数量" })
    Assert(reportJson.Contains($"\"{field}\""), $"报告缺少中文字段：{field}");
Assert(!reportJson.Contains("\"FailureClass\""), "报告不应暴露英文 FailureClass 字段。");
Assert(!reportJson.Contains("\"Health\""), "报告不应暴露英文 Health 字段。");

// 全中文 GUI 文案合同。
var sourceRoot = Directory.GetParent(AppContext.BaseDirectory)!;
while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "CodexDoctor.Native.csproj"))) sourceRoot = sourceRoot.Parent;
Assert(sourceRoot is not null, "无法定位 V8 源码目录。");
var mainForm = File.ReadAllText(Path.Combine(sourceRoot!.FullName, "MainForm.cs"));
foreach (var phrase in new[] { "一键诊断", "修复建议项", "重启 Codex", "迁移 .codex", "恢复 .codex", "导出报告", "诊断失败", "修复失败" })
    Assert(mainForm.Contains(phrase), $"缺少中文 GUI 文案：{phrase}");

// 原生运行链合同：C# 运行时代码不得启动 PowerShell 或引用 ps1/psm1 作为依赖。
var csFiles = Directory.GetFiles(sourceRoot.FullName, "*.cs", SearchOption.TopDirectoryOnly);
var allCs = string.Join("\n", csFiles.Select(File.ReadAllText));
foreach (var forbidden in new[] { "powershell.exe", "pwsh.exe", ".ps1", ".psm1" })
    Assert(!allCs.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"原生运行时代码包含禁止依赖：{forbidden}");

Console.WriteLine("V8 Native 回归测试全部通过。");
