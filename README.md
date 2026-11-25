<p align="center"><img src="assets/KeePassHQ.svg" width="88" alt="KeePassHQ house and lock"></p>

# KeePassHQ

A portable Windows workspace for browsing and organizing KeePass databases. Expand cards, clean up notes, and move entries between nested groups without wrestling with a compact tree.

**Version 1.2 · Windows x64 · .NET Framework 4.8+ · GPL-2.0-or-later**

[Source](https://github.com/w3medialabs/keepasshq) · [Releases](https://github.com/w3medialabs/keepasshq/releases) · [Issues](https://github.com/w3medialabs/keepasshq/issues) · [Private security reports](https://github.com/w3medialabs/keepasshq/security/advisories/new)

![Organizer with fictional entries](docs/images/organizer.png)

## Get started

Download a published portable archive from [GitHub Releases](https://github.com/w3medialabs/keepasshq/releases), when available, and verify its published SHA-256. Until a binary release is published, [build from source](docs/BUILD.md). The executable is unsigned. Windows x64 with .NET Framework 4.8 or later is required; Windows on ARM and other operating systems are not supported targets.

1. Extract the portable release and run `KeePassHQ.exe`. No installer or installed copy of KeePass is required.
2. Browse to an existing `.kdbx`, supply its key factors, and unlock. Or choose **Create new database** to create an empty database with a new name and key.
3. Use **Viewer** for one browser or **Organizer** for two equal browsers. Initially both browse the same database independently.
4. Click a group to open its children one column to the right. Click an entry to expand its editable fields.
5. Drag a card onto a group to nest it, or into an expanding gap to set its position. Save with **Ctrl+S** when the edits are ready.

Use a disposable copy first. Passwords are deliberately visible while entries are expanded. The application has no network client, account, telemetry, update checker, or cloud API; a sync provider can still synchronize the encrypted files you choose.

## Focused features

- Identical card browsers in both modes; independent navigation, shared live edits when browsing the same database.
- Five editable fields: name, username, password, URL, and selectable multiline notes. Integrated cut/copy/paste actions.
- Create groups and entries at any level, rename groups, reorder mixed cards, and move in either direction. Active groups are green.
- Open or create a different database from either pane's **Database** menu. Keys, models, drafts, saves, and file gates remain separate.
- Cross-database **Save & copy** or **Save & move**. Moves save and verify the destination before removing the source; an interruption may leave a duplicate.
- Explicit saves, private encrypted draft checkpoints, save reminders, inactivity locking, safe database closing, and recovery copies.
- SHA-256 change detection and three-way conflict review before replacing a changed database. Encrypted backups and post-replacement checks preserve late writers.
- Password generator with length/category rules and exactly **Cancel**, **Copy**, and **Use**.
- KeePass 2.62 handles KDBX encryption and rich metadata. Hidden fields, history, and attachments are retained without reducing entries to the visible fields.

## Documentation

| Document | Contents |
| --- | --- |
| [Feature list](FEATURES.md) | Supported behavior and deliberate limits |
| [User guide](docs/USER-GUIDE.md) | Navigation, editing, two databases, saving, drafts and recovery |
| [Build and tests](docs/BUILD.md) | Offline build, diagnostics, source layout and CI |
| [Security policy](SECURITY.md) | Threat boundaries and private reporting |
| [Development audit](docs/AUDIT.md) | Findings, corrections, tests and remaining limits |
| [Adversarial follow-up](docs/ADVERSARIAL-AUDIT.md) | Reproduced edge cases, fixes and regression evidence |
| [Architecture](docs/ARCHITECTURE.md) | Sessions, storage, merge and transfer protocol |
| [Design choices](docs/THEORY.md) | Browser model, private edits, preservation and narrow runtime |
| [Contributing](CONTRIBUTING.md) | Small changes, synthetic fixtures and review expectations |
| [Release checklist](docs/RELEASING.md) | Publishing a public repository and reproducible checks |
| [Changelog](CHANGELOG.md) | Version history |
| [Third-party notices](THIRD-PARTY.md) | KeePass, Bootstrap Icons and source provenance |

## Build and contribute

```powershell
git clone https://github.com/w3medialabs/keepasshq.git
cd keepasshq
.\build.ps1 -OutputDirectory .\bin
powershell -NoProfile -File .\validate.ps1 -Render
```

Builds use the bundled, hash-pinned KeePass 2.62 dependency without package restore. Validation checks source inventory, documentation links and provenance, builds release and diagnostics, checks the release boundary and embedded dependency, runs the synthetic suite, and renders nine fictional screens. See [Contributing](CONTRIBUTING.md) before opening a [pull request](https://github.com/w3medialabs/keepasshq/pulls).

This is an independent application using KeePass technology, not an official KeePass release. The house-lock mark is from [Bootstrap Icons](https://icons.getbootstrap.com/icons/house-lock/), used under MIT. KeePassHQ and its combined distribution are GPL-2.0-or-later; see [LICENSE](LICENSE).
