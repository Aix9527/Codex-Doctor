# Codex Doctor

Windows Codex / ChatGPT Desktop **一键扫描、连接诊断、安全修复、智能迁移与恢复**工具。

## 当前推荐版本：V8.1.1 原生维修中心

V8.1.1 使用 **C# + .NET 8 + WinForms**，正式发布物为真正的 `win-x64` 自包含单文件 `CodexDoctor.exe`。

在 V8.1.0 维修中心基础上，V8.1.1 新增：

- **中文 / English 双向一键切换**：优先可信 Desktop UI 语言适配器，否则尝试绑定扫描确认的 Desktop EXE/PID，通过 Windows UI Automation 操作应用自身语言设置；目标固定为 `zh-CN` / `en-US`，无法重新验证时不伪报成功；
- **Codex Desktop / CLI 安装 / 卸载**：Desktop 自动路径只使用 Microsoft Store/winget 官方包 ID `9NT1R1C2HH7J`；CLI 自动路径只使用 npm 官方包 `@openai/codex`；每次安装/卸载后都必须重新扫描确认结果；
- **安全卸载**：默认保留 `%USERPROFILE%\.codex`、用户项目、Codex Doctor 备份和诊断报告；GUI 在卸载前进行二次确认。

V8.1.x 核心特性继续包括：

- 通过 Windows manifest 请求管理员权限，未通过 **UAC** 不进入主界面；
- 先执行 **一键扫描 Codex**，统一检查 Desktop / CLI、`.codex`、代理、DNS/TLS、Git/npm、TUN、语言、迁移、安装状态和启动路径；
- 同时保留多个问题，并按 **严重 → 紧急 → 警告 → 提示 → 正常**进行问题分级；
- **一键修复**只执行白名单内、可验证的安全动作；需要备份的项目先备份，验证失败自动回滚；
- 修复结束后执行**自动复检**，保留修复前后 ScanId 和动作结果；
- 自动发现 Codex / ChatGPT Desktop 实际 EXE 与 PID，真实路径启动/重启，不依赖 `chatgpt:` URL 协议；
- `.codex` 支持智能迁移/恢复和中断事务安全恢复；
- **导出完整报告**会记录扫描、RepairPlan、修复/验证/回滚结果，并把用户主目录标准化为 `%USERPROFILE%`，对 Key/Token/Secret/Password/Cookie/Auth/Session 等敏感内容脱敏；
- 用户无需安装 .NET；运行时代码不启动 `powershell.exe` / `pwsh.exe`，不依赖 `.ps1` / `.psm1` 或外置运行 DLL。

## V8.1.1 使用流程

```text
UAC 管理员授权
→ 一键扫描
→ 问题分级
→ 一键修复 / 中文 / English / 安装 / 卸载
→ 重新扫描验证
```

扫描完成后，主界面根据实际扫描结果提供：

1. **启动 Codex** — 使用扫描确认的 Desktop 实际路径启动。
2. **重启 Codex** — 使用实际 EXE 与 PID 重启，不依赖 URL 协议。
3. **一键修复** — 只执行安全白名单动作，执行后验证，失败回滚，并自动复检。
4. **智能迁移/恢复** — 根据普通目录、Junction、迁移状态或可恢复中断事务自动决定可用动作。
5. **中文** — 将 Desktop UI 切换到 `zh-CN`，操作后重新验证。
6. **English** — 将 Desktop UI 切换到 `en-US`，操作后重新验证。
7. **安装 / 卸载** — 管理 Codex Desktop 与 Codex CLI，安装/卸载后重新扫描确认。
8. **导出完整报告** — 导出隐私安全的 V8.1.1 中文 JSON 报告。

主界面还会展示所有扫描问题、严重度、状态、说明、扫描进度和运行日志。


## V8.1.1 安全原则

- 扫描阶段默认只读，不改注册表、不改 PATH、不删除文件、不写配置。
- `ManualRequired` / `ExternalRequired` 问题不会被伪报为“已修复”。
- 默认一键修复不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。
- 写入动作必须来自明确白名单；需要备份的动作必须先备份。
- 每个自动修复动作都必须有执行后验证；验证失败执行对应回滚。
- 语言自动化只绑定扫描确认的 Desktop，不猜测未知数据库、`.codex` 字段、MSIX/AppX 资源或二进制文件。
- Desktop 自动安装/卸载只使用官方包 ID `9NT1R1C2HH7J`；CLI 只使用 `@openai/codex`。
- 安装/卸载命令退出码不能单独作为成功依据，必须重新扫描验证。
- 卸载不删除 `%USERPROFILE%\.codex`、用户项目、备份或报告。
- 完整报告不输出 API Key、Token、Cookie、认证或会话敏感值。

## 一键扫描范围

V8.1.1 的统一健康扫描覆盖管理员/系统状态、Codex/ChatGPT Desktop、Codex CLI、`.codex` 与 Junction、迁移状态、已知配置、Codex 专用代理环境、Windows 代理环境、Git/npm 代理、DNS、直接 TLS、代理 HTTPS、Clash Verge/Mihomo/sing-box、TUN、可用网络路径、语言状态、安装状态、配置权限以及启动/重启路径等检查。

单个检查失败不会中止整个扫描；失败项会转化为可审计的安全问题摘要，后续检查继续执行。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类
- `versions/v7` — V7.1.2 PowerShell 兼容版
- `versions/v8-native` — V8.1.1 C#/.NET 8 原生维修中心

V7.1.2、V8.0.x 和 V8.1.0 发布物继续作为历史版本保留，不由 V8.1.1 工作流覆盖。

## 实机诊断规则

对于此前验证过的典型状态：

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

并建议配置 Codex 专用 `.env`，不会把“直连受限但代理可用”误报成单纯 TLS 故障。

## V8.1.1 构建

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

CI / Release 最终门禁包括：

- V8.1.1 原生回归测试全部通过；
- 输出存在且具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本固定为 `8.1.1`；
- 发布目录无 `.ps1` / `.psm1` / 外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`。

正式 Release 资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

## V7.1.2 兼容版

如果需要旧版 PowerShell 实现，可继续使用 `versions/v7`。V7.1.2 已保留 UTF-8 BOM、Windows PowerShell 5.1 兼容修复和 `.Count` 标量回归修复。

## License

MIT
