# Features and boundaries

## Databases

- Open local/synced `.kdbx` files using a master password, existing key file, Windows account key, or supported combination.
- Create a new empty database with password confirmation and optional existing key factors. No automatic Old/New groups. Creation never overwrites another file.
- New files use AES-256 and Argon2id: 64 MiB memory, three passes, parallelism two. Existing files retain their supported cipher/KDF settings.
- Close all databases to return to the open screen. Close one of two through its pane menu; the remaining database becomes the shared browser target.
- Runtime is a single portable executable, with settings beside the executable and encrypted recovery drafts in local application data.

## Browsing and editing

- Viewer has one pane. Organizer has equal left/right panes with the same controls and behavior.
- Both start at the same root. Navigation is independent; the underlying session is shared until another database is opened.
- A search bar at the top of each browser filters that database independently. Case-insensitive literal words match names, usernames, URLs, notes and group paths; every word must match. Password fields, hidden fields/history/attachments, enabled recycle-bin contents and search-disabled groups are excluded.
- Open/edit matching cards, drag them to a normal browser destination, or use their group link to return to their folder. Clear/Escape restores prior browsing; Ctrl+F focuses the active browser's search. Queries stay private to the current session and clear on lock/close.
- Groups and entries appear together as title-only cards. Clicking a group adds its child column. Clicking an entry expands it in place without changing the column width.
- Live name, username, password, URL and notes editing; visible passwords by design. Text actions are integrated into inputs. Notes support selection and extraction into manually created entries.
- Create entries/groups at any visible level; rename a group through its context menu.
- Green active groups, receiving gaps, hover-to-open groups and edge scrolling during drag.
- Move and reorder in either direction, including within a column; invalid cycles/root moves are rejected.
- Up to 40 local undo actions, with nearby field edits coalesced. Cross-database committed transfers clear undo.
- Light theme, centered resizable window at startup, blue top-center notifications and shortcut actions.
- Centered one/two-pane view switch, icon-only header actions, Settings at the far right, and styled descriptive tooltips.
- Compact colored cards with official Bootstrap folder/key icons, bordered white panels/columns, centered column captions, narrow scrollbars and full database paths under record counts. Field actions use smaller, closely spaced Bootstrap icons.
- Styled menus/dropdowns, labeled file-picker rows, and explicit show/hide controls for opening/creation passwords. Hidden/closed/locked password inputs clear their temporary revealed text.

## Two databases

- Either pane's Database menu can open or create another database. Two files maximum; separate files may use different master keys.
- Same-file Windows aliases are rejected rather than loaded as independent sessions. Separate database copies remain separate files.
- Same-database drags edit the live model and wait for Save.
- Cross-database drags require an explicit **Save & copy / Save & move** decision. This publishes pending edits in both files before transferring. Copy keeps the source. Move verifies the saved destination first, then commits source removal.
- Subtrees retain hidden fields, protected flags, attachments, history, tags, expiry, group properties and custom icon images. Transferred groups/entries/icons get new UUIDs; recognized internal UUID field references and mixed ordering are remapped.
- Moves containing the source recycle/templates group, or recognized UUID field references crossing the selection boundary, are blocked; Copy remains available. Unknown plugin metadata and name-based references are not interpreted or rewritten.
- There is no cross-file atomic transaction or automatic deduplication. A crash, failed source save, or external writer can leave a copy in each database. The app favors keeping data.

## Saving and recovery

- Save/Ctrl+S publishes all loaded databases. Files are saved sequentially; failure can leave one saved and another still dirty. The error and dirty state remain visible.
- Private encrypted checkpoints every 15 or 30 seconds when dirty; these do not update the shared database.
- Save reminder at about one minute or a batch of actions; a stronger prompt after longer editing. Keeping edits private remains possible.
- Configurable inactivity lock: 1, 2, 5, 10 or 15 minutes; default five. OS session lock also requests safe closure. In-flight commits complete or stop safely first.
- Last completed edit is checkpointed before a successful lock/close. Successful closure releases both sessions and gates; no reauthentication is required to exit a locked window.
- Protection failures offer retry, recovery to another location, or explicit discard. Failure is never labeled as a successful lock.
- Encrypted baseline/working/pending-conflict recovery branches survive restart. Existing drafts must be resumed, explicitly discarded, or left by canceling that database's opening.
- Backup-only checkpoints remain discoverable; a damaged latest draft offers an explicit older-checkpoint fallback while preserving the damaged primary.
- Unique encrypted save/checkpoint candidates, decryption and intended-content readback checks, atomic filesystem replacement, first-session backup, rolling previous copy and late-writer preservation.
- Replacement preimages start under unique encrypted filenames. An unreadable preimage blocks further publication in that session and remains preserved across later sessions.
- SHA-256 change detection, three-way field/group/order reconciliation, conflict choices and complete Keep-both entry copies.
- Review concurrent username/password/URL changes as one credential bundle; retain independent notes/custom fields. Authenticate new checkpoint component inventories and explicitly review legacy draft compatibility.

## Generator and clipboard

- OS-backed cryptographic RNG; 8–128 characters; upper/lower/digits/symbols selectable; at least one of each chosen category.
- Cancel does nothing; Copy copies and closes; Use fills the invoking field and closes without copying.
- Explicit buttons and normal text copies throughout the window share cleanup tracking and Windows clipboard history/cloud exclusion formats. Matching-content cleanup runs after about 30 seconds or on lock, preserves different replacement text, and retries when busy. Exclusion/cleanup remain best-effort; see [security boundaries](SECURITY.md).

## Deliberate limits

Windows x64 only. No browser integration, autofill, URL/command execution, plugins, sync engine, account service, regex/XPath search or search profiles, bulk import/export, key-file generation, master-key change UI, attachment/history editor, full KeePass settings editor, or record deletion UI. Viewer uses the same live editor; it is not a read-only mode. Other clients may display their own sorting and ignore the custom mixed-order metadata.

No distributed OneDrive lock, guaranteed RAM/pagefile erasure, clipboard-history erasure, automatic resolution of unknown changed keys, publisher signature, independent penetration test, or guarantee against a compromised OS. See [SECURITY.md](SECURITY.md).

## Synthetic previews

[Open screen](docs/images/unlock.png) · [Create database](docs/images/create.png) · [Viewer](docs/images/viewer.png) · [Organizer](docs/images/organizer.png) · [Two databases](docs/images/dual.png) · [Generator](docs/images/generator.png)
