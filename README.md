# Codex Doctor

## V9.0.0 Functional Rebuild（当前推荐版本）

新增独立 `versions/v9`，不依赖旧版程序集。入口改为 **一键修复重连**：检测当前本地代理及实际监听端口，通过 HTTPS 验证后创建/更新 `~/.codex/.env` 的 `HTTP_PROXY` 和 `HTTPS_PROXY`，保留其他配置，备份并复检。

同时重建客户端识别、进程启动/重启、官方安装管理、配置权限/PATH 修复、Git/npm 代理清理、迁移恢复、语言设置自动化和报告导出。[V9 使用与验证说明](versions/v9/README.md)，[V9.0.0 发布说明](RELEASE_NOTES_V9.0.0.md)。

V9 已有行为测试、真实本地代理临时目录测试和 UI 自动化夹具证据；尚不能据此宣称全部客户端场景已经验收或所有 Reconnecting 故障均可自动解决。下面保留 V8 历史说明。

Windows Codex / ChatGPT Desktop **一键扫描、连接诊断、安全修复、Reconnecting 专项自愈、智能迁移与恢复**工具。

## 历史版本：V8.2.0 Reconnecting 自愈中心

V8.2.0 使用 **C# + .NET 8 + WinForms**，正式发布物为真正的 `win-x64` 自包含单文件 `CodexDoctor.exe`。它在 V8.1.2 的 Desktop 稳定性修复、V8.1.1 的中文 / English 与安装 / 卸载能力之上，新增面向 Codex Desktop 持续 `Reconnecting` 的确定性专项闭环。

### V8.2.0 Reconnecting 自愈

点击 **Reconnecting 自愈** 后执行：

```text
修复前独立扫描
→ 生成安全白名单计划
→ 必要时备份
→ 执行动作
→ 逐项验证
→ 验证失败则回滚
→ 必要时最后重启 Codex Desktop
→ 全新独立复检
→ 终态分类
```

专项白名单只有四类动作：

- `codex.proxy.env` — 仅在直连不可用、且候选代理已经通过 HTTPS 验证时写入 Codex 专用 `.codex/.env`；
- `git.proxy.clear` — 清理已确认冲突的 Git 代理；
- `npm.proxy.clear` — 清理已确认冲突的 npm 代理；
- `codex.desktop.restart` — 仅在需要时最后执行，使用扫描确认的 Desktop 身份/路径。

安全边界固定：**直连正常时不写代理；未通过 HTTPS 验证的代理不写入 `.codex/.env`；默认不写 Windows 用户级 HTTP/HTTPS 代理环境变量。** 每个可变更动作继续复用既有 RepairEngine 的备份、验证和回滚语义，未知/无法安全构造的动作不会被静默跳过为成功。

专项闭环结束后会产生明确终态，其中：

- `RECOVERED`：网络路径已满足完整恢复条件，且 Codex Desktop 正在运行；
- `NETWORK_RECOVERED`：网络路径已恢复，但 Desktop 仍未达到完整运行条件；
- 其它未恢复情况会保持 `ProxyFailed`、`DnsFailed`、`TlsFailed`、`DesktopRestartFailed` 或 `ManualRequired` 等真实分类，不伪报成功。

“导出完整报告”会自动带上最近一次 Reconnecting 自愈的修复前/后 ScanId、网络诊断、动作结果、验证/回滚证据和最终状态，并继续对用户目录、Key、Token、Secret、Password、Cookie、Auth、Session、Bearer 等敏感值脱敏。

## V8.2.0 使用流程

```text
UAC 管理员授权
→ 一键扫描 Codex
→ 问题分级
→ 一键修复 / Reconnecting 自愈 / 中文 / English / 安装 / 卸载 / 迁移恢复
→ 独立重新扫描验证
→ 导出完整报告
```

扫描完成后，主界面根据实际状态提供：

1. **启动 Codex** — 使用扫描确认的 Desktop 实际路径启动。
2. **重启 Codex** — 使用实际 EXE 与 PID 重启，不依赖 URL 协议。
3. **一键修复** — 执行通用 RepairPlan 白名单，执行后验证，失败回滚，并自动复检。
4. **Reconnecting 自愈** — 针对持续连接重试执行独立闭环，并输出确定性终态。
5. **智能迁移/恢复** — 根据普通目录、Junction、迁移状态或可恢复中断事务决定安全动作。
6. **中文** — 将 Desktop UI 切换到 `zh-CN`，操作后重新验证。
7. **English** — 将 Desktop UI 切换到 `en-US`，操作后重新验证。
8. **安装 / 卸载** — 管理 Codex Desktop 与 Codex CLI；动作后重新扫描确认。
9. **导出完整报告** — 导出隐私安全的 V8.2.0 中文 JSON 报告，并由用户自由选择保存位置。

