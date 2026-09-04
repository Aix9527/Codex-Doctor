# Codex Doctor V6 — Connectivity Diagnosis

V6 focuses on finding the actual layer behind repeated `Reconnecting` symptoms instead of immediately rewriting proxy settings.

## Diagnosis layers

- DNS resolution for `chatgpt.com` and `api.openai.com`
- TLS handshake to `chatgpt.com:443`
- Explicit HTTP proxy route validation
- Clash/Mihomo/sing-box process and TUN adapter detection
- Git global `http.proxy` / `https.proxy` mismatch detection
- npm `proxy` / `https-proxy` mismatch detection
- Deterministic failure classification: `DNS`, `TLS`, `PROXY`, `ENV_CONFLICT`, `HEALTHY`

## Run

Double-click `运行_Codex_Doctor_V6.bat` or run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Codex-Doctor-V6.ps1
```

JSON output for automation:

```powershell
.\Codex-Doctor-V6.ps1 -Json
```

Override proxy:

```powershell
.\Codex-Doctor-V6.ps1 -ProxyUrl http://127.0.0.1:7897
```

## Safety

V6 diagnosis is read-only. It does not automatically clear Git/npm proxy settings, change Windows TUN configuration, or rewrite certificates. Use V5 when you specifically want the `.codex/.env` proxy repair and migration UI.
