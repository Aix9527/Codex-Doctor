# Changelog

## V8 Native
- 使用 C# + .NET 8 + WinForms 原生重写，不再通过 PowerShell 启动器运行主程序
- 正式目标为 `win-x64`、self-contained、single-file 的 `CodexDoctor.exe`
- 用户无需安装 .NET，运行时不依赖 `.ps1` / `.psm1`
- GUI、弹窗、日志、诊断建议全部中文化
- DNS、直接 TLS、显式代理 HTTPS、TUN、Git/npm 代理冲突统一诊断
- 新增 `PROXY_REQUIRED` / “需要配置代理”语义：直连 TLS 失败但已验证本地代理可用且 `.codex/.env` 缺失时，不再误报 TLS 故障
- 真实 2026-09-05 诊断样本已加入回归测试
- `.codex/.env` 修复保留无关配置并自动备份旧文件
- Windows 用户环境变量写入默认关闭
- 保留 Codex 重启、`.codex` 迁移/恢复、报告导出、`codex doctor`、Git/npm 清理
- Windows CI 实际执行 `dotnet publish`，验证 PE 文件头、单文件发布、无 PowerShell 运行依赖并生成 SHA256

## V7.1.2 中文兼容版
- 保留 V7.1.2 PowerShell 实现作为历史/兼容版本
- GUI 标题、按钮、弹窗、日志、错误提示、诊断建议中文化
- 导出报告改为中文字段和中文状态说明
- 保留 UTF-8 BOM / Windows PowerShell 5.1 兼容修复
- 保留 Git/npm 单元素代理结果 `.Count` 回归修复

## V7.1
- Release Edition based on V7 Unified GUI
- Reproducible Windows EXE launcher build using pinned `ps2exe 1.0.18`
- Portable `Codex-Doctor-V7.1-Windows.zip` packaging
- SHA256 checksum generation for release verification
- Current-user installer and uninstaller scripts
- EXE launcher keeps PowerShell modules external and auditable instead of hiding runtime logic
- Windows CI validates generated EXE has a PE `MZ` header before packaging

## V7
- Unified WinForms GUI combining V5 repair/migration and V6 diagnosis
- RepairPlan decision engine separates diagnosis from mutation
- Health model with `Healthy`, `Warning`, and `Error`
- Confirmed `.codex/.env` proxy repair with backup preservation
- Optional Windows user proxy environment write (default off)
- Explicit Git/npm proxy cleanup actions
- `.codex` migration/restore with Junction safety checks
- CLI diagnosis mode with JSON output
- Windows CI runs V6 and V7 tests and validates `.ps1`/`.psm1`

## V6
- DNS resolution checks for `chatgpt.com` and `api.openai.com`
- TLS handshake diagnosis for `chatgpt.com:443`
- Explicit HTTP proxy route validation
- Clash/Mihomo/sing-box process and TUN adapter detection
- Git global proxy mismatch detection
- npm proxy mismatch detection
- Deterministic failure classes: `DNS`, `TLS`, `PROXY`, `ENV_CONFLICT`, `HEALTHY`
- Read-only diagnosis mode with JSON output for automation

## V5
- Installer Edition
- GUI health indicator
- Admin privilege detection
- Restart Codex / ChatGPT Desktop
- Export diagnostics report
- Desktop and Start Menu shortcuts
- Uninstaller
- Optional PS2EXE build helper

## V4
- WinForms GUI
- One-click diagnosis and repair
- GUI migration / restore

## V3
- Clash Verge / Mihomo config discovery
- `mixed-port`, `port`, `socks-port` parsing
- Real HTTPS proxy validation

## V2
- `.codex` migration to another drive using NTFS Junction
- Migration state and rollback
- Proxy diagnostics

## V1
- Common local proxy port scan
- `.codex/.env` creation and backup
- HTTP(S)_PROXY user environment variables
