# Codex Doctor

Windows Codex / ChatGPT Desktop 连接诊断、代理修复与 `.codex` 数据迁移工具。

## 推荐版本

当前推荐组合：

- **V6 Connectivity Diagnosis**：先定位 `Reconnecting` 的真实原因（DNS / TLS / 代理 / TUN / Git/npm 环境冲突）。
- **V5 Installer Edition**：确认需要修复后，用 GUI 执行 `.codex/.env` 代理修复、迁移、恢复和重启。

## 版本目录

- `versions/v1` — 初代代理端口扫描与 `.env` 写入
- `versions/v2` — 代理修复 + `.codex` 迁移/回滚
- `versions/v3` — Clash/Mihomo 配置识别 + 实际 HTTPS 代理测试
- `versions/v4` — WinForms GUI
- `versions/v5` — 安装版 GUI、健康灯、重启、报告、卸载与 EXE 构建脚本
- `versions/v6` — DNS/TLS/代理/TUN/Git/npm 连接链路诊断与故障分类

## V6 故障分类

V6 会把诊断结果归为：

- `DNS` — `chatgpt.com` / `api.openai.com` 域名解析失败
- `TLS` — DNS 正常，但 TLS 握手失败
- `PROXY` — TLS 正常，但显式 HTTP 代理链路不可用或没有代理配置
- `ENV_CONFLICT` — Codex 代理可用，但 Git/npm 存在不一致的代理配置
- `HEALTHY` — 当前检查链路通过

V6 默认只诊断，不自动清理 Git/npm、证书或 TUN 配置。

## Releases

历史 ZIP 包保存在 `releases/`。GitHub Actions 也会从 `versions/` 自动生成构建产物。

## 快速使用

先运行 V6：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\versions\v6\Codex-Doctor-V6.ps1
```

如果结果为 `PROXY` 或需要重新写入 `.codex/.env`，再使用 V5 GUI 执行修复。

> 修改 `%USERPROFILE%\.codex\.env` 后，需要彻底退出并重新打开 Codex / ChatGPT Desktop。

## 安全原则

工具不会直接搬动 WindowsApps/MSIX 应用包本体；迁移只针对 `%USERPROFILE%\.codex`，并保留备份与恢复路径。

## License

MIT
