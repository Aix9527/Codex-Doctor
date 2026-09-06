# Codex Doctor V8.1.1 双向语言与安装管理

**软件作者：Aix**

V8.1.1 在 V8.1.0 原生维修中心基础上，正式加入 **Codex Desktop 中文 / English 双向一键切换** 和 **Codex Desktop / CLI 安装 / 卸载管理**。核心原则不变：先扫描确认真实对象，再执行动作，动作后重新扫描验证；不能验证就不报告成功。

## 中文 / English 双向切换

- 主界面将旧“一键中文”升级为独立 `中文` 与 `English` 两个按钮。
- `中文` 的目标语言固定为 `zh-CN`；`English` 的目标语言固定为 `en-US`。
- 优先使用经过确认、可逆、明确属于 Desktop UI 的可信语言适配器。
- 没有可信本地适配器时，只对扫描确认的 Codex/ChatGPT Desktop 实际 EXE 与 PID 尝试 Windows UI Automation，并通过应用自身 `Settings → General → Language` 操作。
- 普通 `.codex/.env`、`config.toml` 中的 `language` / `locale` / `ui_language` 不会被猜测为 Desktop UI 设置。
- 不修改未知数据库、MSIX/AppX 资源或应用二进制文件。
- 语言动作结束后必须重新读取或重新扫描验证；无法确认目标语言时返回失败/需要用户操作，不伪报成功。

## Codex Desktop 安装 / 卸载

V8.1.1 的 Desktop 自动安装/卸载只使用 Microsoft Store / Windows Package Manager 官方包标识：

```text
9NT1R1C2HH7J
```

生产路径使用 `winget.exe`。如果系统没有可用的 winget，Codex Doctor 会返回 `ManualRequired`，不会下载第三方安装器或猜测来源。

安装完成后必须重新扫描确认 Desktop 已出现；卸载完成后必须重新扫描确认 Desktop 已消失。命令退出码为 0 不能单独作为成功依据。

## Codex CLI 安装 / 卸载

CLI 自动管理只使用 npm 官方包：

```text
@openai/codex
```

对应命令为：

```text
npm.cmd install -g @openai/codex
npm.cmd uninstall -g @openai/codex
```

如果本机没有可用的 `npm.cmd`，Codex Doctor 返回 `ManualRequired`。安装/卸载结束后同样执行重新扫描验证：只有 discovery 确认状态已经改变，才报告成功。

## 卸载保留范围

Desktop / CLI 卸载默认保留：

- `%USERPROFILE%\.codex`；
- 用户项目；
- Codex Doctor 备份；
- Codex Doctor 诊断报告。

安装管理窗口在卸载前会进行二次确认，并明确提示上述保留范围。

## V8.1.0 能力全部保留

V8.1.1 继续保留：

- Windows `requireAdministrator` / UAC 启动门；
- 一键扫描 Codex；
- 严重 / 紧急 / 警告 / 提示 / 正常多问题分级；
- RepairPlan 白名单一键修复；
- 执行后验证、失败回滚和完整自动复检；
- Desktop 真实 EXE/PID 启动与重启，不依赖 `chatgpt:` URL 协议；
- `.codex` 智能迁移/恢复；
- DNS / TLS / 代理 HTTPS / Git / npm / TUN 诊断；
- 隐私安全完整报告和敏感值脱敏；
- `codex doctor` 使用真实 CLI 路径执行。

## 发布与验证

正式发布继续采用真正的 Windows 原生单文件：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

构建目标为 `.NET 8 / win-x64 / self-contained / single-file`。Release 门禁验证：

- V8.1.1 原生回归测试通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 产品版本固定为 `8.1.1`；
- 发布目录无 `.ps1`、`.psm1`、外置 `.dll`、`.runtimeconfig.json`、`.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成并发布 SHA256 校验文件。

建议下载 `CodexDoctor.exe` 后同时校验 `CodexDoctor.exe.sha256`。
