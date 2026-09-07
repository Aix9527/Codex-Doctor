# Codex Doctor

Windows Codex / ChatGPT Desktop **一键扫描、连接诊断、安全修复、Reconnecting 自愈、智能迁移与恢复**工具。

## 当前推荐版本：V8.2.0 Reconnecting 端到端自愈

V8.2.0 使用 **C# + .NET 8 + WinForms**，正式发布物为真正的 `win-x64` 自包含单文件 `CodexDoctor.exe`。用户无需额外安装 .NET，运行时代码不启动 `powershell.exe` / `pwsh.exe`，也不依赖 `.ps1` / `.psm1` 或外置运行 DLL。

### V8.2.0 的核心变化

主界面新增 **`Reconnecting 自愈`**。它不把“进程又启动了”当作恢复成功，而是执行完整闭环：

```text
fresh 全量扫描
→ 选择可验证网络路径
→ 生成 RepairPlan
→ 备份 / 修复 / 验证 / 失败回滚
→ 使用扫描确认的真实 EXE/PID 重启 Desktop
→ fresh Desktop discovery
→ fresh 全量网络诊断
→ 输出结构化恢复状态
```

只有以下两项同时成立才返回 `RECOVERED`：

- 修复后的网络路径通过 fresh 验证；
- 与修复前相同真实 EXE 路径的 Codex / ChatGPT Desktop 被重新 discovery 且处于运行状态。

网络恢复但 Desktop 未验证时只能返回 `NETWORK_RECOVERED`。其它失败会区分 `PROXY_FAILED`、`DNS_FAILED`、`TLS_FAILED`、`DESKTOP_RESTART_FAILED`、`MANUAL_REQUIRED`，不会把未验证状态伪报为成功。

## 不猜代理，只使用已验证网络事实

V8.2.0 复用现有 `DiagnosisService`：

- 直接 TLS 已健康：保持直连，不强制写代理；
- 直连失败但本地代理 HTTPS 已验证：只使用 `DiagnosisResult.ProxyUrl` 中的已验证地址；
- 代理没有通过 HTTPS 验证：不得写入 `.codex/.env`；
- 不根据“常见端口”猜测代理；
- 默认不写 Windows 用户级 HTTP/HTTPS 代理环境变量。

`.codex/.env` 修复继续保留无关配置，修改前备份，写入后重新读取验证，验证失败自动回滚。

## V8.2.0 使用流程

```text
UAC 管理员授权
→ 一键扫描 Codex
→ 问题分级
→ Reconnecting 自愈 / 一键修复 / 智能迁移恢复 / 中文 / English / 安装 / 卸载
→ fresh 重新扫描验证
→ 导出完整报告
```

扫描完成后主界面提供：

1. **启动 Codex** — 使用扫描确认的真实 Desktop 路径。
2. **重启 Codex** — 使用真实 EXE/PID，不依赖 `chatgpt:` URL。
3. **一键修复** — 仅执行安全白名单动作，逐项验证，失败回滚并自动复检。
4. **Reconnecting 自愈** — 网络 + Desktop 双重 fresh 验证。
5. **智能迁移/恢复** — 安全处理普通 `.codex`、Junction、中断事务与歧义状态。
6. **中文** — 目标 `zh-CN`，操作后重新验证。
7. **English** — 目标 `en-US`，操作后重新验证。
8. **安装 / 卸载** — 管理 Codex Desktop 与 Codex CLI。
9. **导出完整报告** — 使用标准 Windows 另存为窗口，自由选择保存位置。

## Reconnecting 自愈安全门

专项自愈只有在完成 fresh 扫描、存在可信 Desktop，并且没有严重/紧急 `ManualRequired` / `ExternalRequired` 阻断项时才允许执行。

修复动作继续沿用 RepairPlan 安全白名单。需要备份的动作先备份；每项执行后必须验证；验证失败执行对应回滚。任何未知 Desktop、未经验证的代理、未知数据库字段、MSIX/AppX 资源或应用二进制文件都不会被猜测修改。

## 完整报告

报告继续记录：

- 当前 ScanId；
- 全部问题、诊断与 discovery；
- 最近 RepairPlan；
- 修复动作、验证与回滚；
- 修复前 / 修复后 ScanId；
- 最近一次 `Reconnecting自愈` 的 before/after ScanId、结构化状态、网络验证、Desktop 验证、选中网络路径和动作结果。

