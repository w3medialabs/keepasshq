# UI completeness follow-up

Reviewed November 25, 2026 against UI refresh commit `ffe0309b10a0609af23db269db5989432c00c25b`, using first-party source inspection, in-process fictional WPF controls and synthetic renders. This review does not use real databases, actual factors, account-protection files, the user's clipboard or native unlocked-password-manager automation.

## Resolved gaps

| Gap | Resolution and evidence |
| --- | --- |
| Expanding an entry widened its column and input actions dominated the row | Columns now keep a steady 280-pixel width. Field actions use 12-pixel icons in adjacent 22-pixel buttons. Reviewed normal and compact fictional renders; the existing editing, clipboard, generator and scrolling workflows continue to pass. |
| Hand-drawn key and control icons were inconsistent | Use eighteen original Bootstrap Icons 1.13.1 SVGs embedded locally, including its standard horizontal key. Preserve path fill rules, viewBox, upstream sources, hashes and MIT attribution. |
| Enter only worked from the original masked unlock field | Both representations now share submission handling. Synthetic routed Enter events unlock from the revealed main field and open modal, reject a mismatched confirmation, and create a correctly confirmed fictional database. |
| Eye action could leave keyboard focus on the eye button | Explicit eye actions return focus to the corresponding editor; showing places its caret at the end. Programmatic clearing does not steal focus. |
| Exiting with no loaded database skipped factor/clipboard cleanup | The direct exit path clears main/modal representations and attempts app-owned clipboard cleanup. A regression first failed against the original implementation; the fixed path clears owned text and preserves another app's replacement. |
| Dropdown options lacked contextual help | Settings options describe seconds/minutes and the effect of their value. Tests generate the actual popup's item containers, select a real option, and verify its help. |
| Editing an entry name left stale header help/accessibility labels | Live name updates now update those labels alongside the visible title. |
| Custom scrolling and dropdowns had render evidence but little direct interaction evidence | Synthetic thumb-drag and page commands verify horizontal/vertical viewport movement, value synchronization and narrow dimensions. The dropdown preview renders its actual popup template without showing a native window. |
| Local deployment was a sequence of temporary packaging commands | `package.ps1` builds and validates an immutable commit archive, optionally generates/read-verifies a fictional demo, preserves existing deployments, writes standard forward-slash ZIP paths across PowerShell versions, and verifies package inventory, payload hashes and relative checksum paths. |
| Extracted-source checks could skip nested files when the archive was placed beneath an ignored output directory | Filter generated directories relative to the extracted source root. A deliberately broken nested documentation link is rejected inside an extracted archive under `test-output`; the clean archive passes. |

## Completeness and validation

The requested windowed startup, centered segmented view switch, right-side icon actions/far-right Settings, styled help/menus/dropdowns, primary-color notifications, password eyes, compact cards, white bordered panels/columns, narrow scrollbars, centered captions, database paths and folder/key icons are present. Lock, Close and Recovery copy retain their documented storage/lifecycle behavior and have explanatory help. No unrelated runtime dependencies, networking, dependency upgrades or legacy identifier changes were introduced.

The current suite passes **350 assertions**, including twelve follow-up assertions after the prior 338. Fifteen previews render, including the actual dropdown template. Release metadata excludes the new synthetic input source and test workflow. Source inventory, documentation links, secret/personal-path patterns, branding and upstream source integrity checks pass. [VALIDATION.txt](VALIDATION.txt) records run evidence; [release instructions](RELEASING.md) describe local deployment.

## Limits

These are in-process control tests, not physical pointer/DPI/OS compatibility certification. Real sync providers and actual key factors remain outside the fixtures. Clipboard clearing is best-effort when Windows denies access; a process that has exited cannot retry afterward. The wrapper remains unsigned, with no independent security certification. Previous audit/storage boundaries continue to apply; see [SECURITY.md](../SECURITY.md) and [ADVERSARIAL-AUDIT.md](ADVERSARIAL-AUDIT.md).
