# Development security and completeness review

Reviewed November 25, 2026 for KeePassHQ 1.2. Scope: application source, database lifecycle, file publication/recovery, merge/transfer protocol, crypto usage, clipboard/generator boundary, dependency integrity and synthetic WPF rendering. This is an implementation review by the development agent, not an independent certification, formal proof, upstream cryptographic audit, penetration test or parser fuzzing campaign.

No real vault, real master key, real key file, Windows account-protection file or user clipboard was accessed. Tests generated fictional KDBX files and isolated key/draft/clipboard services. Native password-manager automation was not used.

## Corrections verified

| Area | Risk addressed | Resolution |
| --- | --- | --- |
| Locked exit | Reentrant Closing could trap the window or require another unlock | Close directly when locked; defer active workflows and queue final exit |
| Recovery/create races | Cleanup could overwrite/delete another writer's new file | Uniquely owned encrypted temporary file, readback, non-overwriting move; cleanup touches only the owned temp |
| Publication | Timed saves could publish unfinished edits | Manual shared Save; encrypted private checkpoints and increasingly visible reminders |
| Final write race | Hash checks alone miss an ignoring writer immediately before replacement | Compare preserved preimage after replacement; archive competing version and carry pending review through encrypted drafts |
| Draft authenticity | Valid ciphertext components could be swapped/mispaired | Authenticated role/nonce tags, root validation, separate session branches and final readback |
| Structural loss | Reorders/moves or five-field editing could lose rich data | Full KeePass models, tree validator, encrypted mixed order and clone-based metadata retention |
| Dual ownership | A second pane could edit/save the first database by accident | Session bound per pane; ancestry-based card ownership; independent keys/checkpoints and same-file identity checks |
| Cross-file deletion | Source removed before destination durability | Explicit Save & move; destination save, full readback/content comparison and hash check before source tombstones |
| Failed transfer rollback | Restoring a whole destination snapshot could erase external edits just merged | Keep staged destination copy and merged state for review; retain source; no destructive whole-model rollback after async merge |
| Cross-file source race | An automatic merge could propagate an unreviewed deletion | Final source save stops on conflict; restore original working content, preserve late writer, checkpoint recovery |
| Links/settings | Moves could break recognized external UUID refs or source special folders | Block those moves, offer Copy; remap recognized internal UUID refs/history/order/icons |
| Modal/async lifecycle | Background shortcuts, closing or locking could invalidate an operation | Disable interaction, cancel superseded decisions, queue closure through in-flight commits; re-enable after normal unlock |
| Clipboard | Cleanup could erase another app's text or retain extra plaintext | Ownership digest, timeout/lock cleanup, busy retries; isolated test clipboard |
| Timers | Wall-clock changes could extend inactivity | Monotonic elapsed counters for inactivity/checkpoints/reminders/clipboard |
| Dependency | Old or substituted crypto library | Pinned KeePass 2.62 version and exact SHA-256; valid upstream Authenticode signature checked |
| Release surface | Test hooks/data or generated diagnostics could ship | Conditional diagnostics excluded; release type/resource/metadata checks; explicit packaging inventory |

## Executed evidence

The delivered build passed **306 synthetic assertions**. Coverage includes AES/AES-KDF and ChaCha20/Argon2id, password/key-file combinations, rich fields/history/attachments/icons/settings, ordering/cycles, compatible/conflicting external edits, deletion choices, final-window writers, recovery role tampering, separate branches, partial-write rejection, candidate publication races, generator category/boundary rules, UI mirroring/modals/reminders/timeout/close, creation through the UI, separate master passwords, independent drafts and destination-first transfer failures.

Release/source builds use the verified embedded dependency. Source rebuild, release diagnostics exclusion and package inventory checks are recorded with hashes in [VALIDATION.txt](VALIDATION.txt). The permanent repository adds a repeatable `validate.ps1` command and was revalidated after migration; see that record for the distinction between original package checks and repository checks. Synthetic renders cover Viewer, Organizer, generator, open/create and dual-file layouts. The logo uses the official Bootstrap house-lock geometry, not a generated database mark.

## Remaining limits

- Tests cover in-process controls, model behavior and synthetic renders. Actual pointer gestures, real OneDrive propagation, Windows clipboard history, real account-key factors and every filesystem/Windows version were not exercised.
- A malicious/compromised OS, another process with access, unsynchronized remote writes and later external deletions are outside a local app's locking guarantee. File/provider failures can prevent durability.
- Cross-file saves are sequential; crash recovery may leave duplicates and source deletion may need manual review. No distributed atomicity/deduplication is claimed.
- Opaque plugin metadata and non-UUID reference conventions are preserved but not interpreted. Copying a subset does not promise all external references resolve in a different vault.
- Passwords are intentionally visible; managed memory, clipboard history, paging and crash dumps are not forensically scrubbed.
- Unknown changed key factors/root identity stop automatic merge. Encrypted backups/drafts may require original factors and the original path for discovery.
- The wrapper is unsigned and has no independent audit. Review and trusted distribution remain necessary for public adoption.

See [SECURITY.md](../SECURITY.md), [source provenance](../THIRD-PARTY.md), and [release checklist](RELEASING.md).
