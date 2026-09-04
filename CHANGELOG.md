# Changelog

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
