# Third-party notices and provenance

## KeePass 2.62.0.0

Copyright (C) 2003–2026 Dominik Reichl and contributors. GNU GPL version 2 or later. [Official project](https://keepass.info/), [download/source](https://keepass.info/download.html), [published integrity hashes](https://keepass.info/integrity.html).

The unmodified managed KeePass.exe is embedded into KeePassHQ.exe. Its application entry point, plugins and external native helper DLLs are not run/loaded by this wrapper. The build pins both version and SHA-256. Full license and matching upstream source mirror snapshot are included under `third_party/KeePass/`; KeePass.chm includes upstream acknowledgements/component licenses. The portable distribution also includes these notices and upstream help/license material. Distribute the corresponding KeePassHQ source package alongside public binaries.

Portable archive transport mirror:
https://github.com/JiriSko/KeePass/releases/download/v2.62/KeePass-2.62.zip

Official portable archive SHA-256, verified before extraction:
`49B008E4CA9E9DB9CB22E1173EC0F7676235B5309D7B47213CF81010CD1BBEED`

Extracted KeePass.exe SHA-256:
`AFB89B74AE1B8519F483D600095F4DF8D86628178AF6ACDC5F2B23888C89C3F3`

Authenticode checked Valid, signer Open Source Developer, Dominik Reichl. This signature authenticates the dependency, not the KeePassHQ wrapper.

Included upstream source snapshot: https://github.com/JiriSko/KeePass, v2.62 tag object `8a81ba888758aca90e8e3980c5626a2a541ff3bc`.

Source mirror repack SHA-256:
`E0E692D04B86FF2F509507A540692DD2B33730B208CE89A7A2FA18EEC38D87D8`

The included mirror ZIP has different wrapping/layout from the official `KeePass-2.62-Source.zip`; it is identified as a mirror, not represented as the official archive. The separately downloadable official source archive has SHA-256:
`9C07B0539A013EE5E22B26FD806E472C1858BC2F3F7B3B4406F4404774263AC0`

## Bootstrap Icons

The logo/app icon uses the unmodified house-lock geometry from [Bootstrap Icons](https://icons.getbootstrap.com/icons/house-lock/), recolored blue for KeePassHQ and rasterized for WPF/Windows icon sizes. Copyright (c) 2019–2024 The Bootstrap Authors. MIT license reproduced in `third_party/Bootstrap-Icons-LICENSE.txt` and in the portable distribution. Original SVG is in `assets/house-lock.svg`; branded SVG/PNG/ICO are adjacent. No endorsement by Bootstrap is implied.

Toolbar, card, search and field-action icons use nineteen unmodified SVGs from [Bootstrap Icons 1.13.1](https://github.com/twbs/icons/tree/ce0e49dd063243118a115f17ad1fe1fe7576d552), commit `ce0e49dd063243118a115f17ad1fe1fe7576d552`. Their original sources, matching MIT license, hashes and action mapping are included in [assets/bootstrap-icons](assets/bootstrap-icons/README.md). WPF renders their filled vector paths locally, preserving each path's fill rule and original viewBox. These assets are embedded into the portable executable, with no runtime download or additional rendering library.

## Platform

Windows/.NET Framework/WPF are system prerequisites under their respective Microsoft terms; no installer, bundled web runtime or external package restore is included. GitHub CI actions have their own licenses and are not runtime dependencies.
