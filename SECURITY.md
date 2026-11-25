# Security policy

KeePassHQ 1.2 is an unsigned community desktop editor. A development review and synthetic test suite are included; it has not received an independent security certification or full penetration test. Report problems against the current source/release.

## Reporting

Use [GitHub private vulnerability reporting](https://github.com/w3medialabs/keepasshq/security/advisories/new) for confidential reports. If GitHub says reporting is unavailable, [open an issue requesting a confidential channel](https://github.com/w3medialabs/keepasshq/issues/new), without exploit details or sensitive data. Maintainers must verify private reporting is enabled before a public binary release. No response-time guarantee or maintainer email is invented by this repository.

Version 1.2 is the current development line; older prototypes are superseded. Fixes are provided through this repository and its [releases](https://github.com/w3medialabs/keepasshq/releases), with no automatic updater or promised support period.

Never attach a real database, master password, key file, account-protection file, recovery bundle, or screenshot of actual secrets to a public issue. Reproduce with generated fictional data and describe the affected version/steps. Regular bugs can use the public issue template.

## Trust boundary

- Decryption and editing happen in this process on the local computer. The application contains no networking, telemetry, AI integration, account backend, update checker, plugin loader, URL launcher, or command execution feature. Building requires no network access with the bundled verified dependency.
- A user-selected database in OneDrive or another synchronized folder remains subject to that provider's synchronization. Windows, backup software, malware or another privileged process can independently access files/process memory.
- KeePass 2.62 performs the KDBX cryptography. New databases use AES-256 with Argon2id (64 MiB, 3 passes, parallelism 2); existing supported crypto settings are preserved.
- Startup refuses a neighboring KeePass assembly in place of the embedded dependency. Keep the portable executable in its own trusted folder. This check does not protect a compromised installation or operating system.
- Expanded passwords and notes are intentionally visible and editable. Clipboard access is explicit or a normal field copy/cut operation. Copies request Windows history/cloud exclusion, but other software may ignore these formats. Matching-content cleanup is best-effort and cannot distinguish identical replacement text or make read/clear atomic. Screenshots, memory dumps, paging and forensic RAM erasure are outside the clearing guarantee. WPF input does not provide KeePass secure-desktop/control parity.
- Successful lock/close drops UI controls, undo/model/key references and file gates after protecting final work. A checkpoint failure is explicitly **Lock incomplete**, with working data retained until retry/recovery/explicit discard. Managed strings cannot be guaranteed physically erased.
- SHA-256 validation, short local file locks, encrypted readback, backups and late-writer capture protect ordinary local concurrent-save scenarios. Remote edits that have not reached this computer cannot be observed. Cloud synchronization is not a distributed transaction or lock.
- Two-file transfers are destination-first, not atomic. Copies can remain after interruption. Cross-file Save & move publishes pending changes in both databases only after an explicit in-app decision. Source removal is blocked without a verified saved destination.
- Encrypted drafts and version backups contain complete sensitive data and should be protected like the original vault. Filenames/path hashes reveal some metadata; recovery bundles do not store raw passwords or key-file contents alongside them.
- Plugin-provided factors/ciphers and opaque plugin reference conventions are not supported. Known changed master keys or different root identities stop automatic reconciliation; preserved encrypted versions need their own correct factors.
- Concurrent nonidentical username/password/URL changes require review as a complete credential bundle. Independent notes/custom fields can still merge. This is a deliberate wrapper policy, not identical KeePass synchronization semantics.
- New draft ciphertext authenticates its component inventory. Legacy drafts require an explicit compatibility review because they cannot retroactively prove that optional review components were not removed. No external anti-rollback/freshness authority is provided.
- Preserving database metadata does not enforce every KeePass UI/application policy, including forced master-key-change schedules. KeePass's own standard-field protection normalization on load/save remains applicable. Hostile-file/KDF resource exhaustion and parser fuzzing were not covered by this review.

For current findings, primary-source comparisons, testing limits and executed checks, see [the final adversarial review](docs/FINAL-AUDIT.md) and [original audit](docs/AUDIT.md). Community releases should be reviewed, tested with disposable copies and ideally signed by their publisher; the verified signature on KeePass.exe does not sign KeePassHQ.exe.
