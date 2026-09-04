# Codex Doctor V7 Unified GUI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a unified V7 Windows GUI that diagnoses Codex connectivity, converts findings into safe repair plans, performs confirmed repairs, preserves V5 migration/recovery features, and packages V7 with CI coverage.

**Architecture:** Keep diagnosis, repair planning, repair execution, and GUI orchestration separated. Reuse V6 diagnosis semantics. Pure decision functions are tested independently before GUI wiring.

**Tech Stack:** Windows PowerShell 5.1+, PowerShell modules (`.psm1`), WinForms, GitHub Actions Windows runner.

**Spec:** `docs/superpowers/specs/2026-09-05-v7-unified-gui-design.md`

## Global Constraints
- Diagnosis is read-only.
- DNS/TLS/TUN are advisory-only automatic repair domains.
- Mutations require explicit user actions.
- Existing `.env` must be backed up before replacement.
- Keep V6 diagnosis tests green.
- V7 must run on Windows PowerShell 5.1 syntax.

---

### Task 1: RepairPlan decision engine
**Files:** Create `versions/v7/tests/RepairPlan.Tests.ps1`; create `versions/v7/lib/RepairPlan.psm1`.
**Interfaces:** `New-CodexRepairPlan -FailureClass <string> -GitConflict <bool> -NpmConflict <bool> -ProxyAvailable <bool>` returns an object with `FailureClass`, `Automatic`, `ConfirmRequired`, `Actions`, `Advisory`.
- [ ] Write failing tests for DNS/TLS advisory-only, PROXY env-write plan, ENV_CONFLICT confirmed cleanup, HEALTHY no-op.
- [ ] Run tests and verify failure because module/function is missing.
- [ ] Implement minimal decision engine.
- [ ] Run tests and verify pass.
- [ ] Commit.

### Task 2: Health/status model
**Files:** Create `versions/v7/tests/HealthModel.Tests.ps1`; create `versions/v7/lib/HealthModel.psm1`.
**Interfaces:** `Get-CodexOverallHealth -DnsOk -TlsOk -ProxyOk -EnvConflict -EnvPresent` returns `Healthy|Warning|Error`.
- [ ] Write failing precedence tests.
- [ ] Verify red.
- [ ] Implement minimal model.
- [ ] Verify green.
- [ ] Commit.

### Task 3: Repair execution utilities
**Files:** Create `versions/v7/lib/RepairActions.psm1`; create `versions/v7/tests/RepairActions.Tests.ps1`.
**Interfaces:** `Set-CodexProxyEnvFile`, `Clear-GitProxyConfig`, `Clear-NpmProxyConfig`, `Get-CodexEnvProxy`.
- [ ] Write file-system-safe env tests using a temp directory; do not mutate real user config in tests.
- [ ] Verify red.
- [ ] Implement backup-preserving `.env` mutation and command wrappers.
- [ ] Verify green.
- [ ] Commit.

### Task 4: Unified CLI orchestration
**Files:** Create `versions/v7/Codex-Doctor-V7.ps1`; copy/adapt V6 diagnosis module into `versions/v7/lib/Diagnosis.psm1`.
**Interfaces:** `-Mode Diagnose|Gui`, `-ProxyUrl`, `-Json`.
- [ ] Add orchestration smoke test for module loading and JSON diagnosis envelope.
- [ ] Verify red.
- [ ] Wire diagnosis + health + repair plan output.
- [ ] Verify green.
- [ ] Commit.

### Task 5: WinForms GUI
**Files:** Modify `versions/v7/Codex-Doctor-V7.ps1`; create `versions/v7/启动_Codex_Doctor_V7.bat`.
**Interfaces:** Dashboard cards and explicit buttons: Diagnose, Repair Recommended, Restart, Migrate, Restore, Export Report; advanced confirmed actions for env/Git/npm.
- [ ] Add parser/smoke checks ensuring the GUI entrypoint loads modules without executing mutations.
- [ ] Verify red if entrypoint is incomplete.
- [ ] Implement GUI and confirmed mutation handlers.
- [ ] Verify syntax and smoke checks.
- [ ] Commit.

### Task 6: Docs, packaging, CI
**Files:** Create `versions/v7/README.md`; modify root `README.md`, `CHANGELOG.md`, `.github/workflows/package.yml`, `.github/workflows/validate.yml`; add `releases/Codex-Doctor-V7.zip` when packaged.
- [ ] Extend CI to run V7 tests and validate `.ps1`/`.psm1`.
- [ ] Extend packaging loop to V7.
- [ ] Update docs recommending V7 and preserving V5/V6 fallback guidance.
- [ ] Run Windows GitHub Actions and require all validation steps green.
- [ ] Open PR, review diff, merge only after green CI.
