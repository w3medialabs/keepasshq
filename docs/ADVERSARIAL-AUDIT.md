# Adversarial completeness audit

Reviewed November 25, 2026 on the development workstation, starting at source commit `37edbe9540f58aceebd6beb7d741b4291828e078`. Scope: all application source, synthetic tests, build/validation scripts, workflow, public documentation, templates, dependency provenance and release boundaries. This is a development review, not independent security certification or a parser fuzzing campaign. No real databases, actual key factors, account-protection files, user clipboard or unlocked password-manager UI were accessed.

## Findings and resolutions

| Finding | Severity | Resolution and evidence |
| --- | --- | --- |
| An unreadable replacement preimage could remain in the rolling backup slot and be overwritten by a later save | High, data preservation | Capture each preimage under a unique encrypted filename before verification. Promote only verified expected bytes to the rolling slot. Block the affected session on verification failure. Fault injection holds the preimage unreadable, checks the late writer's content, blocks retry, verifies recovery-copy behavior and confirms a later session preserves that unique file. |
| A valid previous checkpoint was ignored when its primary draft was missing | Medium, recovery | Include backup-only checkpoints in discovery and clearly identify their older status. A regression first failed against the original implementation, then passed with authenticated restoration and branch-specific cleanup. |
| A damaged latest draft offered no way to use its valid previous checkpoint | Medium, recovery | Require an explicit older-checkpoint choice; retain authentication, role/nonce/root checks and the damaged primary. Model and in-process UI regressions verify rejection of the damaged latest bundle and explicit fallback without silent substitution. |
| Lock could be dropped during decryption or wait indefinitely on a transfer/close conflict decision; creation-dialog passwords survived Lock with no loaded file | Medium, lifecycle | Queue lock through opening, cancel unresolved review during transfer/close, and clear empty-session dialogs/passwords. Synthetic regressions verify each transition. |
| Strong save reminder claimed protection even when the latest revision had no successful checkpoint | Medium, correctness | Derive wording from each dirty session's checkpoint revision. Test both unprotected and protected states; inspect synthetic reminder render. |
| Diagnostic UI creation could use the normal default draft directory before test-specific reconfiguration | Low, test isolation | Require an isolated default in diagnostics. Test and render entry points set it; release metadata checks prove the diagnostic field is excluded. |
| Source inventory, links, source-mirror integrity and copied branding depended only on manual review | Low, maintenance | Add `scripts/check-source.ps1` to local and manual CI validation, plus additional ignore rules. Support both Git checkouts and extracted source archives. Pattern scanning supplements manual review. |

All actionable findings above are resolved in the current source. No TODO/FIXME markers, unfinished exception stubs, generic repository URLs or placeholder implementations were found in first-party code. The user guide's online-only file “placeholder” is a filesystem term, and standard license examples remain intact. Unfilled issue/PR template prompts are intentional forms. Legacy `VaultOrganizer` namespaces, draft paths and metadata identifiers are intentional compatibility requirements.

## Validation

- Release and diagnostics build with the Windows Framework64 compiler and no compiler warnings.
- **329 synthetic assertions pass**, expanding the prior 306 by 23 regression assertions. Tests use fictional fixtures, an isolated clipboard and isolated default draft roots.
- Release checks exclude test classes, render/CLI modes, isolated defaults and all fault hooks, including the new post-replacement fault hook.
- Nine fictional WPF previews render. The new recovery choice and unprotected-edit reminder were visually inspected.
- Source inventory, documentation links, secret/personal-path patterns, upstream source-mirror hash and runtime branding checks pass.
- A clean extracted source archive builds and validates from a directory containing spaces; exact wrapper hashes vary with compiler timestamps.

See [VALIDATION.txt](VALIDATION.txt) for run hashes and historical package evidence. Manual Windows CI remains configured; the previously recorded GitHub dispatch/private-reporting service failures are external limitations, not passing hosted-run evidence. No binary release or tag was published by this audit.

## Dependency and licensing review

KeePass 2.62 is preserved with the exact existing assembly hash, corresponding source mirror, GPL notices and upstream acknowledgements. Bootstrap's MIT icon notice and geometry are retained. The current [official 2.62 release notes](https://keepass.info/news/n261104_2.62.html), [integrity page](https://keepass.info/integrity.html) and [upstream security-issue analysis](https://keepass.info/help/kb/sec_issues.html) were consulted. The documented official portable/source archive hashes match the published values; the included mirror is separately identified and hash-checked. No dependency upgrade or unsupported claim of an exhaustive vulnerability assessment was made.

## Remaining boundaries

The executable is unsigned and has no independent certification. Actual pointer gestures, live sync-provider propagation, real account-key factors, every DPI/Windows/filesystem combination, and hostile-file fuzzing remain outside this synthetic review. Cross-file commits remain sequential and interruptions may leave duplicates. File-provider failures, compromised operating systems and unsynchronized remote writers remain outside local locking guarantees. Previous checkpoints can omit newer work; retained damaged branches and encrypted preimages require review. Managed memory, clipboard history and pagefiles have no forensic erasure guarantee. Deliberately excluded integrations/editors are listed in [FEATURES.md](../FEATURES.md); they are not hidden unfinished work.