用户主目录会标准化为 `%USERPROFILE%`；Key、Token、Secret、Password、Cookie、Auth、Session、Bearer 等敏感内容继续脱敏。

## V8.1.2 能力继续保留

V8.2.0 完整继承 V8.1.2：

- **报告自由选择位置**：`SaveFileDialog`，取消保存时零写入；
- **Desktop 动态卸载身份**：卸载前重新扫描真实 Desktop，根据确认的 `ChatGPT.exe` / `Codex.exe` 映射受限的 `ChatGPT` / `Codex` 精确名称，完成后重新扫描验证；
- **多进程 Desktop 窗口绑定**：扫描 PID → 可信进程树 → 完全相同 EXE 路径；无关窗口和不可见窗口拒绝；
- Desktop 卸载仍只通过 winget，不直接删除应用目录。

V8.1.2 的 Release 工作流在 V8.2.0 中已转为历史手动复现，并固定 checkout `v8.1.2` tag，不再监听 main。

## V8.1.1 能力继续保留

- **中文 / English** 双向切换；
- **安装 / 卸载** 管理；
- Desktop 自动安装仅使用 Microsoft Store / winget 官方包 ID `9NT1R1C2HH7J`；
- CLI 自动管理仅使用 npm 官方包 `@openai/codex`；
- 安装/卸载命令退出码不能单独判定成功，必须重新扫描验证；
- 无法验证时不伪报成功。

V8.1.0 历史版本中的 **一键中文** 入口已升级为后续版本的中文 / English 双向按钮，该名称仅作为历史兼容说明保留。

## 核心安全原则

- manifest 使用 `requireAdministrator`，未通过 **UAC** 不进入主界面；
- 扫描默认只读，不改注册表、不改 PATH、不删除文件、不写配置；
- `ManualRequired` / `ExternalRequired` 不会被伪报 Fixed；
- 自动修复必须来自明确白名单；
- 语言自动化不按窗口标题猜测，不修改未知数据库、普通 `.codex` 语言字段、MSIX/AppX 或二进制资源；
- Desktop 自动安装只使用官方 Store/winget 包；
- CLI 自动安装/卸载只使用 `@openai/codex`；
- 卸载不删除 `%USERPROFILE%\.codex`、用户项目、备份或报告；
- 最终 `RECOVERED` 必须有网络和 Desktop 双重 fresh 证据。

## 一键扫描范围

统一健康扫描覆盖管理员/系统状态、Codex/ChatGPT Desktop、Codex CLI、`.codex` 与 Junction、迁移状态、已知配置、Codex 专用代理环境、Windows 代理环境、Git/npm 代理、DNS、直接 TLS、代理 HTTPS、Clash Verge/Mihomo/sing-box、TUN、可用网络路径、语言状态、安装状态、配置权限以及启动/重启路径等检查。

单个检查失败不会终止其它检查；异常项目会转成可审计的安全问题摘要。

## 典型 Reconnecting 场景

对于此前验证过的典型状态：

```text
DNS = 正常
直接 TLS = 超时
本机代理 = http://127.0.0.1:7897，可通过 HTTPS 验证
.codex/.env = 不存在
```

V8.2.0 会判断“存在已验证代理路径”，可将该**已经验证**的代理写入 Codex 专用 `.env`，随后重启真实 Desktop 并进行 fresh 双重验证，不会把“直连受限但代理可用”误报成单纯 TLS 故障。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihimo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类
- `versions/v7` — V7.1.2 PowerShell 兼容版
- `versions/v8-native` — V8.2.0 C#/.NET 8 原生维修中心

V7.1.2、V8.0.x、V8.1.0、V8.1.1、V8.1.2 发布物继续作为历史版本保留。

## V8.2.0 构建

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

CI / Release 最终门禁：

- V8.2.0 原生回归测试全部通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本固定为 `8.2.0`；
- 发布目录无 `.ps1` / `.psm1` / 外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`；
- `v8.2.0` Release 已存在时跳过且不覆盖任何资产。

正式 Release 资产：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

## 软件作者

**Aix ｜ QQ：976936105 ｜ 抖音：xch03209527**

## License

MIT
