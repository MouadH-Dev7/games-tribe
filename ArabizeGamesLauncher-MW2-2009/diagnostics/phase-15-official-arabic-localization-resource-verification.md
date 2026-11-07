# Phase 15 — Official Arabic Localization Resource Verification

## Executive verdict

**Arabic UI and dialogue subtitles are CONFIRMED as working in the user's reported test.** The user reports that after selecting Arabic, menus/UI and dialogue subtitles appeared in Arabic while character voices remained English. This is runtime evidence as reported by the user; it was not reproduced in this read-only phase.

**Arabic resource paths are STRONGLY INDICATED in the saved VFS inventory, but their connection to the active installed build is UNKNOWN.** The inventory contains 76 `arara`-prefixed logical paths: 46 `.ff` and 30 `soundfile*.pak`. However, the saved `execution_report.json` identifies itself as an offline parse and marks build identity, index match, local header, BLTE, and decoded-root hash validation false. The Phase 12F implementation report also says its game-path script was not executed. Therefore these paths cannot be represented as independently verified contents of the installed `lazr` build on the evidence currently saved.

**Arabic voice availability is UNKNOWN.** Thirty Arabic-named sound-package paths are listed, but neither the package contents nor their language/audio roles were examined. The user's test heard English character voices; that confirms what played in that test, not that Arabic voice resources are absent.

**Standalone H2 parser investigation should remain PAUSED** for localization activation. It is not needed to use the already-working Arabic UI/subtitle configuration. A parser or official asset inspection tool would be needed to inspect/reuse `.ff`/`.pak` contents, determine whether Arabic speech assets exist, or verify payload identity and completeness.

## Evidence and confidence

| Source | Observation | Status / confidence |
|---|---|---|
| User's Phase 15 instructions / reported test | Arabic menus/UI and dialogue subtitles displayed; character voices remained English. | **CONFIRMED as user-reported runtime behavior**; not independently reproduced in this phase. |
| `diagnostics/phase-10-official-arabic-localization-and-local-build.md:8–16, 33–39` | Earlier read-only report identifies active product `lazr`, version `1.1.2.1279292`, build `CRC_1279292A_retail_signed_ship`; `.build.info` tags included conditional alternatives `arSA text` and `arSA speech`. | **CONFIRMED as prior local metadata finding**; declares locale options, not installed payload selection. |
| Phase 10 report `:53, 57–70` | `Arabic.reg` was inspected as text; it set `LOCALE=arSA` and `LOCALE_AUDIO=arSA` for LAZR. It was characterized as an undocumented third-party workaround. The report did not inspect actual content or establish official launcher behavior. | **CONFIRMED as prior file-text observation**; official status and internal effect **UNKNOWN**. No registry was read in Phase 15. |
| `tools/install-manifest-prototype/out/phase12f/vfs_resource_inventory.csv` | Contains 76 paths beginning `arara`: 46 `.ff`, 30 `soundfile*.pak`; includes locale-like names and path/size/EKey metadata. | **STRONGLY INDICATED** as entries in the saved TVFS-root-derived inventory; not proof of language or installed-build provenance. |
| `tools/install-manifest-prototype/out/phase12f/execution_report.json` | Status is `offline TVFS parse; no game directory opened`; `active_build` is `NOT VERIFIED`. `validation.build_identity`, `unique_index_match`, `local_header`, `BLTE_chunk_hashes`, and `decoded_vfs_root_MD5_and_size` are false; `TVFS_bounds_and_paths` is true; `referenced_assets_read` is false. | **CONFIRMED** for this saved offline report. It validates path parsing only, not the root's link to the installed build. |
| `diagnostics/phase-12f-tvfs-parser-and-inventory-implementation.md:5–7, 105–120` | States that the script and synthetic tests were not executed in that phase, and that inventory parsing does not read asset payloads or infer locale/content. | **CONFIRMED** report limitation; conflicts with any assumption that saved CSV alone is an end-to-end installed-build verification. |
| `diagnostics/phase-13-resource-format-and-localization-analysis.md:40–63, 87–114, 118–135` | Summarizes the `arara` group and candidate ranks; explicitly says `arara` is not proven to mean `arSA`, soundfile PAK contents are unknown, and inventory provenance to the active build is unverified. | **STRONGLY INDICATED** as metadata-level leads; payload facts remain **UNKNOWN**. |
| Phase 10 report `:43–47, 61–64, 82–93` | The official documentation reviewed did not establish a product-specific PC Arabic selector/download path; separate text/audio UI controls and a language repair/install option remained unverified. | **UNKNOWN** whether Battle.net currently offers Arabic installation/selection for this product. |

### Evidence interpretation

