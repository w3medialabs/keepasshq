# Design choices

KeePassHQ is a small, offline workspace for organizing existing KeePass data.
Its [architecture](ARCHITECTURE.md) implements the following choices; the
[feature list](../FEATURES.md) defines the supported surface and limits.

## One browser model

Viewer presents one card browser and Organizer presents two instances of the
same browser. Both support live editing. Independent navigation makes it easier
to clean up long notes and move records between nested groups without inventing
special source/destination folders. Shared sessions mirror changes immediately;
separate files retain independent models and keys.

## Private work and explicit publication

Editing updates the working model. Manual Save publishes it to the database,
while encrypted private checkpoints protect unfinished work between saves.
This lets users decide when a shared or synchronized file receives their edits.
Reminders keep that distinction visible. Checkpoint failure retains working data
and requires a clear recovery decision instead of claiming a successful lock.

## Preserve rich data and competing writers

The five visible fields are a focused editor over the complete KeePass model.
Hidden fields, history, attachments and supported metadata remain part of that
model. Mixed card order uses encrypted custom metadata with legacy identifiers
retained for compatibility.

File hashes, verified encrypted candidates, preimage backups and three-way review
protect against ordinary local concurrent edits. Unknown key changes and root
identities stop automatic reconciliation. Preserving a competing encrypted copy
is preferable to silently discarding a writer. Sync providers remain independent;
local checks cannot see remote edits before those edits arrive.

## Transfer durability before removal

A cross-file transfer explicitly publishes pending changes. The destination is
saved and reopened for verification before a move removes the source. Two files
cannot be committed atomically by this protocol, so interruption may leave
duplicates that require review. Keeping both copies is preferable to removing the
only durable record. Opaque plugin reference conventions remain outside the
application's interpretation boundary.

## A narrow runtime

Windows WPF and an embedded, pinned KeePass managed assembly provide the UI and
database implementation without an installer, package restore, web runtime or
service. Additional integrations must justify their complexity and security
boundary. The unsigned wrapper and development review provide no independent
security certification; see [SECURITY.md](../SECURITY.md).
