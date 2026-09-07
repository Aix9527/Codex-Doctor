# Codex Doctor V8.2.0 — Reconnecting 端到端自愈

**软件作者：Aix**

V8.2.0 在 V8.1.2 原生维修中心上新增面向 **Codex / ChatGPT Desktop 一直 Reconnecting** 的专项闭环自愈。核心变化不是“多一个重启按钮”，而是把恢复成功标准升级为：**修复后网络路径通过 fresh 验证，同时扫描确认的 Desktop 也通过 fresh discovery / 运行验证，二者同时成立才允许报告 `RECOVERED`。**

## 1. Reconnecting 自愈

主界面新增：

```text
Reconnecting 自愈
```

执行流程固定为：

```text
fresh 全量扫描
→ 选择可验证网络路径
→ 生成安全 RepairPlan
→ 备份 / 修复 / 验证 / 必要时回滚
→ 使用扫描确认的真实 EXE/PID 重启 Desktop
→ fresh Desktop discovery
→ fresh 全量网络诊断
→ 生成结构化恢复结论
```

没有可信 Desktop、存在严重/紧急的 `ManualRequired` / `ExternalRequired` 安全门、或网络事实不足时，工具会停止自动动作并返回人工处理状态。

## 2. 不猜代理，只采用已验证路径

V8.2.0 复用既有 `DiagnosisService` 的事实，不新增“常见端口猜测”逻辑：

- 直接 TLS 已验证健康：保持直连，不为了统一配置强制写代理；
- 直连失败，但本地代理 HTTPS 已验证通过：只允许使用 `DiagnosisResult.ProxyUrl` 中的已验证代理；
- 代理未通过 HTTPS 验证：不得写入 `.codex/.env`；
- 没有任何可验证路径：按 DNS / TLS / Proxy 事实返回失败，不伪造成功。

`.codex/.env` 继续沿用安全规则：保留无关变量、修改前备份、只写一致的 `HTTP_PROXY` / `HTTPS_PROXY`、写入后验证、失败回滚。默认仍不写 Windows 用户级 HTTP/HTTPS 代理环境变量。

## 3. 双重恢复验证

V8.2.0 的成功判定不再等价于“进程存在”。

最终状态包括：

- `RECOVERED`：网络与 Desktop 双验证通过；
- `NETWORK_RECOVERED`：网络已恢复，但 Desktop 未通过 fresh 验证；
- `PROXY_FAILED`：代理路径/代理修复未通过验证；
- `DNS_FAILED`：DNS 失败且没有其它可验证路径；
- `TLS_FAILED`：DNS 正常，但 TLS 失败且无可验证代理；
- `DESKTOP_RESTART_FAILED`：网络可用，但真实 Desktop 重启失败；
- `MANUAL_REQUIRED`：安全门或事实不足，不允许自动处理。

任何未验证状态都不会被显示为 `RECOVERED`。

## 4. 完整报告增加恢复证据

“导出完整报告”继续允许用户自由选择保存位置，并新增 `Reconnecting自愈` 区段，记录：

- 修复前 ScanId；
- 修复后 ScanId；
- 最终结构化状态；
- 网络是否验证；
- Desktop 是否验证；
- 选择的代理路径；
- 修复动作、验证/回滚结果；
- 中文恢复摘要。

报告继续执行用户目录标准化与 Key / Token / Secret / Password / Cookie / Auth / Session / Bearer 等敏感值脱敏。

## 5. V8.1.2 能力全部保留

V8.2.0 继续保留：

- Windows `requireAdministrator` / UAC 启动门；
- 一键扫描与严重 / 紧急 / 警告 / 提示 / 正常问题分级；
- RepairPlan 白名单、执行后验证和失败回滚；
- Desktop 真实 EXE/PID 启动与重启；
- `.codex` 智能迁移/恢复；
- DNS / TLS / 代理 HTTPS / Git / npm / TUN 诊断；
- 中文 / English 双向界面切换；
- Codex Desktop / CLI 安装与卸载管理；
- 报告另存为任意有写权限位置；
- Desktop 动态卸载身份；
- 多进程 Desktop 窗口安全绑定；
- 无运行时 PowerShell 依赖的 C#/.NET 8 原生单文件交付。

V8.1.2 Release 工作流已冻结为历史手动复现，并固定 checkout `v8.1.2` tag，避免 V8.2.0 合并后使用新源码误跑旧版本门禁。

## 发布与验证

正式资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

构建目标：`.NET 8 / win-x64 / self-contained / single-file`。

V8.2.0 Release 门禁要求：

- V8.2.0 原生回归测试全部通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 产品版本固定为 `8.2.0`；
- 发布目录无 `.ps1`、`.psm1`、外置 `.dll`、`.runtimeconfig.json`、`.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`；
- 如果 `v8.2.0` Release 已存在，工作流明确跳过且不覆盖任何既有资产。
