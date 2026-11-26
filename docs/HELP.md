# KeePassHQ help

## Getting started

KeePassHQ is a portable Windows x64 application requiring .NET Framework 4.8 or later. Run it from its own trusted folder. The wrapper is unsigned and is not an official KeePass release or independently certified security product.

**Open a database:** choose a local .kdbx file, enter every required key factor, then select Unlock database. If a portable preview includes Demo.kdbx, its fictional password is `demo`. Never put real credentials in that demo.

**Create a database:** select Create new database, choose an unused filename, and enter and confirm a nonempty password, or select supported nonpassword factors. Existing files are never overwritten. New databases start empty.

The eye button explicitly reveals or hides a master-password input. Enter submits either representation. Closing the dialog, disabling the password factor, completing decryption or locking clears the temporary input. Expanded entry passwords are visible by design.

Keep a key file separate from its database and back up every required factor securely. Windows account protection needs that account's intact protected material; recreating the login name or copying this executable does not recreate the factor. There is no universal recovery key. Plugin key providers and ciphers are unsupported.

## Browsing and editing

**Viewer** has one browser. **Organizer** has two identical browsers. Both allow editing. Initially both browse the same database independently; changes to a shared entry mirror immediately.

Click a folder card to open its children in the next column. Select another folder at that level to replace deeper columns. Click an entry's key/title header to expand or collapse its name, username, password, URL and notes.

Use **+** in a column header to create an entry or group. Right-click a group header to rename it. No automatic Old/New folders are created. There is no record deletion or attachment/history editor.

Fields update the private working model immediately. Use the small Cut/Copy/Paste actions on the selection, or on the whole field when nothing is selected. Paste inserts at the cursor. Notes can be selected and split manually into newly created entries; no automatic credential extraction occurs.

Drag a card by its header. Drop on a group to nest it, or into a widening green gap to set its order. Hover to open a group, and use the edges/scrollbars to reach deeper destinations. Drop into the root column for a top-level card. A group cannot be moved into its own descendants.

**Ctrl+Z** undoes a recent model action. Edits stay unpublished until Save. Other KeePass clients may use their own sorting and ignore KeePassHQ's mixed group/entry ordering.

## Searching

Each browser's **Search this database** field searches only that browser's database. All typed words must match, without case sensitivity, across names, usernames, URLs, notes or group paths. Words are literal; quotes, exclusions, regular expressions and XPath are not interpreted.

Password fields, hidden fields, history and attachments are not searched. Notes are searched as entered text, including credentials you may have placed in notes. Enabled recycle-bin contents and groups with inherited searching disabled are omitted.

Matching cards retain the normal editors and mirror live edits. They stay in place while you edit; press **Enter** in the search field to refresh. A result's group-path link returns to its folder and expands the entry. Results can be dragged into a normal browser destination; the result list itself is not a drop target.

**Ctrl+F** focuses the browser containing keyboard focus, or the left visible browser. Clear **×** or **Escape** in the search field restores its previous browsing context. Queries survive Viewer/Organizer changes but are not stored in settings, databases or drafts. Lock/close clears them. Ctrl+Z in a search field does not undo database edits.

## Saving and private drafts

**Save / Ctrl+S** publishes pending work to every loaded database. Two files save sequentially; a failure can leave one saved and the other dirty. Review the status and retry or save a recovery copy when appropriate.

Private encrypted checkpoints run every **15 or 30 seconds** while edits exist. They protect local work without publishing it to the shared database. A reminder encourages an explicit Save. A checkpoint can only protect the revision it successfully wrote; keep the application open when protection fails.

Encrypted version backups are kept beside the database in its `.versions` directory. Private drafts and version backups contain complete sensitive data and need the same protection as your vault. Never attach them to a bug report.

Draft discovery depends on the database's original absolute path and key factors. Moving or renaming the file can prevent automatic discovery. Keep the original path and encrypted copies available when recovering. Separate draft branches are not silently erased.

On opening a database with an existing draft, choose to resume and review it, explicitly discard it, or cancel opening and retain it. A damaged latest checkpoint may offer an explicitly selected older checkpoint, which can omit newer work. Older draft formats require a compatibility review. Keep damaged or older material until recovery is verified.

## Recovery and conflicts

**Save recovery copy** writes the current working database to a new encrypted filename, leaving the original unchanged, then closes loaded databases after protecting other dirty sessions. It is a working database you can reopen with the same factors. Existing destination files are never overwritten.

If the shared file changed, KeePassHQ compares the loaded baseline, your edits and the changed file. Compatible changes merge; conflicts require **Use app edits**, **Use file edits**, or **Keep both entries** when available. Keep both gives the file version a new identity.

Concurrent username/password/URL changes are reviewed together as one credential bundle, preventing a combination that never existed in either version. Independent notes and custom fields may still merge.

Changed key factors or a different database identity stop automatic reconciliation. Preserve every encrypted version and use each version's correct factors; do not overwrite one to force the merge. A preserved unreadable competing version can block further publication for that session while still allowing a recovery copy.

OneDrive and similar providers operate independently. Let synchronization finish before opening and after saving. Remote edits not yet present on this computer cannot be detected. Local locking and verification do not provide a distributed transaction or guarantee against later external deletion.

## Two databases and transfers

In Organizer, use either pane's database-folder button to open or create a different database. Two separate files can have independent master passwords/key factors. Closing one file makes both panes browse the remaining file. Viewer hides the second pane without closing its database; Save and Lock still cover both.

A cross-file drag asks for **Save & copy** or **Save & move**. Both first publish pending edits in both databases. Copy saves a complete clone in the destination and keeps the source. Move saves and reopens/verifies the destination before removing and saving the source.

