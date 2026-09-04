# Codex Doctor V7 — Unified GUI

V7 combines V5 repair/migration features with V6 connectivity diagnosis.

## Main flow
1. Run `启动_Codex_Doctor_V7.bat`.
2. Click **一键诊断**.
3. Review health and failure class: `DNS`, `TLS`, `PROXY`, `ENV_CONFLICT`, or `HEALTHY`.
4. Use **修复建议项** only when the displayed plan contains a confirmed repair action.
5. Restart Codex / ChatGPT Desktop and diagnose again.

## Safety model
- Diagnosis is read-only.
- DNS/TLS/TUN findings are advisory; V7 does not auto-change certificates, DNS, antivirus HTTPS inspection, or TUN configuration.
- `.codex/.env`, Git proxy, npm proxy, user environment, restart, migration, and restore require explicit user actions.
- Existing `.env` is backed up before replacement.
- `.codex` migration keeps a backup and uses an NTFS Junction.

## CLI
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Codex-Doctor-V7.ps1 -Mode Diagnose -Json
```

Explicit proxy:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Codex-Doctor-V7.ps1 -Mode Diagnose -ProxyUrl http://127.0.0.1:7897 -Json
```

## GUI actions
- 一键诊断
- 修复建议项
- 重启 Codex
- 迁移 / 恢复 `.codex`
- 导出 JSON 报告
- 运行 `codex doctor`
- 高级手动清理 Git/npm 代理
- 可选写入 Windows 用户级代理环境变量（默认关闭）

## Requirements
- Windows 10/11
- Windows PowerShell 5.1+
- Optional: Codex CLI for `codex doctor`
