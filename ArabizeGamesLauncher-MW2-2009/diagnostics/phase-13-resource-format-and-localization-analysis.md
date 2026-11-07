# Phase 13 — Resource Format and Localization Candidate Analysis

## Scope and evidence quality

This is a metadata-only analysis. It reads the Phase 12F CSV and JSON, and local diagnostics/source listings. It does not open the saved VFS blob, any CASC index or shard, or any referenced `.ff`, `.pak`, `.bik`, or other asset payload.

The inventory records 1,536 paths and its JSON says 1,536 TVFS paths parsed with zero duplicates/invalid paths. However, this JSON is from **offline `--from-file` parsing**: it explicitly marks build identity, unique index match, local header, BLTE, and decoded VFS-root key/size validation as false. Its only input was `vfs_root.bin` (57,384 bytes, SHA-256 `f33fdf88c4f3aba85fddf33a59e04688d1fe8d6ac0548437487a735250dfcde8`). This report treats the inventory as the supplied parsed artifact and does **not** verify that its provenance is the stated installed build.

The Phase 12D review still records an unresolved guarded index-entry-block Jenkins hash mismatch as a warning. It does not invalidate this path analysis, but it remains an integrity caveat; the report preserves it and does not treat it as resolved.

## Inventory statistics

Size fields below are the TVFS/CFT `content_size` and `encoded_size` recorded in `referenced_metadata`, not sizes measured by opening the files. Sums are over logical path rows and therefore include aliases that share EKeys or spans. They are not unique on-disk storage totals.

| Extension | Paths | Sum decoded bytes | Sum encoded bytes | Decoded size range | Encoded size range |
|---|---:|---:|---:|---:|---:|
| `.bik` | 59 | 4,069,510,532 | 4,069,900,190 | 13,072–249,038,980 | 13,109–249,062,767 |
| `.cfg` | 1 | 77 | 114 | 77–77 | 114–114 |
| `.dll` | 11 | 10,044,632 | 10,045,864 | 15,360–4,173,928 | 15,397–4,174,340 |
| `.exe` | 1 | 14,803,528 | 14,804,965 | 14,803,528–14,803,528 | 14,804,965–14,804,965 |
| `.ff` | 850 | 15,157,655,896 | 15,159,127,596 | 100–885,724,368 | 137–885,808,855 |
| `.pak` | 585 | 61,078,472,154 | 61,084,311,865 | 23,314–3,968,240,696 | 23,351–3,968,619,194 |
| `.pak_1` | 11 | 10,957,619,200 | 10,958,664,332 | 996,147,200–996,147,200 | 996,242,212–996,242,212 |
| `.pak_2` | 11 | 6,641,982,678 | 6,642,616,335 | 63,317,212–996,147,200 | 63,323,274–996,242,212 |
| `.pak_3` | 4 | 3,493,820,488 | 3,494,153,736 | 505,378,888–996,147,200 | 505,427,100–996,242,212 |
| `.pak_4` | 3 | 1,339,605,404 | 1,339,733,215 | 95,140,428–979,799,096 | 95,149,515–979,892,558 |

### Directory prefixes

| First path component | Paths | Sum decoded bytes | Sum encoded bytes |
|---|---:|---:|---:|
| `[root]` | 1,308 | 99,453,915,276 | 99,463,436,763 |
| `english` | 76 | 1,060,879,531 | 1,060,982,893 |
| `russian` | 76 | 1,145,767,197 | 1,145,878,609 |
| `spanish` | 76 | 1,102,952,585 | 1,103,059,947 |

### Language-like filename prefixes

Seventeen filename-prefix groups contain 76 paths each:

| Prefix group | Paths | Sum decoded bytes | Sum encoded bytes |
|---|---:|---:|---:|
| `arara` | 76 | 1,087,960,311 | 1,088,066,248 |
| `english/eng` | 76 | 1,060,879,531 | 1,060,982,893 |
| `englishens` | 76 | 1,060,879,303 | 1,060,982,665 |
| `ffra` | 76 | 1,062,777,132 | 1,062,880,669 |
| `gdeu` | 76 | 1,079,231,405 | 1,079,336,567 |
| `iita` | 76 | 1,161,502,221 | 1,161,615,183 |
| `japanese_jpf` | 76 | 1,104,823,604 | 1,104,931,166 |
| `japanese_jpp` | 76 | 1,067,008,695 | 1,067,112,632 |
| `kor` | 76 | 1,087,750,843 | 1,087,856,755 |
| `popol` | 76 | 1,087,846,215 | 1,087,952,152 |
| `popor` | 76 | 1,060,913,451 | 1,061,016,813 |
| `russian/rus` | 76 | 1,145,767,197 | 1,145,878,609 |
| `russianrup` | 76 | 1,087,912,439 | 1,088,018,376 |
| `simplsch` | 76 | 1,074,230,891 | 1,074,335,528 |
| `spanish/spa` | 76 | 1,102,952,585 | 1,103,059,947 |
| `spanishsna` | 76 | 1,060,901,635 | 1,061,004,997 |
| `tratch` | 76 | 1,074,427,723 | 1,074,532,385 |

