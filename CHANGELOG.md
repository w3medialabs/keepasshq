# Changelog

## Unreleased

- Established [w3medialabs/keepasshq](https://github.com/w3medialabs/keepasshq) as the permanent public source repository from the prepared 1.2 source package.
- Added repository-specific installation, contribution, security and release links; clarified source-only fixture behavior and recovery documentation.
- Added `validate.ps1` for release/diagnostics builds, release boundary and embedded dependency checks, synthetic tests and seven optional renders.
- Fixed relative build output/dependency paths to follow PowerShell's current directory, including validation from an extracted source directory with spaces.
- Aligned Windows CI with manual execution and limited uploaded artifacts to fictional previews and validation summaries.

## 1.2 — 2026-11-25

- Renamed to KeePassHQ; adopted the Bootstrap house-lock icon.
- Added safe new-database creation with confirmed key factors and no-overwrite publication.
- Added Close database / Close all and return to the open screen.
- Added optional separate database sessions per Organizer pane, retaining identical browser behavior and same-file defaults.
- Added deliberate destination-first cross-database copy/move with rich cloning, fresh identities, reference/order/icon mapping and interruption-safe source retention.
- Extended source review and synthetic validation; added public repository documentation, license notices, issue/PR templates and Windows CI.

## 1.1 — 2026-11-25 (Vault Organizer)

Viewer/Organizer modes, same-root independent browsers, live mirrored fields, manual publication with encrypted private checkpoints, reminders, active-group highlight, receiving gaps, password generator, lifecycle/concurrency/recovery hardening, verified KeePass 2.62 dependency and synthetic audit suite.

## 1.0 — Initial prototype (Vault Organizer)

Portable card-based organizer for nested groups, drag/drop, five-field editing and manual notes cleanup. Superseded automatic Old/New container behavior is not used in 1.2.
