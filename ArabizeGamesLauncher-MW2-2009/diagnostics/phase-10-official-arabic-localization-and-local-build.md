# Phase 10 — Official Arabic Localization and Exact Local Build

**Inspection date:** 2026-11-03  
**Scope:** Read-only inspection of local identification/configuration metadata and language-switcher text, plus official support-documentation research. No CASC archives were opened, extracted, or decompressed; no executable or registry file was run; no game or launcher setting was changed.

## Verdict

**ARABIC LOCALE SUPPORTED BUT CONTENT UNVERIFIED**

The active local Battle.net installation metadata identifies product `lazr`, version `1.1.2.1279292`, build name `CRC_1279292A_retail_signed_ship`, and includes `arSA` text and speech locale tags. This is good evidence that this build’s install metadata provides Arabic locale variants. It does not prove the Arabic payload is present in this installation, that the files contain Arabic, or that the whole campaign is translated.

## 1. Actual local build identity

### Verified local findings

- `.build.info` marks branch `eu` active (`1`), identifies the CDN product path as `tpr/lazr`, and reports version `1.1.2.1279292`.
- Its active Build Key is `ebd38497ab7dd9895822a094a31c0457`. The corresponding existing `Data\config\eb\d3\ebd38497ab7dd9895822a094a31c0457` Build Config identifies `build-product = lazr` and `build-name = CRC_1279292A_retail_signed_ship`.
- Therefore the supplied product, version, and build name match the active local metadata.
- The inspected metadata does not supply an independently labeled numeric Build ID. The version’s trailing number is not treated here as separate proof of a Build ID.
- Another local Build Config names `CRC_1279145A_signed_ship`; it is not the active build because its key differs from the Build Key in `.build.info`.
- `MW2CR.exe` version resources identify the product as *Call of Duty: Modern Warfare 2 Campaign Remastered* by Activision. Its embedded ProductVersion (`1.1`) and FileVersion (`1.0.0.0`) do not identify the full installed retail build; the active install metadata is the stronger build evidence.

### SHA-256 fingerprints

| Local file | SHA-256 |
|---|---|
| `.build.info` | `8E4876B7287CF4ACD37A8E5A9FB0734D3B85E8DFA3B0FA9D09D1FD724C35784E` |
| `MW2CR.exe` | `B0B9909199051ECF1E24A56F6A6F93F47364549F32164F7698DB789D271D8C57` |
| Active Build Config (`ebd38497ab7dd9895822a094a31c0457`) | `9BC6952F406C86313D406D174B583E42640822BDFBAC06DD97B7FBCAE48FE9F7` |

These hashes identify the inspected files; they do not authenticate them against a publisher-signed reference.

## 2. Arabic support evidence

### Local installation metadata

The active `.build.info` Tags expression includes both `arSA text` and `arSA speech` among its locale-tag alternatives. This is direct evidence that the active build metadata recognizes separate Arabic text and speech installation tags. It is stronger than a language-like filename, but it remains metadata evidence: the install manifest and archive payloads were deliberately not inspected.

The existing CDN configuration files were read as configuration text only. No archive data was opened. The local Build Config references an install manifest by content key, but that manifest was not read because it resides in CASC-managed content. No inspected plaintext local configuration independently enumerated actual Arabic files.

### First-party documentation reviewed