Transfers are sequential, not atomic across two files. An interruption may leave a duplicate. If the destination saved but source cleanup failed, review both files before retrying. Do not assume an error means that no destination copy exists.

Recognized UUID links crossing a selection's boundary and selections containing the source recycle/templates group are copy-only. References to entries outside a copied selection and opaque plugin conventions may require manual review. Transfers clear local undo.

## Lock, close and settings

**Lock / Ctrl+L** protects final unpublished work in an encrypted draft, releases loaded files and drops view/model/key references. It does not publish a shared Save. Unlocking opens and decrypts again. Inactivity and Windows session lock request the same safe closure.

**Close** in the header closes loaded databases and returns to the open screen. Unsaved work offers Save or Keep draft. The native window close also allows Cancel. A successfully locked window closes without another password.

If checkpoint protection fails, **Lock incomplete** means working data remains in memory. Choose Retry, save a recovery copy elsewhere, or explicitly discard it. A successful lock is not claimed. Lock/close requested during an in-flight operation is completed safely after that operation stops or finishes.

**Settings** at the far right selects a 15/30-second checkpoint interval and a 1, 2, 5, 10 or 15-minute inactivity lock. Defaults are 30 seconds and five minutes. Settings live beside the executable; an unwritable folder limits changes to the current session.

Help is a separate local window. It does not dismiss an active conflict, recovery or key dialog. Reading Help does not prevent the main application's inactivity lock.

## Passwords and clipboard

Use the key action beside a password field to generate 8–128 characters with selected uppercase, lowercase, digit and symbol categories. **Cancel** leaves the field alone. **Use** fills it without copying. **Copy** copies and closes the generator.

Explicit buttons and normal text copies request Windows clipboard history/cloud exclusion and share matching-content cleanup after about 30 seconds or on lock. Other software can ignore exclusion formats. Cleanup preserves different replacement text and retries while busy, but identical replacement text and a read/clear race cannot be distinguished reliably.

Expanded values, screenshots, managed strings, clipboard history, memory dumps and pagefiles have no forensic erasure guarantee. Help/reporting never automatically captures these surfaces. This application does not run entry URLs, commands, plugins or auto-type.

## Troubleshooting

**Could not unlock:** check every factor, local file availability and supported cipher/provider requirements. Do not replace or regenerate a key/account factor to work around an unlock failure.

**Already open:** close or lock the other KeePassHQ window. A local sidecar gate coordinates KeePassHQ windows; other applications and remote devices can ignore it.

**Save failed:** keep the working session open, let file/provider activity settle, and retry or save a recovery copy to a writable location. Review retained competing versions. Never delete the only encrypted backup while troubleshooting.

**Embedded dependency required:** run KeePassHQ in its own trusted folder. A neighboring KeePass.exe or KeePass.dll is refused instead of replacing the pinned embedded dependency.

**Large or expensive vault:** encryption/checkpoint cost follows the database's derivation settings. Existing cipher/KDF choices are preserved. This small editor does not enforce every KeePass UI policy or provide a hostile-file resource sandbox.

For help beyond these topics, reproduce the issue with a fictional database and use the reporting topic. The portable package also includes a detailed user guide, security policy, source/license notices and development audits.

## Keyboard shortcuts

- **F1:** open or return to this offline Help window, including from the open screen or an active in-app dialog.
- **Ctrl+F:** focus the active browser's search field.
- **Enter in search:** refresh matching cards after editing.
- **Escape in search:** clear the query and restore browsing.
- **Ctrl+S:** publish all loaded databases.
- **Ctrl+L:** checkpoint and lock loaded databases, including when Help has focus.
- **Ctrl+Z:** undo a recent model action while browsing/editing; not while typing a search.
- **Enter in a master-password input:** submit open/create with the same validation as the primary button.
- **Escape in Help:** close only Help. An underlying dialog remains intact.

Other database/search shortcuts apply in the main application window, not while reading Help. Native Windows file pickers handle their own keys.

## Report a problem

**GitHub Issues are public.** Report ordinary bugs and feature requests there using fictional reproductions. Use GitHub's private vulnerability channel for suspected security problems. Private reporting is not a reason to upload a real vault or its factors.

The buttons below copy a blank report template or a fixed reporting link. **Only the application version is filled automatically.** KeePassHQ does not create or send the report, open a browser, collect a screenshot/log/stack trace, inspect clipboard contents for a report, attach a file, or include database names, paths, entries, key factors, search queries, computer names or account names.

Paste the link into your browser, sign in to GitHub there, and review your report before selecting Submit. Fill in reproduction steps manually using invented accounts and a disposable database. You can enter the package's build/commit and Windows/display information manually when useful.

Never include real passwords, key/account-protection files, vaults, drafts, version backups, attachments, personal paths, entry/group names, URLs from your vault, or screenshots of actual data. Raw logs and exceptions can also contain sensitive values. A blank template is not a sanitizer for text you add later.

If private reporting is temporarily unavailable, open an ordinary issue asking for a confidential contact without exploit details or sensitive material.

## About and boundaries

KeePassHQ is an independent GPL-2.0-or-later community application using the pinned KeePass 2.62 cryptography/library. Bootstrap Icons are used under MIT. Preserve the included licenses, acknowledgements and corresponding source when redistributing a build.

The app stays local and offline: no account service, telemetry, automatic updater, automatic reporter, URL launcher or bundled web runtime. Clipboard copying is always an explicit user action or normal field copy/cut. A separate sync provider, Windows and other software have their own behavior outside this process.

The wrapper remains unsigned. Development audits and synthetic tests do not constitute independent certification, a penetration test or a guarantee that no defects remain. Use trusted builds, keep independent encrypted backups and test important workflows with disposable copies first.
