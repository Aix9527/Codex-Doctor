# Codex Doctor V7 Unified GUI Design

## Goal
Unify V5 repair/migration UX with V6 connectivity diagnosis into one Windows PowerShell + WinForms application.

## Architecture
V7 uses three layers:
1. `lib/Diagnosis.psm1` — network diagnosis and failure classification (reusing V6 behavior).
2. `lib/RepairPlan.psm1` — pure decision layer that converts a diagnosis result into safe repair actions.
3. `Codex-Doctor-V7.ps1` — WinForms UI and orchestration only.

## Safety
- Diagnosis is read-only.
- Writing `~/.codex/.env`, clearing Git/npm proxy values, writing Windows user environment variables, restarting Codex, migrating `.codex`, and restoring `.codex` are explicit user actions.
- DNS/TLS/TUN findings are advisory only; V7 does not automatically change certificates, DNS, antivirus HTTPS inspection, or TUN settings.
- Existing `.env` is backed up before changes.
- `.codex` migration preserves backup and uses an NTFS Junction.

## Health model
Statuses: `Healthy`, `Warning`, `Error`, `Unknown`.
Checks: DNS, TLS, HTTP proxy, TUN, Git proxy, npm proxy, `.env`, Codex process.
Primary failure classes: `DNS`, `TLS`, `PROXY`, `ENV_CONFLICT`, `HEALTHY`.

## Repair plan mapping
- `DNS` -> advisory only; no automatic repair.
- `TLS` -> advisory only; no automatic repair.
- `PROXY` -> detect a validated local HTTP-compatible proxy and offer to write `.codex/.env`; Windows user env write is optional.
- `ENV_CONFLICT` -> show conflicting Git/npm proxy values and offer separate confirmed cleanup actions.
- `HEALTHY` -> no repair; offer restart/retest only.

## GUI
Dashboard shows overall state and cards for DNS, TLS, Proxy, TUN, Git/npm, `.env`, and Codex process. Main actions: Diagnose, Repair Recommended, Restart Codex, Migrate `.codex`, Restore `.codex`, Export Report. Advanced actions expose `.env` write, optional user environment write, Git/npm cleanup, and `codex doctor`.

## Testing
- Pure RepairPlan behavior tests.
- Health/status model tests.
- Existing V6 diagnosis tests remain green.
- Windows GitHub Actions validates `.ps1` and `.psm1` syntax, runs V6/V7 tests, and verifies V1-V7 release packages.

## Packaging
Source lives under `versions/v7/`; workflow creates `Codex-Doctor-V7.zip`. README recommends V7 while retaining V5/V6 as stable fallback paths.
