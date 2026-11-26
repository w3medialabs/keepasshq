# Contributing

Keep KeePassHQ focused: clear card browsing, careful editing/reordering, understandable save/recovery behavior, and very small runtime dependencies. Discuss larger features in an issue before implementation. Do not introduce telemetry, network services, plugins or automatic credential extraction as incidental changes.

Use [issues](https://github.com/w3medialabs/keepasshq/issues/new/choose) for fictional bug reproductions and focused proposals, and [pull requests](https://github.com/w3medialabs/keepasshq/compare) for reviewed changes. Fork [w3medialabs/keepasshq](https://github.com/w3medialabs/keepasshq) and clone your fork if you do not have repository write access. Maintainer work follows the applicable workspace instructions; external contributors may use a topic branch in their fork.

1. Read [BUILD](docs/BUILD.md), [ARCHITECTURE](docs/ARCHITECTURE.md), [SECURITY](SECURITY.md), and the [upstream-guidance audit](docs/FINAL-AUDIT.md).
2. Build and run synthetic tests locally. Use invented accounts under reserved `example.test` domains, disposable databases and isolated clipboard/draft services.
3. Add targeted tests for behavior with real data-loss/security consequences. Do not add implementation-mirroring tests for cosmetic changes.
4. Include what changed, why, validation performed, and any compatibility or failure behavior in your pull request. Include synthetic renders for visible changes.
5. Review dependency provenance and license effects before changing embedded code/resources. Keep release builds free of diagnostics and vault samples other than explicitly fictional demo material.
6. Keep `docs/HELP.md` and the detailed user guide aligned when changing user-visible behavior. Help is embedded without a browser/HTML engine. Preserve the one-value report allowlist and [reporting privacy boundary](docs/REPORTING.md); do not add automatic logs, dumps, screenshots or session-derived URL parameters.

Run `powershell -NoProfile -File .\validate.ps1 -Render` from your checkout. It creates ignored local build/test output. Review rendered PNGs under `test-output/renders` for affected screens. The [manual Windows workflow](https://github.com/w3medialabs/keepasshq/actions/workflows/build.yml) runs the same command; local evidence remains useful when hosted runners are unavailable.

Do not commit real vaults, key files, drafts, backups, settings, plaintext exports, runtime logs or screenshots with secrets. The ignore rules are a guard, not a secret scanner. Inspect your diff and staged file list. For security problems, use the private process in SECURITY.md.

Contributions are provided under GPL-2.0-or-later; keep third-party copyright/license notices intact. Do not claim independent audit, formal assurance, cross-cloud atomic saves or forensic memory erasure without corresponding evidence.
