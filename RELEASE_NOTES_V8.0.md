# Codex Doctor V8.0 Native 中文原生版

V8.0 是 Codex Doctor 的原生 Windows 重写版本。

## 核心变化

- 使用 C# + .NET 8 + WinForms 重写核心运行逻辑。
- 正式发布物为真正的 `win-x64` 自包含单文件 `CodexDoctor.exe`。
- 用户无需安装 .NET。
- 程序运行时不启动 `powershell.exe` / `pwsh.exe`，也不加载 `.ps1` / `.psm1`。
- GUI、弹窗、日志、诊断建议和导出报告字段均为中文。

## 诊断能力

- DNS：`chatgpt.com` / `api.openai.com`
- 直接 TLS：`chatgpt.com:443`
- 本地代理自动探测及真实 HTTPS 验证
- Clash Verge / Mihomo / sing-box 进程与 TUN 网卡检测
- Git / npm 全局代理冲突检查
- `.codex/.env` 状态检测

## 实机诊断模型修正

来自 2026-09-05 的真实诊断样本：

- DNS 正常
- OpenAI 直连 TLS 超时
- `http://127.0.0.1:7897` 代理 HTTPS 可用
- `.codex/.env` 不存在

V8 将此状态判断为：

> **需要配置代理**

并建议将已经通过 HTTPS 验证的代理写入 Codex 专用 `.env`，不再误报为单纯 TLS 故障。

## 安全修复

- 修改 `.codex/.env` 前自动备份。
- 保留 `.env` 中与代理无关的配置。
- Windows 用户环境变量写入默认关闭，必须显式勾选。
- Git / npm 代理清理必须明确确认。
- `.codex` 迁移与恢复具备 Junction 安全检查。
- 网络诊断本身默认只读。

## 发布验证

Windows GitHub Actions 会实际执行：

- V8 原生回归测试
- `.env` 备份/写入测试
- 2026-09-05 实机样本分类测试
- 中文 GUI/报告合同测试
- `dotnet publish` self-contained single-file
- PE `MZ` 文件头验证
- 发布目录无 `.ps1` / `.psm1` / 外置运行时 DLL
- C# 运行时代码无 PowerShell 启动依赖
- SHA256 生成

## 下载

正式资产：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

直接双击 `CodexDoctor.exe` 即可运行。
