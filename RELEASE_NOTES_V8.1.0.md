# Codex Doctor V8.1.0 原生维修中心

**软件作者：Aix**

V8.1.0 将 Codex Doctor 从“单项诊断/修复工具”升级为 **先扫描、问题分级、事务式一键修复、自动复检** 的 Windows 原生维修中心。

## 核心升级

- **强制管理员启动**：程序通过 Windows manifest 请求管理员权限；未通过 UAC 不进入主界面。
- **一键扫描 Codex**：统一只读扫描 Desktop / CLI、`.codex`、Junction/迁移状态、代理环境、Git/npm、DNS、TLS、代理 HTTPS、Clash/Mihomo/sing-box、TUN、语言与启动路径等健康项。
- **多问题并存与分级**：结果按“严重 → 紧急 → 警告 → 提示 → 正常”排序，不再只显示单一故障。
- **一键修复**：只执行白名单内、可验证的安全修复；需要备份的动作先备份，执行后验证，失败则回滚。
- **自动复检**：一键修复完成后自动执行完整健康扫描，用修复前/后的 ScanId 形成可审计证据链。
- **真实路径启动/重启**：自动识别本机 Codex / ChatGPT Desktop 实际 EXE 和进程，优先使用扫描结果启动或重启，不依赖 `chatgpt:` URL 协议。
- **智能迁移/恢复**：识别普通 `.codex`、有效 Junction、可恢复中断迁移和歧义状态；歧义状态只显示详情，不冒险自动覆盖或删除。
- **一键中文**：仅在存在经过确认、可逆的可信 Desktop UI 语言适配器时自动设置简体中文；不修改未知数据库、MSIX/AppX 或二进制资源，也不会伪报成功。
- **导出完整报告**：导出 V8.1.0 中文 JSON 健康报告，包含扫描、RepairPlan、修复/验证/回滚和修复前后扫描结果；用户主目录标准化为 `%USERPROFILE%`，Key/Token/Secret/Password/Cookie/Auth/Session 等敏感值脱敏。

## 主界面流程

```text
UAC 管理员授权
→ 一键扫描
→ 问题分级
→ 一键修复
→ 自动复检
```

扫描完成后提供六个主要操作：

1. 启动 Codex
2. 重启 Codex
3. 一键修复
4. 智能迁移/恢复
5. 一键中文
6. 导出完整报告

## 安全边界

- 扫描阶段默认只读，不修改注册表、PATH、用户配置或系统设置。
- `ManualRequired` / `ExternalRequired` 不会被伪报为“已修复”。
- 默认一键修复不会写 Windows 用户级 HTTP/HTTPS 代理环境变量。
- 有写入风险的修复必须先确认；需要备份的动作必须先备份。
- 自动修复动作必须具备执行后验证；验证失败执行回滚。
- 报告仅记录 `软件作者=Aix`，不写入 QQ 或抖音联系方式。

## 发布形式

V8.1.0 继续保持真正的 Windows 原生单文件交付：

```text
CodexDoctor.exe
CodexDoctor.exe.sha256
```

构建目标为 `.NET 8 / win-x64 / self-contained / single-file`。用户无需另外安装 .NET，运行时代码不依赖 `powershell.exe`、`pwsh.exe`、`.ps1` 或 `.psm1`，发布目录不携带外置运行 DLL。

建议下载后同时校验 `CodexDoctor.exe.sha256`，确认 EXE 完整性后再运行。
