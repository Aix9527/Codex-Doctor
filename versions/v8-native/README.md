# Codex Doctor V8 原生中文版

这是 Codex Doctor 的 **真正 Windows 原生 EXE** 版本。

## 与 V7.1.2 的区别

V7.1.2 兼容版仍基于 PowerShell；V8 原生版使用 C# + .NET 8 + WinForms 重写核心逻辑，正式发布物为 `CodexDoctor.exe`，运行时不启动 `powershell.exe` / `pwsh.exe`，也不加载 `.ps1` / `.psm1`。

## 当前功能

- DNS 解析诊断
- 直接 TLS 连接诊断
- 本地 HTTP/Mixed 代理自动探测与真实 HTTPS 验证
- Clash Verge / Mihomo 等进程与 TUN 网卡检测
- Git / npm 代理冲突检测
- `.codex/.env` 安全备份与代理写入
- 可选写入 Windows 用户代理环境变量（默认关闭）
- Codex / ChatGPT 重启
- `.codex` 迁移与 Junction 恢复
- 中文 JSON 诊断报告
- `codex doctor` 调用入口
- Git / npm 代理清理（必须人工确认）

## 实机样本回归

2026-09-05 实机报告显示：DNS 正常、直接 TLS 超时、本机 `http://127.0.0.1:7897` 代理 HTTPS 可用、`.codex/.env` 不存在。

V8 必须将该状态判断为：

> **需要配置代理**

而不是误报为“TLS 故障”。

## 发布方式

GitHub Actions 在 Windows runner 上执行：

```powershell
dotnet publish .\versions\v8-native\CodexDoctor.Native.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

正式发布目标为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

用户无需安装 .NET，也无需准备任何 PowerShell 脚本或模块即可启动 V8 原生版。
