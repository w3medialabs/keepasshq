# Help and reporting privacy

Use offline **Help / F1** for instructions. For ordinary bugs and feature requests, use [GitHub Issues](https://github.com/w3medialabs/keepasshq/issues/new/choose). Issues in this public repository are public. Suspected vulnerabilities belong in [private vulnerability reporting](https://github.com/w3medialabs/keepasshq/security/advisories/new), which was enabled and verified during development. If unavailable, request a confidential contact in a public issue without exploit details.

GitHub's own instructions describe [reviewing and submitting an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue) and [privately reporting a vulnerability](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/report-privately). Neither channel is a place to attach a real vault, master/key/account factor or recovery material.

## What the application does

- Embeds the locally reviewed [Help guide](HELP.md) into the single executable. Navigation and reading require neither an unlocked database nor a network connection. F1 also works during an existing dialog; Help uses a separate window and preserves that pending decision.
- Provides explicit **Copy report template**, **Copy issue link** and **Copy private security link** buttons in the reporting topic. Clipboard writes use the same exclusion formats, ownership digest and best-effort cleanup as other explicit app copies.
- Fills only the application assembly version automatically. The remaining report fields are blank prompts. It does not read a database/model, key factor, path, entry/group name, note, URL, search query, attachment, log, exception, screenshot, machine/account name or clipboard content to prepare that template.
- Copies fixed canonical GitHub URLs. No database-derived data enters a URL, including query parameters. It never launches a browser, authenticates to GitHub, submits a report, sends telemetry or uploads a file.
- Leaves reporting to the user's external browser and explicit submission. Help actions do not extend vault inactivity; the main application can protect and lock while Help remains open.

The report template is an allowlist of one version value and fixed text, not an automatic redactor or sanitizer. A future change that collects more data would expand the privacy boundary and needs deliberate review. Avoid adding automatic diagnostic bundles, screen capture, crash dumps, stack traces or authentication tokens as a convenience feature.

## Preparing a useful report

1. Reproduce the problem with a disposable fictional database and invented identities under `example.test` domains. Describe exact steps, expected behavior and observed behavior.
2. Optionally enter the package's build/commit from BUILD-INFO.txt and Windows/display-scale information manually. The in-app template does not collect either automatically.
3. Review every line and any attachment before posting. Use screenshots only from fictional fixtures; never post a screenshot of your actual password-manager data.
4. Keep real vaults, key/account factors, drafts, backups, plaintext exports, private filenames/paths, account names, entry/group titles, notes, vault URLs, attachments and raw logs/exceptions out of the report. Encryption does not make a real database appropriate for a public issue.
5. Use the private channel for vulnerability details, still with fictional reproductions and no actual secrets. If you accidentally publish sensitive material, treat it as disclosed; deleting an issue or attachment cannot guarantee removal of copies.

## Validation and limits

In-process tests use a clipboard probe to verify that opening Help does not copy/read anything; report preparation never reads clipboard content; generated text and fixed URLs exclude loaded fictional model/factor/query/path sentinels; clipboard failures retain existing text; and copied templates use timeout cleanup. Pending key dialogs, application choices and conflict decisions survive Help/F1/Escape. The vault can checkpoint and lock while static Help remains open.

The validator hashes the embedded Help resource against its reviewed source and verifies diagnostics are excluded from release builds. Fictional Help/reporting previews are rendered without showing a native password-manager window. Tests never submit a real GitHub issue/advisory or inspect the user's clipboard.

Users can still add secrets manually after copying a template, and other software can observe clipboard/memory or ignore exclusion formats. Existing [security boundaries](../SECURITY.md), unsigned status and development-review limits apply. Manual reporting reduces collection risk; it does not promise that every user-written report is free of sensitive data.
