# Codex Doctor V8.0.1 原生中文版

这是 Codex Doctor 的 **真正 Windows 原生 EXE** 版本，使用 C# + .NET 8 + WinForms，正式发布物为单文件 `CodexDoctor.exe`，运行时不启动 `powershell.exe` / `pwsh.exe`，也不加载 `.ps1` / `.psm1`。

## V8.0.1 新增：扫描本机 Codex

V8.0.1 增加“扫描本机 Codex”，自动检查：

- Codex Desktop / ChatGPT Desktop 的运行进程、实际可执行文件路径、版本与安装来源。
- Codex CLI 的真实路径、PATH 可调用状态与版本。
- `%USERPROFILE%\.codex` 是否存在、是否为 Junction/Reparse Point、链接目标、文件数量与大小。
- `.codex/.env`、`config.toml` 等已知文本配置。
- 代理、语言等允许显示的非敏感配置。

扫描默认只读，不自动修改注册表、PATH、用户数据或系统设置。敏感键名（例如 TOKEN、KEY、SECRET、PASSWORD、COOKIE、AUTH、SESSION）只显示“已配置/未配置”，不会在 GUI 或导出报告中输出原值。

## V8.0.1 新增：一键设置 Codex 为简体中文

程序明确区分：

- 当前界面语言
- 回答语言偏好
- CLI 输出偏好

当扫描到稳定、可逆的文本语言配置（当前白名单为 `.toml` / `.env` 中的 `language`、`locale`、`ui_language`）时，可使用“一键设置 Codex 为简体中文”，写入前会保存恢复信息，并可点击“恢复原语言”。

如果当前 Codex Desktop 版本没有稳定可写的语言配置，程序会显示“需要用户操作”，并引导用户打开检测到的客户端进入设置。此时程序不会伪报成功，也不修改 MSIX/AppX 安装资源、内部数据库或未知二进制配置。

## 真实路径重启与 codex doctor

“重启 Codex”现在优先使用本机扫描得到的 Desktop 实际 EXE 路径和 PID，不再依赖 `chatgpt:` URL 协议。

“运行 codex doctor”会使用扫描到的 Codex CLI 真实入口；如果只安装了 Codex Desktop 而没有 Codex CLI，会显示纯中文说明，此项可跳过，不影响网络诊断或代理修复。

## 现有功能

- DNS 解析诊断
- 直接 TLS 连接诊断
- 本地 HTTP/Mixed 代理自动探测与真实 HTTPS 验证
- Clash Verge / Mihomo 等进程与 TUN 网卡检测
- Git / npm 代理冲突检测
- `.codex/.env` 安全备份与代理写入
- 可选写入 Windows 用户代理环境变量（默认关闭）
- `.codex` 迁移与 Junction 恢复
- 中文 JSON 诊断报告与本机扫描报告
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
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

正式发布目标继续保持：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

用户无需安装 .NET，也无需准备任何 PowerShell 脚本或模块即可运行原生版。
