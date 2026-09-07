# Codex Doctor V8.2.0 原生维修中心

Codex Doctor V8.2.0 是 Windows 原生 Codex / ChatGPT Desktop 维修主线，使用 **C# + .NET 8 + WinForms**。正式发布物为 `win-x64` 自包含单文件 `CodexDoctor.exe`，用户无需额外安装 .NET；运行时代码不启动 `powershell.exe` / `pwsh.exe`，也不依赖 `.ps1` / `.psm1` 或外置运行 DLL。

## V8.2.0：Reconnecting 端到端自愈

V8.2.0 新增主操作 **`Reconnecting 自愈`**。它不是简单重启，而是一个有证据链的恢复闭环：

```text
fresh 全量扫描
→ 选择可验证网络路径
→ RepairPlan 安全白名单
→ 备份 / 修复 / 验证 / 失败回滚
→ 按扫描确认的真实 EXE/PID 重启 Desktop
→ fresh Desktop discovery
→ fresh 全量网络诊断
→ 生成结构化恢复状态
```

最终只有同时满足以下两项才返回 `RECOVERED`：

1. 修复后的网络路径已重新验证；
2. 与修复前相同真实 EXE 路径的 Desktop 已重新 discovery 且正在运行。

网络恢复但 Desktop 未验证时只能返回 `NETWORK_RECOVERED`；DNS、TLS、代理、Desktop 重启或安全门问题会分别返回 `DNS_FAILED`、`TLS_FAILED`、`PROXY_FAILED`、`DESKTOP_RESTART_FAILED`、`MANUAL_REQUIRED`。未验证状态不会伪报成功。

## 网络路径决策

专项自愈只使用既有 `DiagnosisService` 的已验证事实：

- 直接 TLS 已健康：继续直连，不强制写代理；
- 直连不可用且本地代理 HTTPS 验证通过：只使用 `DiagnosisResult.ProxyUrl` 中的已验证代理；
- 未验证的代理不得写入 `.codex/.env`；
- 不根据“常见端口”猜测代理；
- 默认不写 Windows 用户级 HTTP/HTTPS 代理环境变量。

`.codex/.env` 修复继续保留无关配置、修改前备份、写后验证和失败回滚。

## 主流程

```text
UAC 管理员授权
→ 一键扫描 Codex
→ 问题分级
→ Reconnecting 自愈 / 一键修复 / 智能迁移恢复 / 中文 / English / 安装 / 卸载
→ fresh 重新扫描验证
→ 导出完整报告
```

扫描结果允许多个问题并存，并按 Critical / Urgent / Warning / Info / Ok 稳定排序。扫描默认只读；只有明确属于 RepairPlan 安全白名单、可验证并可按合同回滚的动作才允许自动执行。

## 主要操作

1. **启动 Codex**：使用扫描确认的真实 Desktop EXE。
2. **重启 Codex**：使用扫描确认的真实 EXE/PID，不依赖 `chatgpt:` URL。
3. **一键修复**：执行白名单 RepairPlan，逐项验证，失败回滚，并自动复检。
4. **Reconnecting 自愈**：执行网络 + Desktop 双重验证的专项闭环。
5. **智能迁移/恢复**：处理普通 `.codex`、有效 Junction、可恢复中断事务和歧义状态。
6. **中文**：目标 `zh-CN`。
7. **English**：目标 `en-US`。
8. **安装 / 卸载**：管理 Desktop 和 CLI。
9. **导出完整报告**：标准 Windows 另存为窗口，自由选择保存位置。

## 报告证据链

`HealthReportExporter` 会记录当前 ScanId、RepairPlan、修复/验证/回滚、修复前后 ScanId，并在执行过专项自愈后增加 `Reconnecting自愈` 区段：

- before / after ScanId；
- 结构化恢复状态；
- 网络验证结果；
- Desktop 验证结果；
- 已选择的代理路径；
- 修复动作结果；
- 中文摘要。

用户主目录会标准化为 `%USERPROFILE%`；Token、Key、Secret、Password、Cookie、Auth、Session、Bearer 等敏感信息继续脱敏。

## V8.1.2 稳定性能力继续保留

- 报告使用 `SaveFileDialog` 自由选择位置；
- Desktop 卸载前 fresh 扫描真实客户端，根据 `ChatGPT.exe` / `Codex.exe` 动态映射受限的 `ChatGPT` / `Codex` 精确名称；
- Desktop 卸载仅通过 winget，不直接删除应用目录；
- 中文 / English UI Automation 支持多进程窗口绑定：扫描 PID → 可信进程树 → 完全相同 EXE 路径；
- 不按窗口标题猜测，不修改未知数据库、MSIX/AppX 或二进制资源。

## V8.1.1 历史兼容能力继续保留

V8.1.1 引入的 **中文 / English** 双向切换与 **安装 / 卸载** 管理仍是当前兼容合同。Desktop 自动安装只使用官方 Microsoft Store / winget 包 ID `9NT1R1C2HH7J`；CLI 自动管理只使用 npm 官方包 `@openai/codex`。安装或卸载完成后必须重新扫描验证，命令退出码不能单独作为成功依据，无法验证时不伪报成功。

V8.1.0 历史版本中的 **一键中文** 入口已在后续版本升级为中文 / English 双向按钮；该名称仅作为历史兼容说明保留。

## 安全边界

- manifest 固定 `requireAdministrator`，未通过 UAC 不进入主界面；
- 扫描默认只读；
- `ManualRequired` / `ExternalRequired` 不会被伪报 Fixed；
- 自动动作必须来自 RepairPlan 白名单；
- 需要备份的动作先备份；
- 每个自动动作必须有执行后验证；
- 验证失败执行对应回滚；
- 不下载第三方代理、安装器或脚本；
- 不直接删除 Desktop 安装目录；
- 不删除 `%USERPROFILE%\.codex`、用户项目、备份或报告；
- `RECOVERED` 必须有网络 + Desktop 双重 fresh 证据。

## 构建与发布门

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

最终门禁：

- V8.2.0 原生回归测试通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本固定 `8.2.0`；
- 发布目录无 `.ps1` / `.psm1` / 外置 `.dll` / `.runtimeconfig.json` / `.deps.json`；
- C# 运行时代码无 `powershell.exe` / `pwsh.exe` 依赖；
- 生成 `CodexDoctor.exe.sha256`；
- 既有 Release 不覆盖。

正式资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

## 软件作者

**软件作者：Aix ｜ QQ：976936105 ｜ 抖音：xch03209527**
