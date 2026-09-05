# Codex Doctor V8.0.1 Native 中文增强版

V8.0.1 聚焦“本机 Codex 自动发现、真实路径执行、配置审计和中文体验”。

## 新增能力

- 新增“扫描本机 Codex”：自动发现 Codex Desktop / ChatGPT Desktop 的实际 EXE 路径、版本、运行 PID 与安装来源。
- 自动发现 Codex CLI 的真实入口、PATH 可调用状态与版本，不再只依赖裸 `codex` 命令。
- 扫描 `.codex` 数据目录、Junction/Reparse Point、`.env` 与 `config.toml`。
- 敏感配置自动脱敏：TOKEN、KEY、SECRET、PASSWORD、COOKIE、AUTH、SESSION 等只显示“已配置”，不输出原值。
- 新增“一键设置 Codex 为简体中文”：只有经过明确验证并加入可信适配器白名单的 Desktop UI 本地语言入口才允许自动备份、写入 `zh-CN` 和恢复原语言。
- 普通 `.codex/.env` / `config.toml` 中的 `language`、`locale`、`ui_language` 同名字段不会被冒充为 Desktop 界面语言设置。
- 如果当前客户端没有已验证的本地 UI 语言适配器，则显示“需要用户操作”，引导在客户端“设置 → 通用 → 语言”选择简体中文，不会伪报设置成功。
- 新增扫描窗口：支持打开安装目录、打开 `.codex` 目录、复制扫描摘要、导出扫描报告。

## 实机问题修复

- “重启 Codex”改用扫描到的 Desktop 实际 EXE 和 PID，不再依赖 `chatgpt:` URL 协议。
- “运行 codex doctor”改用扫描到的 Codex CLI 真实路径；只有 Desktop、没有 CLI 时给出纯中文提示，不再直接显示底层 .NET 英文异常。

## 安全原则

- 扫描默认只读。
- 不读取浏览器 Cookie、Windows Credential Manager 或登录 Token 存储。
- 不自动改 PATH、注册表或未知应用内部数据。
- 中文自动写入只作用于**明确批准、可恢复、已验证属于 UI 的适配器**；没有可信适配器时只提供用户操作引导。
- 不修改签名后的 MSIX/AppX 安装资源，不破解客户端二进制文件。
- 网络诊断仍默认只读，Git/npm 清理等动作仍需要人工确认。

## 发布验证

Windows GitHub Actions 将实际执行：

- V8.0.1 原生回归测试
- 本机发现模型和敏感信息脱敏测试
- Desktop/CLI 真实路径执行合同测试
- 未授权 `.codex` 语言字段不得触发 UI 自动写入的安全回归
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
