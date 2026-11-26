# Architecture

## Process and sessions

The application is a C# 5 / WPF desktop executable targeting the x64 .NET Framework. Program registers an assembly resolver for one embedded managed KeePass assembly, refuses a nonembedded/wrong-version dependency selected by CLR probing, loads the theme and starts the window. The KeePass application entry point and plugin system are not invoked. Standard Windows APIs are used for local file identity; no separately supplied native helper DLL is required.

Each VaultSession owns a PwDatabase, baseline snapshot, expected file hash, revision counters, private checkpoint branch and local gate. With one file, both BrowserPane instances share that exact session and use independent paths/expanded cards. With two files, each has its own session; ownership is resolved from object ancestry rather than UUID alone, because copied databases can contain identical UUIDs. Same-file Windows path aliases are rejected before opening a second session.

Each BrowserPane also owns a transient search input and separate search expansion state. Literal all-term matching reads the current session's name/username/URL/notes fields and group paths, honors inherited search exclusions and omits enabled recycle-bin contents. Search never evaluates regex/placeholders or reads password/custom/history/attachment fields. A flat results column reuses entry editors and drag sources; it supplies no ambiguous drop target. Clearing restores the untouched browser path/expansion. Same-session rebuilds preserve queries; database changes reset the affected query. Detached inputs and lock/close clear query text with undo disabled, and queries never enter the storage model.

All live model changes are on the UI thread. Save/checkpoint snapshots are made before worker encryption. Explicit saves disable editing and serialize through one task. Lifecycle decisions block background interaction. Closing/locking during protection or cross-file commits is queued until data is protected or a clear failure state is shown.

Help is a separate owned window with text rendered from the embedded, hash-verified `docs/HELP.md`. Its small renderer supports topic headings, paragraphs, bullets, bold and inline code; it creates no active web links or HTML engine. F1 does not use or replace the operation's existing modal veil. Help navigation/copy controls do not reset vault inactivity. Reporting is fixed text plus the app assembly version, copied only on explicit actions through the existing clipboard service. It does not inspect the working session/environment/clipboard for report content, launch URLs, authenticate to GitHub or submit anything. See [REPORTING.md](REPORTING.md).

## Model and ordering

KeePass models retain the rich database. Five visible string fields are edited directly, with history backups before coalesced edits. Group/entry arrays carry native per-type order, while encrypted `VaultOrganizer.CardOrder` custom data carries mixed interleaving. Legacy identifiers remain compatible with 1.1. Before writing, ValidateTree rejects duplicate UUIDs, inconsistent parents, cycles and an invalid root.

## Save protocol

1. Snapshot and validate the working database/revision.
2. Acquire a short KeePass FileLock, compare current SHA-256 with the expected bytes, and preserve the first-session original.
3. Write a uniquely owned encrypted candidate, flush it, fully decrypt it and compare intended tree/history/field content, stored protection, cipher/KDF, custom metadata/icons and tombstones before publication. Creation, recovery copies and checkpoint components use the same content checks. Respect KeePass's standard-field protection normalization rather than treating it as corruption.
4. Recheck the shared file immediately before atomic replacement; capture its preimage under a unique encrypted filename.
5. Compare that preimage with the expected bytes. A verified expected preimage becomes rolling previous.kdbx. A last-instant ignoring writer becomes a unique concurrent archive for reconciliation. Unreadable or unarchivable versions retain a unique preimage file and block further publication in that session; subsequent sessions never reuse that filename.
6. Accept the written snapshot as the saved baseline. Revision comparison keeps later local work dirty. Two loaded files save sequentially; they are not one filesystem transaction.

When a hash changed, a baseline/local/external plan identifies compatible field edits, parent moves, presence/deletion and card-order conflicts. KeePass MergeIn preserves rich metadata/history/tombstones; explicit three-way patches reconcile the editable fields and order. A conflict decision is required for incompatible changes. Keep both clones the complete external entry with fresh identities. The external file's noneditable database settings are retained rather than silently weakened.

If both versions changed username/password/URL and their credential tuples differ, all three fields require review together. App/File selects that version's entire tuple; Keep both retains the other complete entry. Independent notes/custom strings still merge. This avoids synthesizing a credential combination that never existed in either version. It intentionally differs from KeePass's whole-entry synchronization; retaining metadata does not implement all upstream UI policies.

## Private recovery

A `.vdo-draft` is a ZIP container of encrypted KDBX documents: working, baseline and optional pending-file/pending-base. Authenticated encrypted role and shared nonce tags prevent mixing valid components from different roles/branches. New working ciphertext also binds the exact component inventory, preventing pending-review omission and rejecting duplicate/extra entries. Legacy markerless drafts require explicit compatibility review; their next checkpoint writes the new manifest. Raw key factors are not stored next to the bundle. An unchanged validated baseline ciphertext is reused, reducing KDF work. This is not an external freshness/anti-rollback authority.

Paths are SHA-256-derived from the absolute original database path plus a session identifier. Separate branches are not silently overwritten. Legacy `%LOCALAPPDATA%\VaultOrganizer\Drafts` and metadata names remain intentional compatibility details. Successful Save/discard removes only the active/resumed branch. Lock checkpoints final work, then releases all sessions and view/undo/key references. Failure retains working memory with an explicit incomplete state and recovery choices.

Each replaced draft keeps a `.previous` checkpoint. Discovery includes a backup whose primary is missing. A damaged primary fails authentication/readback normally; selecting its older checkpoint requires an explicit fallback decision. The fallback retains the damaged primary and uses a separate active branch. Both paths retain role/nonce/root validation. Lock requests during decryption are queued; requests during transfer/close conflict review cancel the pending decision and queue safe closure. Locking an empty session also clears any creation/open dialog secrets.

## Cross-file transfer

CrossDatabaseDrop requires an explicit user decision to publish. It first saves/reconciles both files, then reacquires source/target objects by UUID after any model replacement. CardTransfer deep-clones the selected entry/subtree, assigns fresh UUIDs including history, maps recognized internal UUID references, preserves mixed order and imports custom icons with fresh identifiers.

The destination is saved, reopened with its own factors, and compared with the transferred rich card and icons. Source removal requires that verification and an unchanged destination hash. Recognized boundary-crossing UUID references and source recycle/templates group selections are copy-only. Source deletion writes tombstones for the entire moved subtree. This final source save deliberately does not auto-merge a blind deletion: a conflicting source writer stops cleanup, restores the original working card and preserves competing versions for review.

If a uniquely preserved late source version cannot be opened/bound for pending review, publication is blocked for that session. Local work can still be checkpointed or saved to a new recovery filename; the competing archive needs its own correct factors.

There is no distributed atomic commit, network transaction journal or automatic retry/deduplication. A destination copy may exist even if a later step fails. The protocol prioritizes no source deletion before a verified durable copy; it cannot stop an independent remote writer from later deleting that copy. Failed destination saves retain staged/copied data and any merged external changes for review, instead of rolling back a whole database and losing the external edits.
