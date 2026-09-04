# Security Policy

Codex Doctor changes local proxy environment variables and may migrate the user's `.codex` directory. Treat these operations as security-sensitive.

## Reporting a vulnerability

Please report security issues privately to the repository owner rather than opening a public issue with secrets, tokens, private paths, or diagnostic logs.

## Sensitive data

Before sharing reports, remove:
- API keys and tokens
- account identifiers
- private repository URLs
- private proxy credentials
- personally identifying local paths when unnecessary

Codex Doctor should never intentionally collect or upload credentials.
