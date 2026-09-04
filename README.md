# Codex Doctor

Windows Codex / ChatGPT Desktop 连接诊断、代理自动修复与 `.codex` 数据迁移工具。

## Versions

| Version | Package | Highlights |
|---|---|---|
| V1 | `releases/Codex-Doctor-V1.zip` | 基础代理端口扫描，自动写入 `~/.codex/.env` |
| V2 | `releases/Codex-Doctor-V2.zip` | 诊断、代理修复、`.codex` D/E 盘迁移与回滚 |
| V3 | `releases/Codex-Doctor-V3.zip` | Clash Verge / Mihomo 配置识别 + 实际 HTTPS 代理测试 |
| V4 | `releases/Codex-Doctor-V4-GUI.zip` | Windows WinForms GUI，一键诊断/修复/迁移/恢复 |
| V5 | `releases/Codex-Doctor-V5-Installer.zip` | 安装版、健康状态灯、快捷方式、重启、报告导出、EXE 构建辅助 |

## Recommended

建议新用户直接使用 **V5**。解压 `releases/Codex-Doctor-V5-Installer.zip` 后运行 `安装_Codex_Doctor_V5.bat`。

## Main capabilities

- 检测 Codex / ChatGPT Desktop 常见 `Reconnecting` 连接问题。
- 检测 Windows 系统代理与常见 Clash/Mihomo 本地代理端口。
- 自动创建或修改 `%USERPROFILE%\.codex\.env`。
- 写入 `HTTP_PROXY` / `HTTPS_PROXY` 及对应小写变量。
- 修改前自动备份配置。
- 支持把 `%USERPROFILE%\.codex` 迁移到 D/E 盘并建立 NTFS Junction。
- 支持恢复原目录。
- V3+ 支持代理 HTTPS 实测；V4+ 提供 GUI；V5 提供安装/卸载和诊断报告。

> 安全策略：不直接搬移或修改 WindowsApps/MSIX 应用包本体，迁移主要针对 Codex 用户数据目录。
