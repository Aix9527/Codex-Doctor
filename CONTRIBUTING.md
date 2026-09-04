# Contributing

Thanks for improving Codex Doctor.

## Development rules

1. Keep Windows PowerShell 5.1 compatibility unless a version explicitly requires newer PowerShell.
2. Never delete a user's `.codex` data as part of migration. Back up first and preserve rollback state.
3. Do not modify WindowsApps/MSIX application packages directly.
4. Preserve unrelated lines in `.codex/.env` when updating proxy variables.
5. Prefer actual HTTP proxy validation over merely detecting a listening TCP port.
6. Keep version-specific behavior under `versions/vN` and update `CHANGELOG.md` for user-visible changes.

## Pull requests

Please include:
- Windows version tested
- Proxy client tested, if relevant
- Reproduction steps
- Before/after behavior
- Rollback verification for migration changes
