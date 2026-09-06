# Codex Doctor V8.1.1 原生维修中心

这是 Codex Doctor 的 Windows 原生主线版本，使用 **C# + .NET 8 + WinForms**。正式发布物为单文件 `CodexDoctor.exe`，运行时不启动 `powershell.exe` / `pwsh.exe`，也不加载 `.ps1` / `.psm1`。

V8.1.1 在 V8.1.0 维修中心基础上新增两组正式能力：**Codex Desktop 中文 / English 双向一键切换**，以及 **Codex Desktop / CLI 安装 / 卸载管理**。所有自动操作继续遵循“先扫描、再执行、重新扫描验证；无法验证就不伪报成功”的原则。

## 启动与管理员权限

V8.1.1 使用 `app.manifest` 的 `requireAdministrator` 请求管理员权限。标准流程从 **UAC** 开始：未获得管理员权限时程序不会进入主界面。

管理员权限不是为了让扫描随意修改系统；统一健康扫描默认只读。管理员权限用于后续经过用户确认的修复、进程、迁移、安装/卸载等需要提升权限的操作，避免运行到一半才因权限不足失败。

## 主流程：先扫描，再操作

```text
UAC
→ 一键扫描
→ 问题分级
→ 一键修复 / 中文 / English / 安装 / 卸载
→ 重新扫描验证
```

“一键扫描 Codex”仍是主入口。扫描采用异步编排，单项检查失败不会中止后续检查；失败项会转为安全、可审计的问题摘要。

扫描结果允许多个问题同时存在，严重度固定为：

```text
严重 Critical
→ 紧急 Urgent
→ 警告 Warning
→ 提示 Info
→ 正常 Ok
```

## 一键扫描范围

统一扫描覆盖包括：

- 管理员与系统运行状态；
- Codex Desktop / ChatGPT Desktop 的进程、实际 EXE、版本与安装来源；
- Codex CLI 的实际路径、PATH 可调用状态与版本；
- `%USERPROFILE%\.codex`、Junction/Reparse Point、链接目标和迁移状态；
- `.env`、`config.toml` 等已知文本配置与权限；
- Codex 专用代理、Windows 用户代理环境、Git/npm 代理冲突；
- DNS、直接 TLS、代理 HTTPS 和当前可用网络路径；
- Clash Verge / Mihomo / sing-box 进程与 TUN 状态；
- Desktop 语言状态与可信语言适配能力；
- Desktop / CLI 安装状态；
- 启动/重启实际路径可用性。

扫描阶段不改注册表、不改 PATH、不删除文件、不写配置。

## 一键修复：白名单 + 验证 + 回滚

V8.1.1 不会把所有 Warning 都机械地“修掉”。只有同时满足以下条件的问题才进入 RepairPlan：能够明确检测、拥有确定修复策略、属于安全白名单，并且执行后可以验证。

执行过程为：

```text
生成 RepairPlan
→ 用户确认
→ 必要时先备份
→ 执行动作
→ 执行后验证
→ 验证失败则回滚该动作
→ 完整自动复检
```

`ManualRequired` 和 `ExternalRequired` 始终保留真实状态，不会为了显示绿色而伪报 Fixed。默认一键修复也不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。

## 主要操作

完成扫描后，主界面根据实际状态提供：

1. **启动 Codex**：从扫描结果选择可信 Desktop 实际路径启动。
2. **重启 Codex**：按扫描到的 EXE 和 PID 重启，不依赖 `chatgpt:` URL 协议。
3. **一键修复**：执行白名单 RepairPlan，逐项验证/回滚，随后自动复检。
4. **智能迁移/恢复**：识别普通 `.codex`、有效 Junction、可安全恢复的中断迁移事务和歧义状态。
5. **中文**：将扫描确认的 Desktop UI 切换到 `zh-CN`。
6. **English**：将扫描确认的 Desktop UI 切换到 `en-US`。
7. **安装 / 卸载**：进入独立 Codex 安装管理窗口，管理 Desktop 与 CLI。
8. **导出完整报告**：输出隐私安全、可审计的 V8.1.1 中文 JSON。

语言与安装按钮只有在完成扫描后才进入可操作状态。

## 中文 / English 双向一键切换

V8.1.1 明确区分界面语言、回答语言偏好和 CLI 输出偏好。

语言切换优先使用经过确认、可逆、明确属于 Desktop UI 的可信语言适配器；如果没有可信本地适配器，才尝试绑定**扫描确认的 Desktop 实际 EXE 与 PID**，通过 Windows UI Automation 操作应用自身 `Settings → General → Language`。

