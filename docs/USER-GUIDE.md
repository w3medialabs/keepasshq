# User guide

## Open or create

Obtain the portable archive from [GitHub Releases](https://github.com/w3medialabs/keepasshq/releases), when available, or [build the source](BUILD.md). Extract the release to a folder you can write to and run KeePassHQ.exe. Browse to a `.kdbx`, enter the master password and any key factors required by that database, then unlock. To use a key-file-only database, clear **Use master password** and select the key file. Windows account protection requires the correct Windows account and its intact protected key material. Plugin key providers/ciphers are unsupported.

**Create new database** opens a filename/key dialog. Enter and confirm a nonempty password, or choose supported nonpassword factors. The app can use an existing key file; it does not generate one. Store all required factors independently. Choose an unused `.kdbx` filename. A newly created database is empty; add your own groups and entries.

Keep a key file separate from the database; obtaining both removes its possession-factor benefit. Back up required factors securely. Windows account protection depends on that account's intact protected material and is not made portable by copying the executable or recreating the same login name. See [KeePass master-key guidance](https://keepass.info/help/base/keys.html).

Portable packages may include a fictional Demo.kdbx with password `demo`. The source checkout contains no database files; diagnostics generate disposable fictional fixtures locally.

On the open screen and open/create dialogs, use the eye button inside a master-password input to show or hide it. Showing is explicit; hiding removes the temporary visible copy. Enter submits either representation, including the creation confirmation. Disabling the password factor, closing its dialog, locking, closing the application or completing decryption clears/hides both representations. Reveal does not copy anything to the clipboard. If you explicitly copy a revealed factor, app-owned clipboard cleanup also runs on exit; another application's replacement is retained. Entry password fields remain visible by design.

## Organize one database

KeePassHQ starts in a centered, resizable window. The centered header switch selects Viewer or Organizer; hover its icons for their names. The right-side actions are Undo, Save, optional Recovery copy, Close, Lock, and Settings at the far right. Styled tooltips describe each action. Panel headings show the database filename, record count and full path. Folder icons identify groups and key icons identify entries; active groups remain green.

Viewer shows one card browser. Organizer shows two equal browsers of the same session. No Old/New folders are created. Each pane starts at the root and navigates independently. Clicking a group opens the next column to its right. Selecting a different group at that level replaces deeper columns.

Use **+** at a column heading to create a group or entry there. Right-click a group header to rename it. Click an entry title to expand the five fields; click again to collapse. Fields edit the working model immediately. A matching expanded entry in the other browser updates immediately too.

To extract credentials from notes, create a new entry, select the desired text in the original notes, and use the integrated Cut/Copy/Paste actions to fill fields. A text action uses the selection, or the whole field if no text is selected. Paste inserts/replaces at the cursor. There is no automatic parsing or flagging.

Drag a card by its title. A green widening gap receives it at that position. Drop on a group to make it a child; hover briefly to open the group first. Drop in a column's empty background to append. The first column is the root, so dropping there makes a top-level child. Dragging works both ways between panes and within the same pane. A group cannot enter its own descendants.

**Ctrl+Z** undoes a recent model change. Same-database changes remain unpublished until **Save/Ctrl+S**. Custom mixed group/entry ordering is encrypted in group metadata; other clients can use their own sorting.

## Work with two databases

In Organizer, open **Database ▾** on either pane and choose **Open database** or **Create database**. The chosen pane becomes that file's browser; the other keeps its current database. The controls and drag behavior stay the same. If two different files are already open, the app closes the selected pane's file safely before opening the replacement.

A different master password or key file is supported on each side. The filenames appear under the pane headings and in the main header. Viewer hides the second pane but does not close its database; Save and Lock still cover both loaded files.

A cross-file drag asks for **Cancel**, **Save & copy**, or **Save & move**. Both transfer actions first save/reconcile all pending edits in both files. Copy adds and saves a full clone with fresh UUIDs, leaving the source. Move also reopens/verifies that destination copy before deleting and saving the original. Subtrees transfer with their rich entry data and supported internal UUID field references remapped.

Move is disabled for recognized UUID links crossing the selected boundary and for selections containing the source recycle/templates group. Copy is available, but references to records left outside the selection may not resolve in the other file. Name-based references and opaque plugin metadata require manual review.

Cross-file transfers clear local undo. They are not an atomic transaction across two files: an interruption may leave duplicates. If the destination saves but source cleanup fails, review both databases before retrying. The original is restored/retained in memory where possible, competing versions are kept, and the app reports the incomplete transfer. Do not assume a failed move means no destination copy exists.

## Save, conflicts and sync

Save publishes every loaded database sequentially. Each session first checks the bytes last loaded/saved. Compatible external changes are brought in using the common baseline; conflicting edits require **Use app edits**, **Use file edits**, or **Keep both entries**, when applicable. Keep both preserves full entries with a fresh UUID for the file copy. Changed/unknown key factors or a different root identity block automatic merging.

When both versions changed credentials, username, password and URL are selected together. For example, an app-side username edit and a file-side password edit require review; the app does not silently combine them. Use app/file selects that whole credential bundle, while independent notes/custom fields are still kept. Keep both retains both actual credential versions. See the [fictional conflict preview](images/conflict.png).

The app holds a local `.organizer-lock` sidecar gate while a file is open and a short standard KeePass file lock while replacing it. This gate coordinates KeePassHQ windows on this machine, not every application or remote device. SHA-256 checks and post-replacement preimage checks preserve late competing writers. No app can detect OneDrive edits that have not reached the local file. Let sync finish before opening and after saving; use a local materialized file rather than an unavailable online-only placeholder.

Beside each database is a `<database>.versions` directory. It contains a byte-preserving first-session backup and a rolling `previous.kdbx`, plus uniquely named concurrent versions when needed. Replacement first captures the prior file as a unique `preimage-*.kdbx`; if verification cannot finish or the app crashes, that file remains independently preserved. Further saves in a session with an unverified competing version are blocked. Keep all encrypted versions private, save a recovery copy of your working edits, and review the retained versions before continuing. Backups are retained; the app does not prune them automatically. Encrypted temporary files may remain after a hard crash; do not delete anything until your saved database and recovery copies are verified.

## Drafts, lock and close

Every 15/30 seconds when edits exist, a private encrypted bundle protects working data and the baseline. Pending competing versions are included. This does **not** publish incomplete work to the shared database. A top-center reminder encourages an explicit Save; a stronger reminder appears after prolonged editing.

For compatibility with Vault Organizer 1.1, drafts remain under `%LOCALAPPDATA%\VaultOrganizer\Drafts` with `.vdo-draft` names and authenticated role/nonce metadata. KeePassHQ retains these legacy names deliberately. Settings are in `KeePassHQ.settings` beside the executable; if that folder is unwritable, choices apply only for the session. Drafts are tied to the database's original absolute path and key factors. Moving/renaming the database will not automatically find drafts under its old path hash. Keep the original path available for recovery.

On unlock, an existing draft must be resumed/reconciled, explicitly discarded, or kept by canceling opening. Separate session branches are retained independently; resuming the most recent branch does not silently erase older ones. Inspect older recovery branches before deleting them.

New checkpoints authenticate the complete list of encrypted components. An older-format checkpoint asks for a separate compatibility review because it cannot prove that optional pending-review files were not removed. Resume only a trusted local copy, inspect its records, and keep original backups; the next checkpoint uses the stronger format. Retained complete older checkpoints are not proof of freshness.

A previous checkpoint is discoverable if its primary draft is missing. If the latest draft is damaged or unreadable and a previous checkpoint exists, choose **Cancel opening**, **Retry latest**, or **Resume previous**. Previous checkpoints can omit newer edits; review the recovered data before saving. The damaged primary is retained separately even after the older work is saved, so a later opening may still offer that retained branch. Discard it only after confirming recovery is complete.

**Lock/Ctrl+L**, inactivity timeout, and Windows session lock protect final edits, release loaded files and discard UI/model/key references. Reopen to decrypt again. **Close** in the header closes all loaded databases and returns to the open screen; unsaved work offers Save or Keep draft. Native window close also supports Cancel. A locked window closes without asking for a password.

Lock requested during decryption is queued. Lock during transfer or database-close conflict review cancels the unresolved decision and closes safely after the active operation stops. Locking before a database is loaded clears the password fields in an open creation dialog. Save reminders distinguish verified checkpoints from latest edits that are not yet protected.

From one pane's Database menu, **Close database** closes just that file if two distinct files are loaded, then both panes browse the remaining file. **Use other pane's database** performs the same safe close. Closing the last file returns to the open screen.

If the final checkpoint fails, the app states **Lock incomplete**. The file gate is released, but unsaved working data remains in memory until you retry, save a recovery copy elsewhere, or deliberately discard it. A successful lock is not claimed while this data remains. Recovery copies use new filenames and never overwrite an existing database; other loaded dirty sessions are checkpointed before closing. The recovery action in each pane selects that pane's database; the header recovery action selects the first database.

## Password generator

Use the key action beside a password field. Choose 8–128 characters and desired upper/lower/digit/symbol categories. Changes regenerate the result. Cancel leaves the field unchanged; Copy copies and closes for use elsewhere; Use fills the calling field without touching the clipboard.

Passwords remain visible while expanded. Explicit buttons and keyboard/context copies, including generator and conflict text, request exclusion from Windows clipboard history/cloud sync and share matching-content cleanup after about 30 seconds or on lock. Cleanup preserves different replacement text and retries while busy. Other software can ignore exclusion formats; identical replacement text and a change between the clipboard read and clear cannot be distinguished reliably. Existing history/cloud copies are not erased.

## Troubleshooting

- **Could not unlock:** check every key factor, local file availability and supported cipher/key-provider requirements. Do not replace or regenerate an existing account/key file to work around a failed unlock.
- **Already open:** close the other KeePassHQ window. Gate files are released on normal lock/close and process termination.
- **Save failed:** edits remain available. Retry after filesystem/sync activity settles, or save a recovery copy to a writable location. Review any preserved competing version.
- **Unknown external key/root:** automatic merge stops. Keep encrypted copies of both versions for reconciliation in their respective clients.
- **Large/expensive vault:** encryption cost is the database's KDF settings. Checkpoints avoid repeating an unchanged baseline's KDF, but must still encrypt and verify the current working database.
- **Unsigned executable:** this community build is not Authenticode-signed. Obtain it from a trusted release or build the supplied source; an unsigned wrapper is not authenticated by the signature on its embedded dependency.
- **Embedded dependency required:** keep KeePassHQ in its own trusted portable folder. Remove or relocate a neighboring KeePass.exe/KeePass.dll to its own folder; startup refuses it rather than using a different dependency.
