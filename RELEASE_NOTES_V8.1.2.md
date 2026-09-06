# Codex Doctor V8.1.2 Desktop 稳定性修复

**软件作者：Aix**

V8.1.2 是 V8.1.1 的稳定性修复版本，重点解决报告导出位置受限、Desktop 卸载身份固定、以及多进程 Desktop 环境下语言切换找不到真实窗口的问题。V8.1.1 的一键扫描、智能修复、迁移/恢复、中文 / English 双向切换和安装管理能力全部保留。

## 1. 导出完整报告可自由选择保存位置

- 点击“导出完整报告”后使用标准 Windows `SaveFileDialog`。
- 用户可以自由选择桌面、D 盘、其它磁盘或任意有写权限的目录。
- 默认目录仍为 `%LOCALAPPDATA%\CodexDoctorV8\reports`，便于不需要自定义位置的用户快速保存。
- 用户取消“另存为”时不会创建报告文件。
- 报告继续使用 UTF-8 JSON，并保留用户目录标准化和敏感值脱敏规则。

## 2. Desktop 卸载改为动态识别真实客户端

V8.1.2 不再把 Microsoft Store 安装 ID `9NT1R1C2HH7J` 当作所有已安装 Desktop 的固定卸载身份。

卸载流程现在是：

```text
重新扫描 Desktop
→ 确认实际 EXE
→ ChatGPT.exe => ChatGPT
  Codex.exe   => Codex
→ winget uninstall --name <已确认名称> --exact
→ 重新扫描验证 Desktop 已消失
```

安全边界保持不变：

- Desktop **安装**仍只使用官方 Microsoft Store / winget 包 ID `9NT1R1C2HH7J`；
- Desktop **卸载**只允许对扫描确认的 `ChatGPT.exe` / `Codex.exe` 映射到受限的 `ChatGPT` / `Codex` 精确名称；
- 不根据模糊名称删除未知程序；
- 不直接删除应用安装目录；
- 卸载完成后必须重新扫描确认，winget 退出码为 0 不能单独判定成功；
- `%USERPROFILE%\.codex`、用户项目、Codex Doctor 备份和诊断报告继续保留。

## 3. 中文 / English 支持多进程 Desktop 窗口绑定

此前部分 ChatGPT/Codex Desktop 使用多进程架构，扫描确认的主进程可能没有 `Process.MainWindowHandle`，导致应用明明已经打开，语言切换仍提示找不到可绑定窗口。

V8.1.2 新增可审计的多进程窗口定位器，通过 Windows 顶层窗口枚举后按以下顺序验证：

1. **扫描确认 PID**：窗口直接属于扫描得到的 Desktop PID；
2. **可信进程树**：窗口属于该 Desktop PID 的子进程；
3. **完全相同 EXE 路径**：多实例情况下，允许与扫描结果完全相同的已确认 EXE 路径作为安全回退。

不可见窗口、与扫描对象没有进程关系且 EXE 路径不同的窗口会被拒绝。语言自动化不会只靠窗口标题猜测，也不会为了切换语言修改未知数据库、普通 `.codex` 语言字段、MSIX/AppX 资源或应用二进制文件。

## V8.1.1 能力全部保留

V8.1.2 继续保留：

- Windows `requireAdministrator` / UAC 启动门；
- 一键扫描 Codex；
- 严重 / 紧急 / 警告 / 提示 / 正常多问题分级；
- RepairPlan 白名单一键修复；
- 执行后验证、失败回滚和完整自动复检；
- Desktop 真实 EXE/PID 启动与重启，不依赖 `chatgpt:` URL 协议；
- `.codex` 智能迁移/恢复；
- DNS / TLS / 代理 HTTPS / Git / npm / TUN 诊断；
- `中文` / `English` 双向语言切换并要求操作后验证；
- Codex Desktop / CLI 安装与卸载管理；
- `codex doctor` 使用真实 CLI 路径执行；
- 隐私安全完整报告和敏感值脱敏。

## 发布与验证

正式发布资产固定为：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

构建目标继续为 `.NET 8 / win-x64 / self-contained / single-file`。V8.1.2 Release 门禁验证：

- V8.1.2 原生回归测试通过；
- `CodexDoctor.exe` 具有 PE `MZ` 文件头；
- manifest 保持 `requireAdministrator`；
- 产品版本固定为 `8.1.2`；
- 发布目录无 `.ps1`、`.psm1`、外置 `.dll`、`.runtimeconfig.json`、`.deps.json`；
- C# 运行时代码不依赖 `powershell.exe` / `pwsh.exe`；
- 生成 `CodexDoctor.exe.sha256`；
- 如果 `v8.1.2` Release 已存在，工作流会跳过，不覆盖既有资产。

建议下载 `CodexDoctor.exe` 后同时校验 `CodexDoctor.exe.sha256`。
