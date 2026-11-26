# Issues and labels

Use the [issue chooser](https://github.com/w3medialabs/keepasshq/issues/new/choose) for bug reports, focused feature requests, help questions and documentation corrections. Each template assigns its type label and `needs triage`. The chooser also links the user guide and [private vulnerability reporting](https://github.com/w3medialabs/keepasshq/security/advisories/new). The normal blank-issue option is hidden to encourage the reporting guidance; this is not an access or privacy control.

Issues are public. Reproduce problems with fictional data, review every line and attachment, and never include actual vaults, factors, drafts, backups, entry data, personal paths, raw logs or screenshots of actual data. Private vulnerability reports must also exclude real vaults and factors. See [reporting privacy](REPORTING.md) and [SECURITY](../SECURITY.md).

## Maintainer triage

1. Review the report and its type. Remove `needs triage` after classification; request a fictional reproduction using `needs reproduction` when needed.
2. Add an area where useful: `area: ui`, `area: saving-recovery`, `area: transfers` or `area: build`. Use `maintenance` for repository/tooling upkeep and `accessibility` for accessibility barriers.
3. Reserve `good first issue` for small, well-described work with a clear acceptance condition; use `help wanted` for work ready for contributors. These labels do not assign someone or promise implementation.
4. Link duplicates before closing them. Use `invalid` or `wontfix` with a clear explanation. Remove `needs reproduction` when adequate steps arrive. GitHub's open/closed state tracks completion; no status bot or automatic issue-closing workflow is installed.

Do not turn a suspected private vulnerability into a public issue. If a report exposes actual secrets, avoid quoting/copying them, direct the author to the private channel and follow GitHub's sensitive-content handling options. Labels and templates do not sanitize user text.

## Reproduce the configuration

The label inventory is versioned in [.github/labels.json](../.github/labels.json). Existing GitHub default labels are retained. With authenticated GitHub CLI access, run:

```powershell
powershell -NoProfile -File .\scripts\configure-issues.ps1
```

This enables Issues and creates missing labels only. It preserves all existing labels, their descriptions/colors and issue assignments, including labels absent from the inventory. It does not create an issue, submit a report, enable an automation, change vulnerability-reporting settings or collect application data. A fork maintainer can supply `-Repository owner/repository` to configure their own fork explicitly. Template changes take effect when committed to the default branch; keep `bug_report.md` stable because in-app Help links to it.
