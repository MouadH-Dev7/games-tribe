# Phase 9 — MW2CR Arabic Localization: Technical Discovery and Toolchain Plan

**Date:** 2026-11-03  
**Target:** Call of Duty: Modern Warfare 2 Campaign Remastered (PC), product `lazr`, supplied build `1.1.2.1279292`, build name `CRC_1279292A_retail_signed_ship`, executable `MW2CR.exe`  
**Scope:** Research, design, and static review only. No game files, registry, process memory, or archive contents were accessed. No candidate tool was run, built, or downloaded.  
**Evidence labels:** CONFIRMED means directly supported by source/docs reviewed; LIKELY is a constrained inference; UNVERIFIED lacks target-build evidence; BLOCKED cannot be established under this phase’s restrictions.

## 1. Executive summary

A maintainable localization path is technically plausible, but no reviewed end-to-end pipeline is currently validated for this exact build. Split the problem into two independent format layers:

1. **CASC transport:** read the Battle.net installation metadata, indexes, encoding table, TVFS path tree, and BLTE payloads to identify and retrieve logical files.
2. **H2 asset/zone layer:** parse the Call of Duty asset containers inside the logical files and interpret/rebuild Localize, StringTable, RawFile, font, and UI assets.

General CASC tools address layer 1; they do not understand Call of Duty H2 assets. ZoneTool understands H2 assets but its inspected H2 workflow loads a modified game binary and relies on runtime hooks, so it is not acceptable as a non-invasive offline extractor. Jekyll exports some relevant assets but reads live process memory and has no exact-build validation. The newer CoD FastFile tooling found in research targets the original 2009 MW2 and other older FastFile generations, not MW2 Campaign Remastered; it is not evidence of H2 support.

A strong new lead changes the localization question: a currently indexed mirror of Blizzard’s `lazr` product configuration lists `arSA` among supported locales. That suggests official Arabic-locale content may exist and should be preferred over inventing replacement strings. This does **not** prove Arabic resources are present in this installation, that all player-visible campaign text is localized, or that local arbitrary language packages can be installed. The supplied version is also not the current version shown by the tracker: it lists `1.1.3.1279292` / build ID `1279293`, not the supplied `1.1.2.1279292`. Exact-build references remain unresolved.

**Decision:** `REQUIRES ADDITIONAL RESEARCH`  
**Next phase proposal:** Phase 10 should be a design-only specification for a read-only CASC inventory prototype and H2 standalone parser test corpus. Do not start implementation or inspect the target archives until exact-build references, parser source provenance, and test isolation are defined.

## 2. Current feasibility

- CASC metadata inventory is a feasible first step with a well-vetted reader, subject to exact `lazr`/build compatibility testing and protecting the source storage from writes.
- Parsing H2 zones offline is plausible but not yet established for this exact build from public evidence reviewed. It needs its own format investigation.
- Existing H2-aware tooling demonstrates asset-type knowledge, but the available x64 ZoneTool source is coupled to game startup and runtime modification.
- Translation management, glossary, QA, and export formats are straightforward independent components and should be developed only after the asset schema is grounded in real, legally obtained sample data.
- Rebuilding and reinsertion are the highest-risk steps. CASC is a content-addressed installation store; modifying individual archive bytes in place is not a safe replacement strategy. A supported loose-file override or separately loadable package has not been verified for the official retail launcher/game.

## 3. CASC and H2 format findings

### CASC transport

