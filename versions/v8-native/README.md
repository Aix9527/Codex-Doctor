# Codex Doctor V8.2.0 Reconnecting 自愈中心

这是 Codex Doctor 的 Windows 原生主线版本，使用 **C# + .NET 8 + WinForms**。正式发布物为自包含单文件 `CodexDoctor.exe`，运行时不启动 `powershell.exe` / `pwsh.exe`，也不依赖 `.ps1` / `.psm1` 或外置运行 DLL。

V8.2.0 在 V8.1.2 / V8.1.1 能力之上新增 **Reconnecting 专项自愈**。所有自动操作继续遵循“先扫描、再执行、执行后验证、必要时回滚、重新扫描；无法验证就不伪报成功”的原则。

## 启动与管理员权限

使用 `app.manifest` 的 `requireAdministrator` 请求管理员权限。标准流程从 **UAC** 开始；未获得管理员权限时程序不会进入主界面。

管理员权限不改变只读扫描原则：**扫描默认只读**，不会因为提升权限就任意修改系统。需要实际变更时必须来自明确白名单并由对应动作合同约束。

## 主流程

```text
UAC
→ 一键扫描 Codex / 扫描本机 Codex
→ 问题分级
→ 一键修复 / Reconnecting 自愈 / 中文 / English / 安装 / 卸载
→ 重新扫描验证
→ 导出完整报告
```

统一扫描采用异步编排，单项检查失败不会中止后续检查；失败项会转为安全、可审计的问题摘要。

## Reconnecting 专项自愈

专项流程固定为：

```text
修复前独立扫描
→ 生成专项安全计划
→ 必要时备份
→ 执行动作
→ 执行后验证
→ 验证失败回滚
→ 必要时最后重启 Codex Desktop
→ fresh rescan
→ 终态分类
```

允许的专项动作 ID 只有：

- `codex.proxy.env`
- `git.proxy.clear`
- `npm.proxy.clear`
- `codex.desktop.restart`

安全边界：直连正常时不写代理；候选代理必须通过 HTTPS 验证后才允许写 `.codex/.env`；默认不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。计划动作无法安全构造时会留下失败审计，不会静默跳过并伪报成功。

网络与 Desktop 都恢复时终态为完整恢复；网络已恢复但 Desktop 尚未正常运行时保持独立网络恢复状态，不会混成完整成功。DNS、TLS、代理、Desktop 重启或证据不足也分别保留真实失败/人工处理分类。

## 一键扫描范围

统一扫描覆盖：

- 管理员与系统状态；
- Codex Desktop / ChatGPT Desktop 的进程、实际 EXE、版本与安装来源；
- Codex CLI 的实际路径、PATH 可调用状态与版本；
- `%USERPROFILE%\.codex`、Junction/Reparse Point、链接目标和迁移状态；
- `.env`、`config.toml` 等已知文本配置与权限；
- Codex 专用代理、Windows 用户代理环境、Git/npm 代理冲突；
- DNS、直接 TLS、代理 HTTPS 和当前可用网络路径；
- Clash Verge / Mihomo / sing-box 与 TUN 状态；
- Desktop 语言状态、安装状态以及启动/重启路径。

扫描阶段不改注册表、不改 PATH、不删除文件、不写配置。敏感配置只输出经过脱敏的状态/摘要。

## 一键修复：白名单 + 验证 + 回滚

通用一键修复与 Reconnecting 专项是两个独立入口，但都坚持安全白名单。只有能够明确检测、拥有确定策略、执行后可以验证的动作才允许自动执行。

```text
生成计划
→ 用户确认
→ 必要时先备份
→ 执行动作
→ 执行后验证
→ 验证失败则回滚
→ 自动复检
```

`ManualRequired` 和 `ExternalRequired` 始终保留真实状态。默认一键修复不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。

## 主要操作

1. **启动 Codex**：使用扫描确认的 Desktop 实际路径。
2. **重启 Codex**：按扫描到的 EXE 和 PID 重启，不依赖 `chatgpt:` URL 协议。
3. **一键修复**：执行通用 RepairPlan，逐项验证/回滚，随后自动复检。
4. **Reconnecting 自愈**：执行连接专项闭环并给出确定性终态。
5. **智能迁移/恢复**：识别普通 `.codex`、有效 Junction、可安全恢复的中断迁移事务和歧义状态。
6. **中文**：将扫描确认的 Desktop UI 切换到 `zh-CN`。
7. **English**：将扫描确认的 Desktop UI 切换到 `en-US`。
8. **安装 / 卸载**：管理 Codex Desktop 与 Codex CLI。
9. **导出完整报告**：输出隐私安全、可审计的 V8.2.0 中文 JSON。

## 中文 / English

语言切换优先使用可信、可逆、明确属于 Desktop UI 的适配器；否则只对扫描确认的 Desktop EXE/PID/可信进程关系尝试 Windows UI Automation。

- `中文` 目标为 `zh-CN`；
- `English` 目标为 `en-US`；
- 不猜测未知数据库字段或普通 `.codex` 语言字段；
- **不修改 MSIX/AppX** 安装资源或 Desktop 二进制；
- 操作后必须重新读取/重新扫描验证；
- 没有可信自动入口时显示“**需要用户操作**”；
- 无法验证时不伪报成功。

## Codex Desktop / CLI 安装 / 卸载

V8.1.1 引入的安装管理能力在 V8.2.0 继续保留。

Desktop 自动安装只使用 Microsoft Store / winget 官方包 ID：

```text
9NT1R1C2HH7J
```

Codex CLI 自动管理只使用 npm 官方包：

```text
@openai/codex
```

如果 winget/npm 不可用则返回 `ManualRequired`，不下载第三方安装器。安装/卸载命令退出码不能单独判定成功，每次动作后必须**重新扫描**确认最终状态。卸载不会删除 `%USERPROFILE%\.codex`、用户项目、备份或报告。

## 智能迁移/恢复

根据 `.codex` 当前状态、迁移状态文件和可恢复性做决策：普通目录允许迁移；有效 Junction + 有效状态允许恢复；可证明安全的中断事务允许恢复；歧义状态不自动删除、覆盖或移动用户数据。

## 导出完整报告与隐私

`HealthReportExporter` 的 V8.2.0 报告包含：当前扫描、RepairPlan、修复动作/验证/回滚、修复前后 ScanId，以及最近一次 Reconnecting 专项的修复前/后网络诊断、动作结果和终态。

报告将用户主目录标准化为 `%USERPROFILE%`。Token、Key、Secret、Password、Cookie、Auth、Session、Bearer 等敏感信息不会输出原值。

## 历史兼容合同

V8.1.2 的报告保存位置、Desktop 动态卸载身份和多进程窗口绑定继续保留。V8.1.1 的 **中文 / English**、**安装 / 卸载**、`9NT1R1C2HH7J`、`@openai/codex`、操作后**重新扫描**以及**不伪报成功**合同继续成立。

V8.1.0 的历史 **一键中文** 入口名称仅作为兼容说明保留。V8.0.1 的历史安全合同也继续成立：**扫描本机 Codex**、**扫描默认只读**、敏感配置隐藏、无可信入口时“**需要用户操作**”、语言功能**不修改 MSIX/AppX**，并继续识别 Codex Desktop 与 Codex CLI。

## 软件作者

**软件作者：Aix ｜ QQ：976936105 ｜ 抖音：xch03209527**

## 构建与最终门禁

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

最终门禁验证：V8.2.0 原生回归、PE `MZ`、`requireAdministrator`、版本 `8.2.0`、真正单文件、无运行时 PowerShell/外置 DLL，以及 SHA256。

正式资产：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```