The conditional `.build.info` tags are **Arabic language registration in installation metadata**. They are not proof that Arabic files are installed. The `arara` names are **locale-like paths in the saved VFS inventory**; no row has an `arSA` tag, and the Phase 13 report says the mapping from `arara` to `arSA` is unproven. The reported Arabic UI/subtitles are **runtime behavior** and are the strongest evidence that text localization works in the user's current setup. None of these observations proves that each named asset's payload is Arabic, that the saved VFS root belongs to the verified local build, or that the complete campaign has Arabic coverage.

## Arabic UI and text-resource inventory

All sizes below are logical decoded/encoded sizes recorded in `vfs_resource_inventory.csv` metadata, not files read from disk. EKeys are identifiers from the VFS metadata; they are not payload hashes verified in Phase 15.

| Logical path | Decoded / encoded bytes | EKey | Observation |
|---|---:|---|---|
| `arara_ui.ff` | 248 / 285 | `35f7e7860eb29404ca` | UI-named candidate. Same decoded size as `english/eng_ui.ff` but different EKey. The contents were not compared. |
| `english/eng_ui.ff` | 248 / 285 | `429c03d9cb33cc4c18` | English-named comparison candidate. |
| `arara_patch_ui.ff` | 2,304 / 2,341 | `2bc1859b729e444b33` | Patch UI candidate. The same EKey is listed for the locale-named patch UI paths, including `english/eng_patch_ui.ff`; this suggests shared referenced content, but no payload bytes were checked. |
| `english/eng_patch_ui.ff` | 2,304 / 2,341 | `2bc1859b729e444b33` | Same metadata EKey and sizes as `arara_patch_ui.ff`. |
| `arara_common.ff` | 21,336 / 21,373 | `b17e78b82f91526878` | Shared/common candidate; no text or subtitle payload was read. |
| `english/eng_common.ff` | 19,544 / 19,581 | `97cccb39c0ebb09564` | English-named common comparison candidate; size/EKey differ from Arabic-named common candidate. |
| `arara_patch_common.ff` | 68,924 / 68,961 | `2cdf96fd7cfb65b475` | Patch/common candidate. |
| `english/eng_patch_common.ff` | 61,624 / 61,661 | `22579a65e79ca7482e` | English-named patch/common comparison candidate. |

The inventory contains 46 `arara_*.ff` paths, totaling 30,112,336 logical decoded bytes by path sum (aliases may refer to shared content). They include UI/patch UI, common/patch common, graphics, campaign/mission, ending, roadkill, and trainer-named resources. Campaign-named examples include `arara_af_caves.ff`, `arara_airport.ff`, `arara_dc_whitehouse.ff`, `arara_ending.ff`, `arara_gulag.ff`, and their `arara_patch_*` counterparts. The full exact path and size list remains in the saved inventory and ranked candidate CSV; no additional asset file was opened.

No inventory path explicitly identifies a `localize`, `stringtable`, `subtitle`, `dialogue`, `font`, or `ttf` payload. This is **NOT PRESENT IN VERIFIED INVENTORY** as a named path; such resources could be embedded inside the listed `.ff` or `.pak` files.

## Arabic audio-related inventory

The saved inventory lists 30 paths named `arara_soundfile*.pak`, with 1,057,847,975 logical decoded bytes total by path sum. They are audio-package leads based on naming only. No package was opened, extracted, decoded, played, or classified by internal audio metadata.

| Logical path | Decoded bytes |
|---|---:|
| `arara_soundfile1.pak` | 31,578 |
| `arara_soundfile2.pak` | 64,136 |
| `arara_soundfile4.pak` | 321,820,101 |
| `arara_soundfile6.pak` | 304,656 |
| `arara_soundfile8.pak` | 120,536,403 |
| `arara_soundfile10.pak` | 174,462,229 |
| `arara_soundfile12.pak` | 4,851,367 |
| `arara_soundfile16.pak` | 1,257,313 |
| `arara_soundfile22.pak` | 3,097,743 |
| `arara_soundfile24.pak` | 33,288 |
| `arara_soundfile26.pak` | 57,941 |
| `arara_soundfile34.pak` | 14,813,727 |
| `arara_soundfile36.pak` | 16,048,135 |
| `arara_soundfile60.pak` | 49,649,308 |
| `arara_soundfile61.pak` | 23,422,011 |
| `arara_soundfile62.pak` | 35,589,674 |
| `arara_soundfile63.pak` | 5,821,742 |
| `arara_soundfile64.pak` | 11,988,527 |
| `arara_soundfile65.pak` | 23,621,710 |
| `arara_soundfile66.pak` | 9,554,243 |
| `arara_soundfile67.pak` | 16,126,460 |
| `arara_soundfile68.pak` | 25,525,117 |
| `arara_soundfile69.pak` | 15,515,817 |
| `arara_soundfile70.pak` | 66,281,529 |
| `arara_soundfile71.pak` | 23,567,864 |
| `arara_soundfile72.pak` | 16,409,574 |
| `arara_soundfile73.pak` | 18,828,571 |
| `arara_soundfile74.pak` | 22,403,570 |
| `arara_soundfile75.pak` | 6,947,500 |
| `arara_soundfile76.pak` | 29,216,141 |

