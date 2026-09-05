# Codex Doctor

Windows Codex / ChatGPT Desktop 连接诊断、代理修复与 `.codex` 数据迁移工具。

## 推荐版本

当前推荐 **V8 Native 原生中文版**。

V8 将 V7.1.2 的核心功能使用 **C# + .NET 8 + WinForms** 重写。正式发布物是 `CodexDoctor.exe`：

- 真正 `win-x64` 自包含单文件 EXE；
- 用户无需安装 .NET；
- 运行时不启动 `powershell.exe` / `pwsh.exe`；
- 不依赖 `.ps1` / `.psm1`；
- GUI、弹窗、日志、诊断建议均使用中文；
- 保留“先诊断、后确认修复”的安全原则。

V7.1.2 PowerShell 版继续保留为兼容/历史版本，并已完成用户可见界面、弹窗、日志、错误信息、诊断建议和导出报告的中文化。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类
- `versions/v7` — V7.1.2 PowerShell 兼容版
- `versions/v8-native` — C#/.NET 8 原生 WinForms 单文件 EXE

## V8 Native 功能

- DNS 解析诊断
- 直接 TLS 连接与系统证书验证
- 本地代理自动检测
- 通过代理真实请求 `https://chatgpt.com` 进行 HTTPS 验证
- Clash Verge / Mihomo / sing-box 等进程与 TUN 网卡检测
- Git/npm 代理冲突检测
- `.codex/.env` 备份与安全写入
- Windows 用户代理环境变量可选写入，默认关闭
- Codex / ChatGPT Desktop 重启
- `.codex` 迁移与恢复
- 中文 JSON 报告
- `codex doctor` 调用入口
- Git/npm 代理清理（明确确认后执行）

### 实机诊断规则

对于以下真实场景：

```text
DNS = 正常
直接 TLS = 超时
本机代理 = http://127.0.0.1:7897，可通过 HTTPS 验证
.codex/.env = 不存在
```

V8 会判断为：

```text
需要配置代理
```

并建议写入 Codex 专用 `.env`，不会再把该场景误报为单纯“TLS 故障”。

## V8 使用

下载正式 Release 后直接运行：

```text
CodexDoctor.exe
```

推荐流程：

1. 点击“一键诊断”。
2. 查看中文故障分类与诊断建议。
3. 仅在存在已验证修复方案时点击“修复建议项”。
4. 修复 `.codex/.env` 后点击“重启 Codex”。
5. 再次诊断确认结果。

## V8 构建

GitHub Actions 在 Windows runner 上执行真正的单文件发布：

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

CI 会验证：

- 回归测试通过；
- 输出具有 PE `MZ` 文件头；
- 发布目录不包含 `.ps1` / `.psm1`；
- 发布目录不依赖外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码不启动 PowerShell；
- 生成 SHA256。

## V7.1.2 兼容版

如果需要旧版 PowerShell 实现，可继续使用 `versions/v7`。V7.1.2 已保留 UTF-8 BOM、Windows PowerShell 5.1 兼容修复和 `.Count` 标量回归修复。

## 安全原则

工具不会移动 WindowsApps/MSIX 应用包本体；迁移只针对 `%USERPROFILE%\.codex`。Git/npm 全局代理清理、Windows 用户环境变量写入、`.codex` 迁移和恢复均要求显式操作。

## License

MIT