These names are metadata clues only; they are not proof that each group is fully localized or selected by the installed game. No `arSA` tag is present on individual CSV rows.

The `arara` group has 76 paths: 46 `.ff` paths (including UI, graphics, mission/common/ending and patch-style names) and 30 `soundfile*.pak` paths.

### Filename suffixes

Using longest-suffix categorization:

| Suffix | Paths | Sum decoded bytes | Sum encoded bytes |
|---|---:|---:|---:|
| `_common` | 36 | 4,860,532 | 4,862,164 |
| `_load` | 21 | 2,507,640,296 | 2,507,880,023 |
| `_ui` | 18 | 6,348 | 7,014 |
| `_patch_ui` | 18 | 104,584 | 105,250 |

The UI groups include `techsets_ui.ff` and the generic `patch_ui.ff` in addition to language-prefixed paths. Suffixes suggest areas to investigate; they do not identify the internal asset class.

### Duplicate metadata

- **61 EKey groups** contain duplicate EKeys, covering **695 path rows**.
- **95 span-signature groups** (same `span_offset`, `content_size`, and `encoded_size`) cover **816 path rows**.
- Example: all 17 localized-name `*_patch_ui.ff` paths have EKey `2bc1859b729e444b33` and the same 2,304/2,341 byte sizes. They are aliases of one CASC encoded resource according to this inventory, not 17 separately evidenced UI payloads.
- A repeated EKey/span is metadata-level sharing. Since payload bytes and content keys were not read, this phase does not independently compare file contents.

## Ranked localization candidates

The complete ranked list is in `tools\install-manifest-prototype\out\phase12f\localization_candidates.csv` (238 rows). Every row includes exact path, extension, decoded and encoded sizes, EKey, selection reason, confidence, what the name supports, and what it cannot establish. Confidence describes candidate relevance, not proof of Arabic content.

| Rank | Exact path | Type by name | Decoded / encoded bytes | EKey | Confidence and filename limits |
|---:|---|---|---:|---|---|
| 1 | `arara_ui.ff` | `.ff`, UI-named | 248 / 285 | `35f7e7860eb29404ca` | Medium as an interface candidate; low for Arabic content. `arara` + `_ui` is a strong name lead, but the 248-byte size and filename do not prove it contains strings or is the complete UI. |
| 2 | `arara_common.ff` | `.ff`, common-named | 21,336 / 21,373 | `b17e78b82f91526878` | Medium as shared localized content. The filename does not identify actual text, subtitles, or format. |
| 2 | `arara_af_caves.ff` | `.ff`, mission-named | 118,076 / 118,113 | `9faf7cfad2ec7e3c00` | Medium as a campaign-resource lead. The name does not prove that it contains mission text or Arabic strings. |
| 2 | `arara_airport.ff` | `.ff`, mission-named | 99,740 / 99,777 | `8d3f975b187626e060` | Medium as a campaign-resource lead. The name does not prove resource type or localization coverage. |
| 2 | `arara_ending.ff` | `.ff`, ending-named | 72,008 / 72,045 | `8bda12ef9d13d0f003` | Medium as campaign/ending content. It does not prove subtitles or localized narrative text. |
| 3 | `arara_patch_ui.ff` | `.ff`, patch UI-named | 2,304 / 2,341 | `2bc1859b729e444b33` | Medium as UI data, low as an Arabic-specific payload: the same EKey is listed for all 17 locale-named patch UI paths. |
| 4 | `arara_soundfile4.pak` | `.pak`, soundfile-named | 321,820,101 / 321,850,813 | `0de48ddf0680f54c8a` | Low for Arabic voice. Harmony's earlier MW2CR notes report FLAC streams in soundfile PAKs from an unspecified build; this row's name alone does not establish language or dialogue/music role. |
| 4 | `arara_soundfile10.pak` | `.pak`, soundfile-named | 174,462,229 / 174,478,891 | `d2edb627214d05eb4c` | Low for Arabic voice, medium as a sound-package lead. No payload or audio metadata was inspected. |
| 5 | `english/eng_ui.ff` | `.ff`, UI-named control | 248 / 285 | `429c03d9cb33cc4c18` | Medium as a comparison candidate. `english/eng` suggests an English-named variant, but no content comparison was made. |
| 5 | `ui.ff` | `.ff`, generic UI-named | 4,963,472 / 4,963,959 | `9a1e21fa21caf00ae2` | Medium as a generic UI lead; no locale or internal asset type is established by its name. |
| 5 | `patch_ui.ff` | `.ff`, generic patch/UI-named | 65,416 / 65,453 | `467ebf277496210be8` | Medium as a UI/patch lead; not evidence of Arabic or a replacement destination. |
| 5 | `techsets_ui.ff` | `.ff`, UI/techset-named | 2,132 / 2,169 | `400d0587c96993385c` | Low as a font lead. The name suggests UI techsets, not font contents or glyph coverage. |
| 6 | `af_caves_load.bik` | `.bik`, load-named | 158,083,476 / 158,098,588 | `e3b195b4a64e9ad4cf` | Low subtitle/cinematic lead. The name suggests a loading-related asset; it does not establish Bink format, embedded audio, subtitles, or language. |