- Activision’s [Modern Warfare 2 Remastered Installation and Setup](https://support.activision.com/modern-warfare-2-remastered/articles/modern-warfare-2-remastered-installation-and-setup) provides installation guidance, but does not establish a PC Arabic language matrix or explain selecting Arabic for this product.
- Activision’s [Modern Warfare 2 Remastered FAQ](https://support.activision.com/au/en/modern-warfare-2-remastered/articles/call-of-duty-modern-warfare-2-faq) confirms product/platform information including Battle.net availability, but does not document Arabic availability or a title-specific language selector.
- The [official product page](https://www.callofduty.com/mw2campaignremastered) does not provide a language list or confirm completeness of Arabic text/audio.

Thus, the product-specific first-party pages reviewed do not independently confirm the scope or completeness of Arabic localization, nor do they document the exact Battle.net controls for adding it. The local locale tags are the best direct evidence found for this build’s declared support.

### Third-party metadata and registry files

Phase 9’s public product-configuration mirror lists `arSA` among `lazr` locales. This is third-party metadata and corroboration only; it is not an Activision or Blizzard support statement. It does not prove local payload presence.

The `_Language Switcher` directory contains `.reg` files. `Arabic.reg`, inspected only as plain text, sets `LOCALE` and `LOCALE_AUDIO` to `arSA` under Blizzard Entertainment Battle.net Launch Options for `LAZR`. This is an unverified third-party registry workaround. The official product documentation reviewed does not document this file, those exact keys, or recommend importing it. Its contents do not establish that the official launcher has downloaded Arabic resources. No registry access or import occurred.

## 3. Battle.net language selection and installation

The local tag expression distinguishes text and speech locales, so those components are represented separately in the build’s installation metadata. This alone does not show what controls the official Battle.net client exposes for this title, whether text and audio can be selected independently in its UI, or whether the client will still offer Arabic downloads for this product today.

The product-specific Activision documentation reviewed does not answer those UI and delivery questions. Consequently:

- **Text/audio separation in metadata:** verified (`arSA text`, `arSA speech`).
- **Separate user-selectable text and audio controls for this product:** not verified.
- **An official Battle.net language-install or repair option that supplies Arabic for this product:** not verified from the reviewed first-party pages.
- **Changing locale by importing the supplied `.reg` file:** not verified as official behavior and not recommended on this evidence.

## 4. Are Arabic resources installed? Is the full localization available?

**Actual Arabic resources installed: not confirmed.** Locale identifiers in `.build.info` indicate declared installation-tag choices, not which tagged content is physically installed. No Arabic content was read or extracted, and filenames alone would not establish resource language.

**Full official Arabic localization: not confirmed.** The presence of separate Arabic text and speech tags suggests metadata coverage for both categories, but does not show that each is populated, that all campaign/UI strings and audio are translated, or that the quality and rendering are complete. The reviewed first-party product pages did not publish a completeness statement or language matrix.

To confirm installed content, a later authorized, controlled inspection would need to establish the active install selection and inventory the relevant manifest/archive entries without changing the game. To establish actual language and completeness, representative text tables, UI strings, fonts/configuration, and audio metadata or samples would need reliable format-aware inspection. This phase did none of that.

## 5. Is a custom extraction pipeline needed?

Not yet. First determine through the official Battle.net client’s documented/current product UI whether it offers Arabic for this specific installation, without changing registry values or using the third-party switcher. If the official client can add Arabic, prefer that path and then verify what it installed. If Arabic is unavailable there, or is only partial, a custom extraction/localization pipeline may be justified after a separately authorized archive-inventory plan. This report does not authorize extraction or replacement.

## Evidence status and open questions

| Question | Status | Evidence / limitation |
|---|---|---|
| Is the installed product `lazr`? | **VERIFIED** | Active `.build.info` product path and matching active Build Config. |
| Is version `1.1.2.1279292` active? | **VERIFIED** | Active `.build.info` Version field. |
| Is build name `CRC_1279292A_retail_signed_ship` active? | **VERIFIED** | Build Config selected by active Build Key. |
| Does active metadata declare Arabic text and speech tags? | **VERIFIED** | `.build.info` Tags expression contains `arSA text` and `arSA speech`. |
| Are actual Arabic assets installed locally? | **NOT VERIFIED** | No install manifest or archive inventory/content inspected. |
| Does the official client currently offer Arabic for this product? | **NOT VERIFIED** | Product-specific first-party docs reviewed do not document this control or delivery state. |
| Is complete campaign text and audio officially available in Arabic? | **NOT VERIFIED** | Locale tags do not establish content coverage; no first-party completeness statement found. |
| Is `_Language Switcher\Arabic.reg` an official method? | **NOT VERIFIED** | Plain-text third-party registry recipe; not documented by reviewed official product pages. |

## Recommended next phase

Use the official Battle.net product UI to check whether Arabic text and/or speech can be selected or installed for this title. Do not use the registry file as proof of support. If the official path is absent or incomplete, the next technical phase should be a narrowly scoped, read-only inventory of install-manifest locale selections and relevant CASC entries, with all work kept outside the game directory and no extraction until parser compatibility and safety are established.
