# Codex Doctor V7.1.1 Hotfix Release Notes

V7.1.1 修复 V7.1 在部分 Windows 10/11 + Windows PowerShell 5.1 环境中的两个启动级问题。

## 修复

- 修复 `CodexDoctorV7.exe` 经 ps2exe 编译后 `$MyInvocation.MyCommand.Path` 可能为空，导致 `Split-Path` 报错“无法将参数绑定到参数 Path，因为该参数是空值”。
- Launcher 改为优先使用 `[System.AppContext]::BaseDirectory`，并以当前进程 `MainModule.FileName` 作为后备路径来源。
- `Codex-Doctor-V7.ps1` 改为 UTF-8 BOM，避免 Windows PowerShell 5.1 在中文系统区域中将 UTF-8 中文 UI 文本误按 ANSI/GBK 解码而出现 `Unexpected token` ParserError。
- EXE 元数据版本更新为 `7.1.1.0`。
- 新增 V7.1.1 兼容性回归测试，固定验证 Launcher 路径策略和 UTF-8 BOM。

## 安装建议

请删除或覆盖旧 V7.1 安装目录后重新安装 V7.1.1。推荐直接下载完整 `Codex-Doctor-V7.1.1-Windows.zip`，不要只复制 EXE，因为 EXE 仍需要同目录的 PowerShell 主程序与 `lib/` 模块。

## 安全模型

本热修复不改变 V7 的诊断与修复权限模型：诊断默认只读；`.codex/.env`、Git/npm 代理清理、Windows 用户环境变量和 `.codex` 迁移仍要求显式操作或确认。