The names and sizes do not reveal whether these are dialogue, music, effects, or mixed sound packages. Phase 4/Harmony notes and Phase 13 cite a prior family-level report that soundfile PAKs from an unspecified MW2CR installation held FLAC streams. That does not identify the contents or language of these exact paths. **Arabic voice availability is UNKNOWN**, not confirmed and not absent from the verified inventory.

## Existing Arabic activation mechanism

Phase 10's plain-text inspection of `_Language Switcher/Arabic.reg` recorded `LOCALE=arSA` and `LOCALE_AUDIO=arSA` for LAZR. The user says the file was imported and that Arabic text is now working. The successful display of Arabic UI/subtitles shows that the current game setup reached an Arabic text rendering path.

The registry values themselves do not show whether the game reads both keys, whether Battle.net downloads or selects separate payload groups from them, whether audio is independently selectable, or whether `LOCALE_AUDIO` was honored. The English voices heard during the test indicate that Arabic voice audio was not playing in that session. The reason is unknown. The previous report classifies the `.reg` mechanism as third-party and undocumented by the official product pages it reviewed; do not treat it as verified official Battle.net behavior. Phase 15 did not query or modify the Registry.

## Reassessing the H2 parser and launcher

- **Needed to activate the already-working Arabic UI/subtitles? No.** The user has already demonstrated the current setup at runtime. Inner `.ff` parsing is not required merely to select an existing locale.
- **Needed to inspect/reuse specific Arabic resources or prove completeness? Yes, unless an official tool exposes equivalent content metadata.** Filenames, sizes and EKeys cannot establish payload contents, text coverage, audio language, or whether resources match this build.
- **Needed to investigate why voices were English? A parser may help, but first verify official text/audio locale selection and installed package state.** Do not start with large sound packages; a parser would need to identify their structure and content safely before inspection.
- **Can a launcher support an existing official localization without parsing `.ff`? Architecturally, yes**, if it uses a verified, supported locale-selection mechanism and leaves the game assets intact. The current evidence does not verify an official Battle.net selector or prove that the two recorded registry values are an official API. No locale adapter should be implemented on the assumption that `Arabic.reg` is official or that both keys work.

Accordingly, keep the H2 parser investigation **PAUSED** for locale activation. Resume it only if the project needs to inspect/reuse payloads, verify voice content, or independently validate resource completeness. For the multi-game launcher, the next phase should define a declarative locale-adapter capability boundary: per-game supported locale identifiers, text/audio independence where verified, an official selection mechanism reference, and a read-only detection mode. Keep the Campaign Remastered adapter unavailable until its official selection path is confirmed; do not write registry values in this phase.

## Proven versus unknown

| Question | Status |
|---|---|
| Did the user report Arabic menus and dialogue subtitles working? | **CONFIRMED** as reported runtime observation. |
| Does the prior local build metadata include Arabic text/speech locale alternatives? | **CONFIRMED** by Phase 10's recorded `.build.info` finding. |
| Does the saved VFS inventory contain Arabic-like `arara` path families? | **STRONGLY INDICATED** by 76 paths in the saved metadata inventory. |
| Is the saved VFS root independently tied to the active local build in the saved execution report? | **UNKNOWN / NOT VERIFIED**; report validation flags are false and root provenance is external to the offline parse. |
| Are `arara_*` payloads confirmed to contain Arabic? | **UNKNOWN**; no asset payloads were read. |
| Are Arabic speech assets present and selected? | **UNKNOWN**; soundfile names are not content classification, and the user's test played English voices. |
| Are Arabic fonts/glyphs, shaping, bidi, and full campaign coverage confirmed? | **UNKNOWN**. |
| Does official Battle.net currently offer an Arabic install/selection option for this product? | **UNKNOWN** from Phase 10's reviewed evidence. |
| Is the third-party registry workaround officially supported? | **UNKNOWN / NOT VERIFIED**. |

## Safety and activity record

- Read existing project reports and the Phase 12F CSV/JSON inventory metadata only.
- Did not open `vfs_root.bin` during Phase 15; used the saved parsed inventory and its execution report.
- Did not access the game installation directory, `.build.info`, CASC indices, or data shards in Phase 15.
- Did not open, read, extract, decode, or play any `.ff`, `.pak`, `.bik`, or other game asset payload.
- Did not inspect the live Registry, import a `.reg` file, launch the game, attach to a process, run tools, access the network, or install dependencies.
- Created only `diagnostics/phase-15-official-arabic-localization-resource-verification.md`. No launcher, script, game, or registry file was changed.

**Phase 15 status: COMPLETE as a read-only evidence review.** The user-reported Arabic text behavior is confirmed as a report; actual resource payload contents, inventory-to-installed-build provenance, Arabic speech availability, and official PC language-selection behavior remain unresolved.