The CSV also includes the other 59 `.bik` paths and 68 graphics-named `.ff` candidates. These are low-confidence filename leads, not format identifications.

## Localization, audio, and rendering areas

- **UI text:** `arara_ui.ff`, `arara_patch_ui.ff`, locale-named UI paths, `ui.ff`, and `patch_ui.ff` are the highest-priority names. The shared patch UI EKey is a warning against treating each locale pathname as a distinct localized payload. No string table or UI asset was opened.
- **Campaign text/subtitles:** `arara_*` mission/common/ending `.ff` paths are plausible places to look after H2 format support is established. No filename explicitly identifies `localize`, `stringtable`, `subtitle`, `dialogue`, or `font` resources in this inventory. This absence does not prove those resources are absent; they may be embedded inside `.ff` or `.pak` payloads.
- **Dialogue/audio:** thirty `arara_soundfile*.pak` rows are audio-package candidates by name. The prior Harmony report provides family-level evidence that soundfile PAKs in an unspecified MW2CR installation contained FLAC streams. It does not establish that these exact PAKs are Arabic speech, dialogue, or even the same build.
- **Music:** no explicit music-named resource was identified. Music may be in unnamed sound packages; contents are unknown.
- **Cinematics:** 59 `.bik` paths include 21 `_load` names, 20 `mission_select_bg_` names, 6 other cinematic-like names, and 12 other names. These strings are leads only. No `.bik` container/audio/subtitle format was established from available local sources.
- **Fonts/glyphs:** no path explicitly names a font or TTF resource. `techsets_ui.ff` and graphics-named files are weak leads only. Glyph coverage, shaping, bidi/right-to-left behavior, mixed Arabic/Latin, and UI layout remain unknown.

## Resource format evidence and confidence

| Resource family | Available local evidence | Conclusion for this build |
|---|---|---|
| `.ff` | Phase 9's saved static audit says prior Jekyll/ZoneTool source exposed some H2 asset types, but neither source tree is present at its previously reviewed compressed-download path now. The prior report also says runtime memory/hooks were involved and exact retail-build compatibility was not established. CascLib source handles CASC/TVFS, not H2 zone internals. | **UNKNOWN** whether these exact `.ff` files use a directly parseable H2 zone/fastfile structure, which version/signature/compression applies, or which assets they contain. Extension is insufficient evidence. |
| `.pak` / `.pak_1`–`.pak_4` | The saved Phase 4 report records Harmony 0.1.0's self-reported MW2CR observations: soundfile PAKs contained FLAC streams in a build-unspecified installation. No Harmony source tree is present locally now. `.pak_1`–`.pak_4` filenames and size patterns can be segmented-file clues only. | **POTENTIAL FAMILY-LEVEL LEAD; exact-build format UNKNOWN.** Cannot tell whether a given PAK is a standalone archive, wrapper, sound bank, or segmented CASC logical file without payload/parser evidence. |
| `.bik` | No relevant Bink/Bink container parser or format documentation was found in the available local source/docs searched. | **UNKNOWN.** Do not infer Bink structure, codec, audio track, or subtitles from `.bik` alone. |
| CASC/TVFS metadata | Phase 12F records path, EKey, span/content size, and CFT encoded size. Its execution report is offline and marks game/build/BLTE identity checks false. | **Metadata parsed; provenance to the installed build NOT VERIFIED by this report.** These fields do not expose inner resource type, compression mode, encryption, or text/audio language. |

