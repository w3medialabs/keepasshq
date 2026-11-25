# Build and tests

## Requirements

Windows x64, .NET Framework 4.8+, Windows PowerShell or PowerShell. The build uses the Framework64 C# compiler and WPF assemblies already on Windows; no SDK, NuGet restore, Node runtime, installer or network access is required.

The repository includes `src/KeePassHQ/Dependency/KeePass.exe`, the unmodified KeePass 2.62 managed assembly. The build requires its exact version and SHA-256:

`AFB89B74AE1B8519F483D600095F4DF8D86628178AF6ACDC5F2B23888C89C3F3`

See [third-party provenance](../THIRD-PARTY.md). Updating the dependency requires a deliberate pin change and regression review. The runtime embeds this assembly and does not invoke the KeePass application or load plugins/native helper DLLs.

## Compile

From the repository root:

```powershell
.\build.ps1 -OutputDirectory .\bin
```

This creates one `bin\KeePassHQ.exe`. If the local script policy requires it, use an approved PowerShell script execution policy for your own checked source. No admin privileges are required. Release builds omit test classes, fixture generators, isolated clipboard, fault-injection hooks and diagnostic command modes. Source remains C# 5-compatible with the included framework compiler.

## Run synthetic validation

Recommended complete check, from any working directory:

```powershell
powershell -NoProfile -File .\validate.ps1 -Render
```

Use the full path to `validate.ps1` when outside the checkout. The script checks source inventory, secret/personal-path patterns, Markdown links, corresponding upstream source hash and branding assets; builds both variants; verifies release diagnostics exclusion and the embedded dependency hash; runs the suite; and optionally renders all nine screens. Omit `-Render` for the same checks without previews. Summaries and generated fictional data stay under ignored `test-output/`; build output stays under `bin/` and `bin-diagnostics/`.

Run `powershell -NoProfile -File .\scripts\check-source.ps1` for source checks alone. Git checkouts inspect tracked and unignored files; extracted archives inspect the documented source directories. Pattern checks support manual review and do not establish that arbitrary content is free of secrets.

To run the diagnostics executable separately:

```powershell
.\build.ps1 -Diagnostics -OutputDirectory .\bin-diagnostics
$exe = (Resolve-Path .\bin-diagnostics\KeePassHQ.exe).Path
$results = Join-Path $PWD 'test-output'
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $results + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content (Join-Path $results 'test-results.txt')
if ($process.ExitCode -ne 0) { throw 'Synthetic checks failed' }
```

GUI executables need `Start-Process -Wait` when scripting completion. Tests create fictional databases, synthetic key files and isolated draft roots under the chosen output directory. Clipboard calls are replaced by an in-memory fake. They do not open your real vault, use your account-protection file, interact with your clipboard, or drive another password manager's UI. Do not pass a real vault path to diagnostics.

The original 1.2 package passed 306 assertions; the adversarial follow-up passes 329. Diagnostics require an explicitly isolated default draft root, including UI-created databases. Test output contains fictional plaintext and diagnostic exception details; keep it out of Git. See [ADVERSARIAL-AUDIT.md](ADVERSARIAL-AUDIT.md) for the new regressions and [AUDIT.md](AUDIT.md) for the original scope and limits.

## Render synthetic previews

```powershell
$image = Join-Path $results 'organizer.png'
Start-Process -FilePath $exe -ArgumentList @('--render-demo', ('"' + $image + '"'), 'organizer') -WindowStyle Hidden -Wait
```

Modes: `viewer`, `organizer`, `generator`, `gap`, `unlock`, `create`, `dual`, `recovery`, `reminder`. These use in-process WPF rendering with fictional fixtures, without opening a real password-manager window. PNGs are 1600×1000. No screenshot of an unlocked real vault is necessary.

## Layout

| Path | Purpose |
| --- | --- |
| `src/KeePassHQ/App.cs`, `Features.cs` | Browsers, editing, reminders, modal/close/lock lifecycle |
| `DatabaseLifecycle.cs`, `FileIdentity.cs` | Create/open/close, per-pane ownership, same-file checks |
| `Core.cs`, `Ordering.cs` | Sessions, encryption, save validation, backup, ordering/tree checks |
| `Merge.cs`, `PendingMerge.cs` | Baseline reconciliation and preserved late versions |
| `Drafts.cs` | Authenticated encrypted checkpoints and restart recovery |
| `Transfers.cs` | Rich cloning, fresh UUIDs/references/icons, destination-first commits |
| `Generator.cs`, `ClipboardService.cs` | Cryptographic generator and clipboard boundary |
| `Tests.cs`, `Diagnostics.cs`, `TransferTests.cs`, `AuditTests.cs` | Diagnostics-only synthetic tests/rendering |
| `scripts/check-source.ps1`, `validate.ps1` | Source/provenance checks and complete local/CI validation |
| `Theme.xaml`, `Brand.png`, `KeePassHQ.ico` | Runtime theme and house-lock mark |
| `third_party/` | Dependency notices, upstream matching source snapshot and acknowledgements |

The [GitHub Actions workflow](https://github.com/w3medialabs/keepasshq/actions/workflows/build.yml) is manually dispatched on Windows and runs `validate.ps1 -Render`. It has read-only repository permissions, commit-pinned actions, no secrets, and no publishing/deployment step. Its artifact contains synthetic result summaries and rendered PNGs. It does not upload generated vaults, factors or draft directories. Local validation is available without hosted CI. Changing a workflow is a code change requiring review.
