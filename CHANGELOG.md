# Changelog

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
