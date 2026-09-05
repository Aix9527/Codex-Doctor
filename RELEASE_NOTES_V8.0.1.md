# Codex Doctor V8.0.1 Native 中文增强版

V8.0.1 聚焦“本机 Codex 自动发现、真实路径执行、配置审计和中文体验”。

## 新增能力

- 新增“扫描本机 Codex”：自动发现 Codex Desktop / ChatGPT Desktop 的实际 EXE 路径、版本、运行 PID 与安装来源。
- 自动发现 Codex CLI 的真实入口、PATH 可调用状态与版本，不再只依赖裸 `codex` 命令。
- 扫描 `.codex` 数据目录、Junction/Reparse Point、`.env` 与 `config.toml`。
- 敏感配置自动脱敏：TOKEN、KEY、SECRET、PASSWORD、COOKIE、AUTH、SESSION 等只显示“已配置”，不输出原值。
- 新增“一键设置 Codex 为简体中文”，对已确认的可逆文本语言配置自动备份、写入 `zh-CN`，并支持“恢复原语言”。
- 如果当前客户端没有稳定可写的语言配置，则显示“需要用户操作”，不会修改未知数据库、MSIX/AppX 资源或二进制文件。
- 新增扫描窗口：支持打开安装目录、打开 `.codex` 目录、复制扫描摘要、导出扫描报告。

## 实机问题修复

- “重启 Codex”改用扫描到的 Desktop 实际 EXE 和 PID，不再依赖 `chatgpt:` URL 协议。
- “运行 codex doctor”改用扫描到的 Codex CLI 真实路径；只有 Desktop、没有 CLI 时给出纯中文提示，不再直接显示底层 .NET 英文异常。

## 安全原则

- 扫描默认只读。
- 不读取浏览器 Cookie、Windows Credential Manager 或登录 Token 存储。
- 不自动改 PATH、注册表或未知应用内部数据。
- 中文设置只作用于白名单、可恢复的文本配置；修改前保存恢复信息。
- 网络诊断仍默认只读，Git/npm 清理等动作仍需要人工确认。

## 发布验证

Windows GitHub Actions 将实际执行：

- V8.0.1 原生回归测试
- 本机发现模型和敏感信息脱敏测试
- Desktop/CLI 真实路径执行合同测试
- 中文设置 RED→GREEN 与恢复测试
- GUI 中文功能合同测试
- `dotnet publish` win-x64 self-contained single-file
- `IncludeNativeLibrariesForSelfExtract=true`
- PE `MZ` 文件头验证
- 发布目录无 `.ps1` / `.psm1` / 外置运行 DLL
- 运行时代码无 PowerShell 启动依赖
- SHA256 生成

## 正式资产

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```
