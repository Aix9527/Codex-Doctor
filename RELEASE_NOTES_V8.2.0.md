# Codex Doctor V8.2.0 Reconnecting 专项自愈

**软件作者：Aix**

V8.2.0 在 V8.1.2 Desktop 稳定性修复基础上新增独立 **Reconnecting 自愈** 闭环，目标是处理 Codex / ChatGPT Desktop 持续连接重试、直连受限、代理路径可用但 Codex 专用环境尚未正确配置、以及 Git/npm 代理冲突等可安全自动处理的场景。

## 专项闭环

点击“Reconnecting 自愈”后，执行流程固定为：

```text
修复前扫描
→ 生成安全白名单计划
→ 必要时备份
→ 执行动作
→ 执行后验证
→ 验证失败则回滚
→ 必要时最后重启 Desktop
→ 独立 fresh rescan
→ 确定性终态分类
```

专项层不会绕过既有 RepairEngine。需要修改配置的动作仍使用备份、验证和回滚合同；如果计划中的动作因为扫描状态变化而无法安全构造，会留下明确失败审计，不会静默跳过并伪报成功。

## 允许自动执行的四个动作

V8.2.0 Reconnecting 专项只允许以下白名单 ID：

- `codex.proxy.env`：写入 Codex 专用 `.codex/.env` 代理配置；
- `git.proxy.clear`：清理已确认冲突的 Git 代理；
- `npm.proxy.clear`：清理已确认冲突的 npm 代理；
- `codex.desktop.restart`：使用扫描确认的真实 Desktop 身份和路径重启 Codex Desktop。

`codex.desktop.restart` 在需要配置变更时固定排在最后，避免重启后又继续修改连接配置。

## 代理安全边界

V8.2.0 明确固化以下规则：

- **直连正常时不写代理**；
- 候选代理必须实际通过 HTTPS 验证，未验证代理不会写入 `.codex/.env`；
- 默认不写 **Windows 用户级** `HTTP_PROXY` / `HTTPS_PROXY` 环境变量；
- `codex.proxy.env` 只修改 Codex 专用配置路径，并继续保留原有无关配置与备份；
- 写入后必须重新验证，验证失败执行对应回滚。

因此“发现本机有代理端口”本身不等于“允许写代理”。只有扫描证据确认直连不可用且已验证代理路径可用时，专项计划才允许生成代理写入动作。

## 终态分类

专项复检后不会只显示笼统的“成功/失败”。主要终态包括：

- `RECOVERED`：网络路径恢复且 Codex Desktop 正常运行，满足完整恢复条件；
- `NETWORK_RECOVERED`：网络路径已经恢复，但 Desktop 尚未达到完整运行条件；
- `ProxyFailed`：需要代理但代理写入/验证/回滚后仍未恢复；
- `DnsFailed`：DNS 仍未恢复；
- `TlsFailed`：DNS 正常但 TLS/网络路径仍失败；
- `DesktopRestartFailed`：网络路径已正常，但 Desktop 重启动作失败；
- `ManualRequired`：当前证据不足以安全自动处理，需要用户操作。

V8.2.0 不会把 `NETWORK_RECOVERED` 伪装成完整 `RECOVERED`。

## UI 与审计

主界面新增独立“Reconnecting 自愈”按钮，与原有“一键修复”分离：

- 执行前显示专项流程和安全边界；
- 执行中禁用冲突操作；
- 日志记录每个动作、状态、摘要和备份路径；
- 结束后显示真实终态；
- 最终触发主界面重新扫描刷新。

原有中文 / English、安装 / 卸载、智能迁移/恢复、启动/重启、通用一键修复能力全部保留。

## 完整报告升级

“导出完整报告”现在自动附带最近一次 Reconnecting 自愈证据，包括：

- 修复前 ScanId；
- 修复后 ScanId；
- 修复前网络诊断；
- 修复后网络诊断；
- 每个专项动作的结果；
- 验证/回滚证据；
- 最终终态与摘要。

报告版本升级为 `8.2.0`，继续把用户主目录标准化为 `%USERPROFILE%`，并对 Key、Token、Secret、Password、Cookie、Authorization、Session、Bearer 等敏感内容脱敏。

## V8.1.2 能力继续保留

V8.2.0 继续保留 V8.1.2 的：

- 标准 Windows `SaveFileDialog` 自由选择报告保存位置；
- Desktop 卸载前重新扫描真实客户端身份；
- 多进程 Desktop 窗口绑定；
- V8.1.1 中文 / English 双向切换；
- Desktop / CLI 安装 / 卸载管理；
- `.codex` 智能迁移/恢复；
- 管理员 UAC、真实路径启动/重启、单文件自包含发布和无运行时 PowerShell 依赖。

历史 V8.1.2 Release 工作流改为仅手动触发，并固定 checkout `v8.1.2` tag。V8.2.0 发布不会覆盖 V8.1.2 或更早版本的既有 Release 资产。

## 发布与验证

正式发布资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

构建目标：`.NET 8 / win-x64 / self-contained / single-file`。

V8.2.0 Release 门禁验证：

- V8.2.0 原生回归测试全部通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 项目版本固定为 `8.2.0`；
- 发布目录无 `.ps1`、`.psm1`、外置 `.dll`、`.runtimeconfig.json`、`.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`；
- 如果 `v8.2.0` Release 已存在，发布工作流明确跳过，不覆盖任何既有资产。