A local CASC read path generally resolves `.build.info` to a build configuration, then uses a local `.idx` to locate encoding keys in `data.NNN` shards. The build configuration names the encoding and TVFS root; the encoding table maps content keys to encoding keys; TVFS maps logical paths to content keys/spans; BLTE describes stored blocks and compression/encryption. CascLib describes itself as a reader for Blizzard CASC storages. CascView’s author describes it as a browser/extractor for Blizzard games and explicitly says it does not edit CASC storage. These are useful transport capabilities, not evidence of `lazr` exact-build support. [CascLib upstream](https://github.com/ladislav-zezula/CascLib), [CascView/CASC documentation](https://www.zezula.net/en/casc/main.html)

A metadata-only listing can enumerate paths without writing extracted files. Reading file bytes requires decoding BLTE. Common unencrypted blocks may be readable without keys; encrypted BLTE blocks require appropriate keys. A tool’s ability to open a generic CASC store does not prove the exact `.idx`, encoding, TVFS, or product-specific content path works for this build. CascLib’s source also documents limitations such as supported encoding key sizes; it must be verified against representative `lazr` metadata before selection. [CascLib encoding implementation](https://github.com/ladislav-zezula/CascLib/blob/master/src/CascOpenStorage.cpp)

The public `lazr` product-config page mirrored by BlizzTrack lists `arSA` in `supported_locales` and `lazr` as the product. This is evidence that Battle.net product metadata recognizes an Arabic locale, not proof of the user’s installed locale set, extracted Arabic files, full campaign text coverage, or permission to synthesize a custom package. [BlizzTrack `lazr` product config](https://blizztrack.com/config/lazr/pc/ccd6442568d0947402f75ab067051d07)

### H2 / FastFile assets

MW2CR H2 assets are a separate engine-specific layer. The locally reviewed ZoneTool source defines H2 asset types and dump handlers for `localize`, `rawfile`, `stringtable`, TTF, menu, and menulist, but the H2 executable bootstrap and components patch/hook the game at startup. Aurora’s documentation describes using x64 ZoneTool to dump zones and documents H2 usage, but does not establish exact retail-build compatibility or a static/offline mode. [ZoneTool basics](https://docs.auroramod.dev/zonetool-basics), [Aurora H2 map-porting guide](https://docs.auroramod.dev/map-porting-s1)

Do not conflate these with original MW2 (2009) PC/console FastFiles. CoD-FF-Tools documents the older MW2 version IDs, signed/unsigned layouts, and some editable `localize`/`rawfile` support, but it explicitly scopes the tool to CoD4, WaW, and the 2009 MW2 across older platforms. That is not proof of Campaign Remastered/H2 support. OpenAssetTools likewise describes support for older IW3/IW4/IW5 and Treyarch titles; it is not a verified H2 parser. [CoD-FF-Tools supported formats](https://github.com/primetime43/CoD-FF-Tools/blob/main/docs/SupportedFormats.md), [OpenAssetTools](https://openassettools.dev/)

### Compression, encryption, and rebuild

- CASC BLTE compression is distinct from H2 zone/fastfile compression. Each layer needs independent format validation.
- If a CASC file payload is encrypted, the necessary key may be unavailable. No bypass or key acquisition is in scope.
- FastFile signing/encryption behavior for this exact H2 retail build is **UNVERIFIED**. Older MW2 PC signing notes cannot be transferred to H2.
- Rebuilding an asset is not the same as making the game load it. No validated stock mechanism was found that lets an external installer register arbitrary localization resources with retail MW2CR without modifying game state or relying on hooks.
- CASC’s content keys, archive indexes, and build manifests make naive in-place byte replacement unsafe. A custom installer must not alter these stores unless a fully understood and reversible official update mechanism is proven.

## 4. Existing tool evaluation

| Candidate | Repository / author / license / update evidence | Relevant capability | Exact target fit and safety conclusion |
|---|---|---|---|
| CascLib | [ladislav-zezula/CascLib](https://github.com/ladislav-zezula/CascLib), author Ladislav Zezula; MIT. Upstream says it has existed since 2014 and repository history is active. | Generic CASC storage reader and test program; transport-level path/list/read operations. | **Potential transport candidate only.** No documented `lazr` or exact-build confirmation found. No H2 asset parser or rewriter. Verify file-open behavior and restrict to read-only copies/overlays before use. |
| CascView | [Official CascView/CASC page](https://www.zezula.net/en/casc/main.html), by the CascLib author; version 2.4 listed on the official page. | GUI browsing/search/extraction for Blizzard CASC storage; author states it does not modify CASC stores. | Potential for transport inventory after controlled compatibility test; not an H2 parser. Tool is distributed as binaries, so source-level provenance/security review is more limited. No exact `lazr` build evidence located. |
| Jekyll | [EthanC/Jekyll](https://github.com/EthanC/Jekyll), EthanC; MIT; GitHub says repository archived Jan. 24, 2022. | H2 process-memory exporters for Localize, StringTable, RawFile, TTF. | Rejected for this architecture: requires a running game, uses unchecked memory reads and unsafe output-path construction; no exact build validation. |
| x64 ZoneTool | [Joelrau/x64-zt](https://github.com/Joelrau/x64-zt), Joelrau; GPL-3.0; active repository history/2026 issue activity. | H2 zone loading, asset listing/dumping, handlers for requested asset types, plus rebuilding/porting workflows. | H2 knowledge is useful as format reference, but not suitable as a non-invasive static extractor. H2 startup patches/hooks game memory and has no verified read-only CASC mode or exact-build gate. |
| CoD-FF-Tools | [primetime43/CoD-FF-Tools](https://github.com/primetime43/CoD-FF-Tools), primetime43; license is listed in its repository; current docs describe active feature work. | Older MW2 PC FastFile parsing/editing, including localize/rawfile, plus other old-game assets. | **Not the target.** Supports original 2009 MW2 format, not H2/Campaign Remastered. Near-name match is misleading. |
| OpenAssetTools | [Laupetin/OpenAssetTools](https://github.com/Laupetin/OpenAssetTools), Laupetin; GPL-3.0; active repo. | Standalone unlink/link support for older CoD fastfiles. | No H2/`lazr` support established. Do not use as a Campaign Remastered parser. |
| cascette-rs | [wowemulation-dev/cascette-rs](https://github.com/wowemulation-dev/cascette-rs), community project; Apache-2.0/MIT components; active changelog. | Modern CASC/TACT/TVFS format work, with documented real CDN data validation mainly for WoW products. | Worth code-review as a possible transport reference, but no `lazr` compatibility evidence. It includes network/client features; a future local-only use must explicitly disable those paths. |

“Last update” above is based on repository status/history pages available during this research, not a guarantee of maintenance quality or release stability. Local Jekyll and x64 ZoneTool snapshots have already been source-reviewed in prior phases; their local folders do not establish verified commits.

## 5. Exact-build compatibility evidence

**Status: UNVERIFIED.** No tool documentation, public issue, or source finding located in this pass ties its H2 parser to `1.1.2.1279292` / `CRC_1279292A_retail_signed_ship`.

BlizzTrack’s current indexed record displays version `1.1.3.1279292` and build ID `1279293`, not the supplied `1.1.2.1279292`. Its product config supports the `lazr` product and lists `arSA`, but that current record cannot validate the user-supplied historical build. No archive was opened, and no exact build manifest was downloaded. [BlizzTrack versions](https://blizztrack.com/view/lazr?type=versions)

Exact-build validation requires a reproducible fixture set for the supplied build (obtained only from approved local metadata in a later authorized phase), parser results compared against independent tools/specifications, and hashes/length checks for the metadata files that can be examined without extracting game payloads. A parser that merely accepts the product code or an arbitrary TVFS root is not sufficient.

## 6. Proposed custom toolchain architecture

### A. Read-only CASC inventory

A small command-line library/executable with no UI or online client code. Inputs: an explicit copied or read-only mounted CASC metadata workspace. Outputs: deterministic JSON/CSV listing path, content key, encoding key, logical size, storage spans, locale-like path components, and parse warnings. No data extraction by default. Enforce canonical path containment, strict parser bounds, maximum file sizes, maximum allocations, and explicit unsupported/encrypted statuses. Record hashes of the copied metadata inputs and output report.

### B. H2 container/zone parser

A separate module consumes a single already-decoded logical file supplied by the CASC reader. First establish whether H2 assets are in a directly parseable H2 zone/fastfile or another container, and document all headers, version fields, compression blocks, pointer relocation/streaming, and asset pool structures. Build an inventory mode before asset deserialization. Preserve unknown asset records byte-for-byte and never rebuild a zone while unsupported records would be dropped.

### C. Typed asset readers and translation interchange

Implement independent readers for Localize, StringTable, RawFile, font definitions, and UI/menu resources. Translation interchange must retain original keys, order, hashes, dimensions, source encoding, placeholders, and raw bytes. Use a lossless intermediate representation with escaped text, stable IDs, and provenance. Validate duplicate IDs, missing translations, placeholder signatures, encoding round trips, and diff results. Do not use comma-delimited CSV as the canonical representation.

### D. Arabic layout and font evaluation

Test the existing official Arabic locale first. If custom text is ever needed, separately evaluate glyph coverage, Arabic joining/shaping, bidirectional text algorithm, mixed RTL/LTR runs, numerals and punctuation, font metrics, line breaks, clipping, and UI alignment. A font replacement cannot supply shaping or RTL layout if the game renderer lacks them. Use read-only render experiments only in a later isolated phase after feasibility/provenance gates.

### E. Rebuilder and installer

Only begin after a verified lossless parser, matching writer, and proven load path exist. Rebuild to a disposable external workspace; compare untouched resources byte-for-byte and changed resources structurally; validate sizes, offsets, locale/key identity, and expected hashes. The installer should prefer an official locale selection or documented loose-file/mod package mechanism. If neither exists, stop rather than patching core CASC shards or executable files. Package manifests must bind game ID, exact product/build, resource paths, source hashes, replacement hashes, and rollback data. Installation should be transactional, backed up, idempotent, and reject unknown builds.

## 7. Arabic localization and scope

The product-config mirror lists `arSA` as a supported locale, which makes Arabic the first built-in locale to investigate. It may already provide Arabic interface/subtitle assets in the product’s language installation. That is **LIKELY**, not confirmed for the target’s installed content. The official Call of Duty MW2CR page confirms the PC product but provides no language matrix in the material retrieved. Arabic support must not be inferred from listings for the distinct 2022 Modern Warfare II product.

Expected categories to map after a future asset inventory:

- Localize entries: likely primary short UI strings, labels, objectives, prompts, and notifications; exact category distribution unknown.
- String tables: likely structured tables and some menu/tutorial text; exact mapping unknown.
- Raw files: may contain scripts/config/text, but only resource inspection can establish visible text and whether the game reads overrides.
- Menu/UI and font assets: menus can include localized labels or layout references; H2 support in ZoneTool is not proof of complete round-trip serialization.
- Subtitles/cinematics: may be represented in other asset types or video/subtitle packages. No evidence currently maps all campaign subtitles to the asset classes above.
- Audio: explicitly excluded from the initial project scope.

No claim is made that these resource classes exhaust all player-visible text.

## 8. Launcher and standalone installer feasibility

The product config identifies `lazr`, `MW2CR.exe`, and supported locale `arSA`; it also describes an NGDP update method. This supports using the official Battle.net language/installation flow as the first non-invasive avenue. It does not document an arbitrary external localization-package API. No public evidence found that the official launcher accepts third-party file replacement packages.

A separate installer is feasible only if it can target a documented, stable external override location or a future validated reversible package format. Direct CASC shard mutation is not acceptable as a first implementation. Do not use the user-supplied registry file as an assumed language switch; it remains unexecuted and unverified.

## 9. Feasibility matrix

| Capability | Status | Evidence | Remaining work |
|---|---|---|---|
| Asset discovery | LIKELY | Generic CASC readers support local index/config/TVFS paths; `lazr` product config exists. | Controlled read-only validation on exact-build metadata; confirm H2 file path inventory. |
| Text extraction | UNVERIFIED | Jekyll and ZoneTool expose H2 Localize/StringTable readers, but Jekyll uses process memory and ZoneTool hooks runtime. | Standalone H2 container and asset readers with independent fixtures. |
| StringTable rebuilding | UNVERIFIED | H2 dumper exists in ZoneTool; no offline, exact-build-safe, proven lossless writer established. | Establish binary layout and round-trip test; preserve dimensions, hash fields, encoding. |
| Localize rebuilding | UNVERIFIED | H2 Localize dump support exists in ZoneTool; older MW2 editor support is different game/format. | H2-specific rebuild and retail load-path proof. |
| Arabic font support | LIKELY | `arSA` appears in `lazr` supported locales; H2 tool recognizes a font/TTF type. | Confirm actual resource coverage and renderer behavior; font presence alone is insufficient. |
| Arabic shaping/RTL | UNVERIFIED | No inspected source proves H2 shaping/BiDi support. | Validate official Arabic UI/subtitles or conduct later controlled render tests. |
| Resource reinsertion | BLOCKED | No safe, documented external package mechanism found; CASC store is content addressed. | Find official override support or design/verify a reversible standalone package format. |
| Installer | LIKELY | Build-aware backup/rollback installer is conventional engineering. | Wait for accepted package format, compatibility evidence, path policy, and install route. |
| Launcher integration | BLOCKED | Product metadata shows official locale selection, but no API for arbitrary external packages. | Prefer official `arSA` locale; otherwise keep separate installer and do not alter original launcher. |

## 10. Security and data-integrity risks

- Treat CASC metadata and asset files as untrusted parser input. Bound every count, offset, nested recursion, decompressed size, and allocation.
- Avoid library helpers that open installation metadata with write access even if they claim not to persist changes. Use an immutable source copy or a copy-on-write overlay in any future test.
- Keep outputs external and enforce canonical containment to stop asset-name path traversal. Use create-new/explicit overwrite confirmation and hash every output.
- Preserve unknown asset types and original metadata; partial fastfile rebuilds can silently discard unrelated assets.
- Do not patch the executable, hook the retail process, alter signatures, or modify CASC indexes/shards as an exploratory shortcut.
- Translation QA must protect placeholders, formatting tags, control codes, Unicode normalization, and source encoding. Arabic text must be tested for shaping and BiDi beyond byte-level correctness.
- Obtain legal review for redistribution. Do not bundle original extracted game assets or proprietary fonts/audio in the installer.

## 11. Estimated phases and blockers

1. **Phase 10 — Parser architecture/spec:** Define testable CASC and H2 layers, threat model, copy-on-write strategy, and exact-build fixture requirements. No target files opened.
2. **Phase 11 — CASC compatibility validation:** Use approved metadata copies and independently validated parser implementations; establish path inventory and checksums without exporting game payloads.
3. **Phase 12 — H2 format research/prototype:** Standalone, offline parser over synthetic and lawfully obtained test fixtures; inventory asset types before extracting selected localization data.
4. **Phase 13 — Translation data pipeline:** Lossless source snapshots, translation interchange, automated QA, key/order/placeholder preservation.
5. **Phase 14 — Arabic rendering study:** Compare official arSA behavior and renderer/font support; decide whether any custom shaping/layout engine is technically possible.
6. **Phase 15 — Rebuild/load-path proof:** Only if exact format and officially supportable override/reinsertion method is found; verify byte preservation and isolated load success.
7. **Phase 16 — Installer:** Build detection, package signature/hash verification, backup, transaction, rollback, uninstall, and refusal of unsupported builds.

Primary blockers: exact target build compatibility evidence; independent H2 standalone parser/rebuilder; actual installed Arabic resource inventory; Arabic rendering behavior; and a supported external package/load mechanism.

## 12. Unresolved questions

- Is the supplied build `1.1.2.1279292` a distinct product build from the indexed `1.1.3.1279292` / build ID `1279293`, and what exact build configuration and file hashes correspond to it?
- Does this specific installation already include the official `arSA` locale and all campaign text assets?
- Which TVFS paths hold each H2 zone, localization table, string table, font, menu, and subtitle resource?
- Which H2 fastfile/zone revision and compression/signature variants are used in this product build?
- Can Localize/StringTable/font/menu assets be reconstructed losslessly offline without dropping unknown data?
- Does the retail executable support any official loose-file override or separate package mechanism?
- Does H2 implement Arabic shaping, bidirectional text, mixed Arabic/Latin layout, and proper glyph fallback?
- What legal limits apply to sharing translation-only patches versus original assets and fonts in each distribution region?

## Sources

- [Activision MW2 Campaign Remastered product page](https://www.callofduty.com/mw2campaignremastered)
- [CascLib source repository (MIT)](https://github.com/ladislav-zezula/CascLib)
- [CascView and CASC storage documentation](https://www.zezula.net/en/casc/main.html)
- [BlizzTrack `lazr` product configuration mirror](https://blizztrack.com/config/lazr/pc/ccd6442568d0947402f75ab067051d07)
- [BlizzTrack indexed `lazr` versions](https://blizztrack.com/view/lazr?type=versions)
- [Aurora ZoneTool basics](https://docs.auroramod.dev/zonetool-basics)
- [Aurora H2 map-porting guide](https://docs.auroramod.dev/map-porting-s1)
- [x64 ZoneTool source](https://github.com/Joelrau/x64-zt)
- [Jekyll source (archived)](https://github.com/EthanC/Jekyll)
- [CoD-FF-Tools supported formats](https://github.com/primetime43/CoD-FF-Tools/blob/main/docs/SupportedFormats.md)
- [OpenAssetTools documentation](https://openassettools.dev/)
- [cascette-rs source](https://github.com/wowemulation-dev/cascette-rs)
- [Steam language table for the distinct 2022 Modern Warfare II product](https://store.steampowered.com/app/3595230/Call_of_Duty_Modern_Warfare_II?l=english) — not evidence about MW2 Campaign Remastered.

---

**Final decision: `REQUIRES ADDITIONAL RESEARCH`**

**Exact next implementation phase proposed:** Phase 10 — produce a parser and validation specification for an isolated, metadata-only CASC inventory prototype and standalone H2 zone parser. This proposal does not authorize implementation or access to the target game data.
