# Bundled Bootstrap Icons

Unmodified SVG sources from [Bootstrap Icons 1.13.1](https://github.com/twbs/icons/tree/v1.13.1), commit `ce0e49dd063243118a115f17ad1fe1fe7576d552`. Each SVG comes from that commit's `icons/` directory. [LICENSE.txt](LICENSE.txt) is the upstream MIT license; [SHA256SUMS.txt](SHA256SUMS.txt) records the bundled SVG hashes.

KeePassHQ embeds these eighteen SVGs in its executable. `UiVisual` reads only their path geometry, preserves their original fill rules and 16×16 viewBox, and applies the control's color. Field actions render at 12 pixels; general controls and card icons render at 16 pixels. No icon font, browser, external renderer or runtime download is used.

| KeePassHQ action | Upstream icon |
| --- | --- |
| Group / database | folder |
| Entry / password generator | key |
| Save | floppy |
| Undo | arrow-counterclockwise |
| Close | x-lg |
| Lock | lock |
| Settings | gear |
| Show / hide password | eye / eye-slash |
| Viewer / Organizer | view-list / layout-split |
| Recovery copy | file-earmark-plus |
| Add | plus-lg |
| Copy / paste / cut | copy / clipboard / scissors |
| Disclosure / information | chevron-down / info-circle |

The existing house-lock brand source remains in [house-lock.svg](../house-lock.svg), with its original attribution in [THIRD-PARTY.md](../../THIRD-PARTY.md).
