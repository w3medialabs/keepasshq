# Architecture

## Process and sessions

The application is a C# 5 / WPF desktop executable targeting the x64 .NET Framework. Program registers an assembly resolver for one embedded managed KeePass assembly, loads the theme and starts the window. The KeePass application entry point and plugin system are not invoked. Standard Windows APIs are used for local file identity; no separately supplied native helper DLL is required.

Each VaultSession owns a PwDatabase, baseline snapshot, expected file hash, revision counters, private checkpoint branch and local gate. With one file, both BrowserPane instances share that exact session and use independent paths/expanded cards. With two files, each has its own session; ownership is resolved from object ancestry rather than UUID alone, because copied databases can contain identical UUIDs. Same-file Windows path aliases are rejected before opening a second session.

All live model changes are on the UI thread. Save/checkpoint snapshots are made before worker encryption. Explicit saves disable editing and serialize through one task. Lifecycle decisions block background interaction. Closing/locking during protection or cross-file commits is queued until data is protected or a clear failure state is shown.

## Model and ordering

KeePass models retain the rich database. Five visible string fields are edited directly, with history backups before coalesced edits. Group/entry arrays carry native per-type order, while encrypted `VaultOrganizer.CardOrder` custom data carries mixed interleaving. Legacy identifiers remain compatible with 1.1. Before writing, ValidateTree rejects duplicate UUIDs, inconsistent parents, cycles and an invalid root.

## Save protocol

1. Snapshot and validate the working database/revision.
2. Acquire a short KeePass FileLock, compare current SHA-256 with the expected bytes, and preserve the first-session original.
3. Write a uniquely owned encrypted candidate, flush it and fully decrypt it for verification.
4. Recheck the shared file immediately before atomic replacement; retain the preimage as previous.kdbx.
5. Compare that preimage with the expected bytes. A last-instant ignoring writer is retained as a unique concurrent archive and reconciled; unknown versions block further publication.
6. Accept the written snapshot as the saved baseline. Revision comparison keeps later local work dirty. Two loaded files save sequentially; they are not one filesystem transaction.

When a hash changed, a baseline/local/external plan identifies compatible field edits, parent moves, presence/deletion and card-order conflicts. KeePass MergeIn preserves rich metadata/history/tombstones; explicit three-way patches reconcile the editable fields and order. A conflict decision is required for incompatible changes. Keep both clones the complete external entry with fresh identities. The external file's noneditable database settings are retained rather than silently weakened.

## Private recovery

A `.vdo-draft` is a ZIP container of encrypted KDBX documents: working, baseline and optional pending-file/pending-base. Authenticated encrypted role and shared nonce tags prevent mixing valid components from different roles/branches. Raw key factors are not stored next to the bundle. An unchanged validated baseline ciphertext is reused, reducing KDF work.

Paths are SHA-256-derived from the absolute original database path plus a session identifier. Separate branches are not silently overwritten. Legacy `%LOCALAPPDATA%\VaultOrganizer\Drafts` and metadata names remain intentional compatibility details. Successful Save/discard removes only the active/resumed branch. Lock checkpoints final work, then releases all sessions and view/undo/key references. Failure retains working memory with an explicit incomplete state and recovery choices.

## Cross-file transfer

CrossDatabaseDrop requires an explicit user decision to publish. It first saves/reconciles both files, then reacquires source/target objects by UUID after any model replacement. CardTransfer deep-clones the selected entry/subtree, assigns fresh UUIDs including history, maps recognized internal UUID references, preserves mixed order and imports custom icons with fresh identifiers.

The destination is saved, reopened with its own factors, and compared with the transferred rich card and icons. Source removal requires that verification and an unchanged destination hash. Recognized boundary-crossing UUID references and source recycle/templates group selections are copy-only. Source deletion writes tombstones for the entire moved subtree. This final source save deliberately does not auto-merge a blind deletion: a conflicting source writer stops cleanup, restores the original working card and preserves competing versions for review.

There is no distributed atomic commit, network transaction journal or automatic retry/deduplication. A destination copy may exist even if a later step fails. The protocol prioritizes no source deletion before a verified durable copy; it cannot stop an independent remote writer from later deleting that copy. Failed destination saves retain staged/copied data and any merged external changes for review, instead of rolling back a whole database and losing the external edits.