## V8.2.0 安全原则

- 扫描阶段默认只读，不改注册表、不改 PATH、不删除文件、不写配置。
- `ManualRequired` / `ExternalRequired` 问题不会被伪报为“已修复”。
- 通用一键修复与 Reconnecting 自愈都只允许明确白名单动作。
- 直连正常时不会为了“保险”写入代理。
- 代理只有在真实 HTTPS 验证通过后才允许用于 `codex.proxy.env`。
- 默认不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。
- 需要备份的动作必须先备份；每个自动修复动作都必须执行后验证；验证失败执行对应回滚。
- Reconnecting 自愈中的 Desktop 重启必须排在需要它的配置动作之后。
- 语言自动化只绑定扫描确认的 Desktop，不按窗口标题猜测，不修改未知数据库、MSIX/AppX 资源或应用二进制。
- Desktop 自动安装仍只使用 Microsoft Store / winget 官方包 ID `9NT1R1C2HH7J`；CLI 自动管理只使用 npm 官方包 `@openai/codex`。
- 安装/卸载命令退出码不能单独作为成功依据，必须重新扫描验证。
- 卸载不删除 `%USERPROFILE%\.codex`、用户项目、备份或报告。
- 完整报告不输出 API Key、Token、Cookie、认证或会话敏感原值。

## 一键扫描范围

V8.2.0 的统一健康扫描覆盖管理员/系统状态、Codex/ChatGPT Desktop、Codex CLI、`.codex` 与 Junction、迁移状态、已知配置、Codex 专用代理环境、Windows 代理环境、Git/npm 代理、DNS、直接 TLS、代理 HTTPS、Clash Verge/Mihomo/sing-box、TUN、可用网络路径、语言状态、安装状态、配置权限以及启动/重启路径等检查。

单个检查失败不会中止整个扫描；失败项会转化为可审计的问题摘要，后续检查继续执行。

## V8.1.2 历史能力保留

V8.1.2 的以下稳定性修复继续存在：

- **报告导出自由选择位置**：使用标准 Windows `SaveFileDialog`；
- **Desktop 动态卸载身份**：根据重新扫描确认的 `ChatGPT.exe` / `Codex.exe` 决定精确 winget 名称；
- **多进程 Desktop 窗口绑定**：扫描 PID → 可信进程树 → 完全相同 EXE 路径。

### V8.1.2 使用流程（历史说明）

```text
UAC 管理员授权
→ 一键扫描
→ 问题分级
→ 一键修复 / 中文 / English / 安装 / 卸载 / 迁移恢复
→ 重新扫描验证
→ 导出完整报告（自由选择保存位置）
```

V8.1.1 引入的 **中文 / English 双向一键切换**、**Codex Desktop / CLI 安装 / 卸载**、官方 Desktop 包 ID `9NT1R1C2HH7J`、npm 包 `@openai/codex` 以及“操作后必须重新扫描、不伪报成功”的合同继续保留。V8.1.0 的历史“一键中文”名称仅作为兼容说明存在。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类
- `versions/v7` — V7.1.2 PowerShell 兼容版
- `versions/v8-native` — V8.2.0 C#/.NET 8 原生维修与 Reconnecting 自愈中心（历史版本）
- `versions/v9` — V9.0.0 Functional Rebuild 独立重构版（当前推荐版本）

V7.1.2、V8.0.x、V8.1.0、V8.1.1、V8.1.2 的发布说明与历史 Release 工作流继续保留；旧版本工作流只用于固定 tag 的手动历史复现，不覆盖既有资产。

## 实机诊断规则

对于典型状态：

```text
DNS = 正常
直接 TLS = 超时
本机代理 = http://127.0.0.1:7897，可通过 HTTPS 验证
.codex/.env = 不存在
```

V8 会判断为“需要配置代理”，Reconnecting 自愈允许在验证通过后生成 `codex.proxy.env` 动作；若直连本身正常，则不会生成该写入动作。

## V8.2.0 构建

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

- V8.2.0 原生回归测试全部通过；
- 输出存在且具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本固定为 `8.2.0`；
- 发布目录无 `.ps1` / `.psm1` / 外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`。

正式 Release 资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

## 软件作者

**软件作者：Aix ｜ QQ：976936105 ｜ 抖音：xch03209527**

## License

MIT