The present local folders `CASCExplorer` and `CascLib-master` do not supply a local H2 asset parser. `CASCExplorer` is a binary/dependency directory. A read-only search of CascLib source found no `.ff`, `.pak`, Bink, or H2 parser declarations; the only sound-file matches were unrelated Diablo III `.smp` examples. The previously referenced local Harmony, Jekyll, and x64 ZoneTool source directories were not present at their checked paths.

## Risks and unresolved questions

1. The offline TVFS JSON validates path parsing only, not the inventory's connection to product `lazr` build `1.1.2.1279292`.
2. The earlier guarded-block Jenkins mismatch warning remains unresolved; the report does not silently discard it.
3. `arara` is not demonstrated here to be the official `arSA` tag. Language-like prefixes can be internal grouping names or aliases.
4. The EKey is a CASC encoded key, not a payload hash supplied here. The inventory has no content key for these rows; payload identity was not independently checked.
5. Sums over paths double count aliases; duplicate EKeys/spans show sharing but do not provide unique physical storage totals.
6. No evidence establishes Arabic voice, complete translation coverage, font glyphs, Arabic shaping, bidi, or a supported replacement mechanism.
7. The TVFS format inventory does not reveal whether `.ff`, `.pak`, or `.bik` contents are encrypted or compressed internally.

## Recommended next controlled experiment

First restore or obtain a source-only copy of the proposed H2 parser and review its provenance, exact supported versions, file-read behavior, and output behavior without executing it. If that review supports a bounded standalone parser, the first later test should compare the small `arara_ui.ff` (248 decoded bytes) with `english/eng_ui.ff` (248 bytes), then inspect `arara_common.ff` against its English counterpart. Keep all inputs as verified copies and every output in a separate workspace; inspect headers/inventory before asset extraction. Do not start with the large soundfile PAKs or use memory hooks. This recommendation authorizes no extraction or game modification in Phase 13.

## Files read or created; execution and access statement

### Files read

- `tools\install-manifest-prototype\out\phase12f\vfs_resource_inventory.csv`
- `tools\install-manifest-prototype\out\phase12f\execution_report.json`
- `tools\install-manifest-prototype\out\phase12f\localization_candidates.csv` (read back after creation to check its header and initial rows)
- Project diagnostics searched for format/localization evidence: `arabic-localization-resource-audit.md`, `arabic-rendering-feasibility.md`, `casc-archive-inventory.md`, `mw2-campaign-remastered-archive-investigation.md`, `mw2-campaign-remastered-diagnostic-report.json`, `mw2-campaign-remastered-localization-feasibility.md`, `phase-10-official-arabic-localization-and-local-build.md`, `phase-11-casc-manifest-locale-inventory.md`, `phase-12a-downloaded-toolchain-inventory.md`, `phase-12b-casclib-provenance-and-tag-parser-review.md`, `phase-12b-install-manifest-parser-synthetic-validation.md`, `phase-12d-install-manifest-extraction.md`, `phase-12d-script-review.md`, `phase-12f-tvfs-parser-and-inventory-implementation.md`, `phase-3-safety-and-integrity-report.md`, `phase-4-casc-compatibility-research.md`, `phase-9-mw2cr-arabic-localization-technical-discovery.md`, and `test-results.md`.
- CascLib source tree was searched read-only for `.ff`, `.pak`, Bink, and FastFile references; the only text match was `src\CascRootFile_Diablo3.cpp` with unrelated Diablo III `.smp` examples. `CASCExplorer` was file-listed only; no binary was executed.
- Checked the expected local paths for Harmony, Jekyll, and x64 ZoneTool source directories; they were absent. No source files in those directories were read.

### Files created

- `diagnostics\phase-13-resource-format-and-localization-analysis.md`
- `tools\install-manifest-prototype\out\phase12f\localization_candidates.csv`

Read-only PowerShell commands were executed to read and aggregate the two inventory files, search local diagnostics/source filenames and text, and create the candidate CSV. No game-resource tool, parser, executable, game process, or test was executed. The game directory was not accessed. No asset payload was opened, read, extracted, or decoded. No existing launcher or Phase 12D/12F implementation was modified.
