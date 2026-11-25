# KeePassHQ development

Read README.md, docs/BUILD.md, docs/ARCHITECTURE.md, SECURITY.md,
docs/AUDIT.md and CONTRIBUTING.md before changing application code.

- Use only fictional fixtures, isolated draft roots and the test clipboard.
  Never access real databases, actual key factors, account-protection files or
  the user's clipboard. Never automate an unlocked password manager.
- Keep the portable Windows x64 WPF application small, offline and focused.
- Preserve the KeePass 2.62 version/hash pin, GPL notices, Bootstrap MIT notice
  and corresponding upstream source material.
- Preserve intentional VaultOrganizer namespaces, draft paths and metadata keys.
- Run `powershell -NoProfile -File .\validate.ps1 -Render` for storage/lifecycle
  changes or release validation; inspect relevant fictional renders.
- Review staged files before publishing. Generated vaults, factors, drafts,
  settings and diagnostic output stay outside Git.

The permanent public repository is https://github.com/w3medialabs/keepasshq.
On the W3ML workstation, also follow the parent workspace's AGENTS.md and its
referenced ecosystem authority. Those machine-specific instructions are not
runtime dependencies or requirements for external contributors.
