# Codex Doctor

Windows Codex / ChatGPT Desktop 连接诊断、代理修复与 `.codex` 数据迁移工具。

## 推荐版本

当前推荐 **V5 Installer Edition**。它提供 GUI 健康状态、Clash Verge / Mihomo 代理检测、Reconnecting 修复、`.codex` D/E 盘迁移与恢复、诊断报告、桌面快捷方式和可选 EXE 构建脚本。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本

## Releases

历史 ZIP 包保存在 `releases/`。

## 快速使用

下载 V5 后运行 `安装_Codex_Doctor_V5.bat`。首次建议先执行“一键诊断”，再按结果修复 `Reconnecting`。

> 修改 `%USERPROFILE%\.codex\.env` 后，需要彻底退出并重新打开 Codex / ChatGPT Desktop。

## 安全原则

工具不会直接搬动 WindowsApps/MSIX 应用包本体；迁移只针对 `%USERPROFILE%\.codex`，并保留备份与恢复路径。

## License

MIT
