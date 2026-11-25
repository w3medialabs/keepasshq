# Release instructions

The permanent source repository is [w3medialabs/keepasshq](https://github.com/w3medialabs/keepasshq). Publish binaries and corresponding source at [GitHub Releases](https://github.com/w3medialabs/keepasshq/releases). Source pushes and the manual Windows validation workflow do not publish a release automatically.

## Review the candidate

1. Inspect branch, remotes, upstream history, index, working tree, stashes and local-only commits before integrating or committing work. Preserve unrelated work and never force-push. Use the actual reviewed commit for the release.
2. Inspect the complete staged list/diff for real vaults, factors, drafts, backups, settings, generated output, personal paths and sensitive screenshots. Only reviewed fictional previews belong in public assets.
3. Verify [private vulnerability reporting](https://github.com/w3medialabs/keepasshq/security/advisories/new) is available. Maintainers can enable it in the repository's Security settings. Do not publish exploit details to test the channel.
4. Review the pinned dependency version/hash, [provenance](../THIRD-PARTY.md) and licenses. A dependency upgrade requires an explicit pin change and new review/tests. Preserve legacy draft paths and metadata for compatibility.
5. Run `powershell -NoProfile -File .\validate.ps1 -Render` and inspect the affected fictional screens under `test-output/renders`. Record results and remaining limits in [VALIDATION.txt](VALIDATION.txt), the changelog and release notes. The manual [Windows workflow](https://github.com/w3medialabs/keepasshq/actions/workflows/build.yml) provides additional evidence when available; hosted CI is not required for local validation.

## Build and package

For a local portable deployment from a clean, committed checkout:

```powershell
powershell -NoProfile -File .\package.ps1 -Demo
```

This archives the selected commit, extracts it into an isolated ignored build directory, runs full build/test/render validation there, and creates `release-output/preview-<commit>/KeePassHQ/KeePassHQ.exe`, a portable ZIP, its matching source ZIP and `SHA256SUMS.txt`. Licenses, corresponding upstream source, build identity and fresh validation summaries accompany the executable. `-Demo` adds a newly generated fictional database (password `demo`) and verifies it by readback; omit it for a package without a database. Existing destinations are preserved; use `-OutputDirectory` with a new ignored or external directory for a repeat package. The script verifies ZIP inventory and executable/demo payload hashes. It does not publish a GitHub Release, create a tag, launch the app or access a real database. See [Build](BUILD.md) for prerequisites.

Build both variants from the candidate source, using the bundled verified dependency without network access. The validator verifies release diagnostics exclusion and embedded dependency integrity. Rebuild from an extracted candidate source archive before publication. Exact byte reproduction is not promised by the legacy compiler's timestamps; successful rebuilding and unchanged version/resource pins are required.

Prepare packages in an ignored output directory. For example, after committing the final reviewed candidate:

```powershell
New-Item -ItemType Directory -Force .\release-output | Out-Null
git archive --format=zip --output=release-output/KeePassHQ-source.zip HEAD
```

The source archive must contain the application source, build/validation scripts, assets, full GPL text, Bootstrap MIT notice, verified managed KeePass dependency, corresponding upstream source mirror, help/acknowledgements and provenance. Keep it alongside the portable archive for that exact release; a moving branch is insufficient.

The portable ZIP should contain `KeePassHQ.exe`, README/user guide, feature list, changelog, security/validation information, LICENSE, THIRD-PARTY.md and the `third_party/` notices, matching upstream source and help material. A fictional `Demo.kdbx` (password `demo`) and synthetic previews are optional; inspect them explicitly. Do not include diagnostics executables, fixture directories, local settings or drafts. Preserve the single-executable runtime: notices/source can be distributed alongside it without adding a runtime service or installer.

Generate SHA-256 checksums for both archives and the executable. If signing, sign only after code is final, then calculate hashes of the signed artifacts. State clearly when the wrapper is unsigned; the KeePass dependency signature does not authenticate KeePassHQ. Record the reviewed commit, versions, assertion count, test/render results and known limitations in release notes.

## Publish and verify

1. Inspect existing tags/releases before choosing a version. Update version metadata/changelog for a version change; do not relabel an existing release or overwrite unrelated assets.
2. Push the reviewed commit normally. Create a version tag pointing to that exact commit only after validation and source packaging are complete.
3. Create a draft release under [w3medialabs/keepasshq](https://github.com/w3medialabs/keepasshq/releases/new), upload the portable ZIP, corresponding source ZIP and checksums, then review the title, notes and assets before publication.
4. Download the published assets into an isolated directory, verify hashes and archive inventories, and confirm the source archive builds. Check public installation/security links and describe the unsigned status and development-review limits.

## Environment checks

Use disposable fictional vaults to verify pointer drag/drop at the target DPI, keyboard focus, Windows session lock and sync-provider file behavior. Validate any supported account-key factors separately in an owner-controlled test environment. The synthetic suite does not replace those environment-specific checks. Never upload a real vault or key factors to an issue or runner, access the user's clipboard during development, or automate an unlocked password manager.
