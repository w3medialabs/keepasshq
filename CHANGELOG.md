# Changelog

## Unreleased

- Final adversarial review against KeePass/Microsoft guidance: require whole username/password/URL bundle review for concurrent credential edits; preserve independent notes and Keep-both versions.
- Compare intended content against encrypted save/create/recovery/checkpoint readback, including history, protected fields, cipher/KDF, custom data/icons and tombstones; reject valid-but-stale candidates.
- Authenticate new draft component inventories, reject missing/ambiguous components, and require explicit compatibility review for older drafts while preserving legacy paths and identifiers.
- Cover generator/conflict/normal text copies with clipboard cleanup and Windows history/cloud exclusion formats. Block publication after an unreadable changed-key source version interrupts a transfer.
- Refuse neighboring KeePass assemblies at startup; add negative EXE/DLL shadow checks, bounded synthetic UI waits, 385 assertions and a sixteenth credential-conflict preview. Enable and verify GitHub private vulnerability reporting.
- Keep browser columns at a steady width when entries expand; shrink and tighten input actions. Replace hand-drawn control/card symbols with locally embedded Bootstrap Icons 1.13.1 SVGs, preserving original geometry, source and MIT attribution.
- UI completeness follow-up: restore Enter submission and editor focus for revealed passwords, clear empty-session factors/app-owned clipboard on exit, add option help, and keep entry help/accessibility labels current after renaming.
- Verify custom scrollbar thumb/page routing and real dropdown option selection; expand synthetic validation to 350 assertions and fifteen rendered previews, including the actual dropdown popup template.
- Add `package.ps1` for a repeatable local portable deployment built from an isolated commit archive, with optional verified fictional demo, corresponding source, licenses, validation summaries and payload/checksum verification.
- Correct extracted-source inventory filtering so an ignored parent directory cannot hide nested source/documentation files from validation.
- Refreshed the WPF interface: normal centered startup, centered view switch, icon-only toolbar, far-right Settings, styled tooltips/menus/dropdowns, blue notifications and labeled file pickers.
- Added explicit password reveal to open/create inputs, with synchronization and clearing on hide, disable, close, lock and decryption; expanded synthetic validation to 338 assertions.
- Made browsers more compact with white bordered panels/columns, folder/key icons, centered captions, narrow scrollbars and full database paths beneath record counts.
- Adversarial follow-up: retain unique encrypted replacement preimages before verification, so failures or crashes cannot expose an unverified competing version to rolling-backup overwrite.
- Discover previous checkpoints when their primary draft is missing; offer explicit older-checkpoint recovery for damaged latest drafts without removing the damaged file.
- Queue Lock during decryption, cancel pending transfer/close conflict decisions on Lock, and clear creation-dialog passwords when no database is loaded.
- Report actual checkpoint protection state in save reminders and require isolated draft defaults in diagnostics.
- Add adversarial regressions (329 total assertions), recovery/reminder renders, and automated source inventory, link, secret-pattern, provenance and branding checks.
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
