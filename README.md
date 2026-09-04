# Codex Doctor

Windows Codex / ChatGPT Desktop 连接诊断、代理修复与 `.codex` 数据迁移工具。

## 推荐版本

当前推荐 **V7 Unified GUI**：把 V6 的 DNS/TLS/代理/TUN/Git/npm 诊断与 V5 的 `.codex/.env` 修复、迁移/恢复、重启、报告功能合并到同一个 Windows GUI。

V7 使用“先诊断、后确认修复”的安全流程。DNS/TLS/TUN 只给出诊断与建议，不自动修改系统证书、DNS、杀毒软件 HTTPS 检查或 TUN 配置。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类
- `versions/v7` — 统一诊断 + RepairPlan + 安全修复 + WinForms GUI + CLI JSON

## V7 使用

双击：

```text
versions\v7\启动_Codex_Doctor_V7.bat
```

推荐流程：

1. 点击“一键诊断”。
2. 查看 `DNS / TLS / PROXY / ENV_CONFLICT / HEALTHY` 原因分类。
3. 仅当 V7 给出可执行修复计划时，点击“修复建议项”。
4. 重启 Codex / ChatGPT Desktop。
5. 再次诊断确认链路状态。

CLI JSON：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\versions\v7\Codex-Doctor-V7.ps1 -Mode Diagnose -Json
```

## 稳定回退

- 只想做链路诊断：使用 V6。
- 只想使用旧版 GUI 修复/迁移：使用 V5。

## Releases

历史 ZIP 包保存在 `releases/`。GitHub Actions 也会从 `versions/` 自动生成 V1–V7 构建产物。

> 修改 `%USERPROFILE%\.codex\.env` 后，需要彻底退出并重新打开 Codex / ChatGPT Desktop。

## 安全原则

工具不会直接搬动 WindowsApps/MSIX 应用包本体；迁移只针对 `%USERPROFILE%\.codex`，并保留备份与恢复路径。Git/npm 全局代理清理、Windows 用户环境变量写入和 `.codex` 迁移均要求显式操作。

## License

MIT