- `中文` 目标固定为 `zh-CN`；
- `English` 目标固定为 `en-US`；
- 不对未知数据库字段、普通 `.codex/.env`、`config.toml` 的 `language/locale/ui_language` 字段进行猜测写入；
- 不修改 MSIX/AppX 安装资源或 Desktop 二进制文件；
- UI Automation 只绑定扫描确认的 Desktop 主窗口；
- 操作后必须重新读取/重新扫描验证目标语言；
- 无法验证时返回失败/需要用户操作，**不伪报成功**。

## Codex Desktop / CLI 安装 / 卸载

V8.1.1 新增独立 `Codex 安装 / 卸载` 窗口，分别显示 Desktop 与 CLI 的安装状态、发现路径和版本。

### Desktop

Desktop 自动安装/卸载只使用 Microsoft Store / Windows Package Manager 的官方包标识：

```text
9NT1R1C2HH7J
```

自动路径使用 `winget.exe`。如果系统没有可用的 winget，程序不会尝试下载第三方安装包或猜测 URL，而是返回 `ManualRequired`。

### CLI

Codex CLI 自动安装只使用 npm 官方包：

```text
@openai/codex
```

对应动作：

```text
npm.cmd install -g @openai/codex
npm.cmd uninstall -g @openai/codex
```

如果本机没有可用的 `npm.cmd`，程序返回 `ManualRequired`，不会伪造安装成功。

### 安装安全合同

安装或卸载命令退出码为 0 **不等于最终成功**。每次动作结束后，Codex Doctor 都会调用 discovery **重新扫描**：

- 安装只有在重新扫描确认目标已经存在时才报告成功；
- 卸载只有在重新扫描确认目标已经不存在时才报告成功；
- 状态与命令结果冲突时报告验证失败；
- Desktop 卸载不会删除 `%USERPROFILE%\.codex`；
- CLI 卸载不会删除 `.codex`、用户项目、Codex Doctor 备份或诊断报告；
- 卸载前 GUI 必须二次确认并明确上述保留范围。

## 智能迁移/恢复

迁移逻辑不会只根据“是不是 Junction”做二选一。V8.1.1 会结合当前 `.codex` 状态、迁移状态文件和可恢复性做决策：

- 普通目录：允许进入迁移流程；
- 有效 Junction + 有效迁移状态：允许恢复；
- 可证明安全的中断迁移：允许恢复事务；
- 状态歧义或无法证明安全：只显示详情，不自动删除、覆盖或移动用户数据。

迁移仍只针对 `%USERPROFILE%\.codex` 数据目录，不移动 WindowsApps/MSIX 应用包本体。

## 导出完整报告与隐私

`HealthReportExporter` 导出的 V8.1.1 中文 JSON 包含：

- 当前扫描与 ScanId；
- 管理员权限状态；
- 本机发现和网络诊断；
- 全部问题及状态；
- 最近 RepairPlan；
- 修复动作、验证结果和回滚状态；
- 修复前 / 修复后扫描及对应 ScanId。

报告会把用户主目录标准化为 `%USERPROFILE%`。Token、Key、Secret、Password、Cookie、Auth、Session、Bearer 等敏感信息不会输出原值。

报告只记录 `软件作者=Aix`，不会把 QQ 或抖音号写入诊断数据。

## 软件作者

**软件作者：Aix ｜ QQ：976936105 ｜ 抖音：xch03209527**

上述联系方式只用于界面与公开项目文档。

## 兼容保留

V8.1.1 延续 V8.1.0 的维修中心、一键扫描、一键修复、自动复检、真实路径启动/重启、智能迁移/恢复和隐私报告能力，也继续保留 V8.0.1 的 Desktop/CLI 实际路径发现、`.codex` 审计、配置敏感值隐藏和 `codex doctor` CLI 路径复用。

V8.1.0 的历史 **一键中文** 入口在 V8.1.1 已升级为 **中文 / English** 双向按钮；旧名称仅作为版本兼容说明保留，不代表当前界面仍存在旧按钮。

V8.0.1 的历史安全合同原样继续成立：**扫描本机 Codex** 功能保留，**扫描默认只读**；敏感配置只显示安全状态；没有可信自动语言入口时显示“**需要用户操作**”；语言功能**不修改 MSIX/AppX**，也不把未经验证的 `.codex` 字段当作 Desktop UI 设置。

此前实机样本“DNS 正常、直接 TLS 超时、本机 `127.0.0.1:7897` 代理 HTTPS 可用、`.codex/.env` 缺失”仍必须判定为 **需要配置代理**，不能退化为单纯 TLS 故障。

## 构建与最终门禁

Windows GitHub Actions 执行：

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

最终门禁验证：

- V8.1.1 原生回归测试通过；
- `CodexDoctor.exe` 存在并具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本为 `8.1.1`；
- 发布目录无 `.ps1` / `.psm1` / 外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码无 `powershell.exe` / `pwsh.exe` 依赖；
- 生成 SHA256。

正式发布资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

用户无需安装 .NET，也无需准备 PowerShell 脚本或模块。
