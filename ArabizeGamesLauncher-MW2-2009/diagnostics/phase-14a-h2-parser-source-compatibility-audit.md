# Phase 14A — H2 Parser Source Recovery and Static Compatibility Audit

**Scope:** static local-source and diagnostic review only. No resource payload was opened, and no parser was run.

## Executive finding

No suitable H2 `.ff` parser source tree is currently available in the known local download locations for a fresh, line-by-line review. Earlier project diagnostics record prior reviews of Harmony, Jekyll, and x64 ZoneTool, but those snapshots are not present at the previously used paths now. Those reports provide useful leads, not a substitute for the source needed to verify parser behavior and safety.

The target build is `lazr`, `1.1.2.1279292`, `CRC_1279292A_retail_signed_ship`. None of the local evidence ties a standalone `.ff` parser to this exact build. The `.ff` signature/header, compression or encryption variants, malformed-input behavior, and resource-table bounds therefore remain **UNKNOWN**. The 248-byte candidate file sizes do not establish that either file is a valid standalone fastfile or that a parser can read it.

**Final compatibility verdict: UNKNOWN — no parser has sufficient available source evidence.**

**Phase 14B: NOT READY.** Do not proceed to the proposed resource experiment until a legitimate parser source snapshot is available and statically qualifies for standalone file parsing.

## 1. Candidate locations and provenance

Known previously reviewed paths were checked for presence; the Harmony, Jekyll, and x64 ZoneTool source directories are absent:

| Candidate | Previously reviewed local path | Current source availability | Provenance/version evidence retained locally | Assessment for standalone MW2CR `.ff` parsing |
|---|---|---|---|---|
| Harmony | `C:\Users\mouad\Downloads\Compressed\harmony-main` (also `Harmony-master`) | Not found | Phase 4 records upstream project `g-rl/harmony`, package version `0.1.0`, branch `main`; it describes partial MW2CR CASC/audio support. No local commit or source is available now. | **UNKNOWN** for `.ff`: the report describes CASC/TVFS and sound-package observations, not an H2 `.ff` parser. |
| Jekyll | `C:\Users\mouad\Downloads\Compressed\Jekyll-master` | Not found | Phase 9 records `EthanC/Jekyll`, MIT, archived in 2022, and a prior source review of H2 memory exporters. No verified commit/source tree is available now. | **INCOMPATIBLE** with the required standalone offline experiment per the prior review: it reads live process memory and lacked exact-build validation. |
| x64 ZoneTool | `C:\Users\mouad\Downloads\Compressed\x64-zt-main` | Not found | Phase 9 records `Joelrau/x64-zt`, GPL-3.0, with a prior H2 review. The local snapshot was not tied to a verified upstream commit. | **INCOMPATIBLE** with the required standalone offline experiment per the prior review: its H2 workflow relies on game runtime hooks/patches; exact-build support was not established. |
| CoD-FF-Tools / older FastFile tools | No matching local source tree found in the known locations | Not found | Phase 9 describes older MW2 (2009) support, not MW2 Campaign Remastered/H2. | **INCOMPATIBLE** as evidence for this target format. |
| CascLib / CASCExplorer | CascLib source and a prebuilt CASCExplorer bundle were previously found under `C:\Users\mouad\Downloads\Compressed` | CascLib source is available; CASCExplorer source is not | Phase 12A says CascLib is a generic CASC/INSTALL reader without MW2-specific H2 parsing, while CASCExplorer is an unverified binary. | **INCOMPATIBLE** for parsing the internal `.ff` layer. CASC/TVFS transport support does not establish H2 asset parsing. |

The filename search also encountered a generically named `Desktop.zip`; its contents were not inspected, so it cannot be treated as a parser source candidate or as proof that source is absent from every archive. No archive was opened.

## 2. Evidence reviewed

The following existing project diagnostics were read for this audit:

1. `diagnostics/phase-4-casc-compatibility-research.md`
   - Lines 14–16: Harmony 0.1.0 is reported as partial MW2CR support, with CASC/index/TVFS and sound-package observations; tested build is unspecified.
   - Lines 18 and 24–27: general CASC tools are distinguished from exact `lazr`/MW2CR compatibility; no exact-build proof is recorded.
   - Lines 34–44: the reported MW2CR findings concern CASC indices, encoding table, TVFS root, and soundfile PAK/FLAC streams. These findings do not establish `.ff` parsing.
   - Lines 85–89: exact-build compatibility remains unverified; source/runtime side effects are noted for Harmony's CLI.
2. `diagnostics/phase-9-mw2cr-arabic-localization-technical-discovery.md`
   - Lines 12–15: separates CASC transport from the H2 asset/zone layer and says the previously inspected ZoneTool workflow used runtime hooks while Jekyll read process memory.
   - Lines 42–44 and 60–66: records prior tool/source assessments, including Jekyll and ZoneTool limitations and lack of verified commits for local snapshots.
   - Lines 123–129 and 157–163: states text extraction/rebuilding, exact H2 formats, Arabic rendering, and resource reinsertion remained unverified or unresolved.
3. `diagnostics/phase-12a-downloaded-toolchain-inventory.md`
   - Lines 8–12 and 33–39: CASCExplorer was an unverified binary; CascLib was a generic CASC source snapshot; no H2 parser was qualified.

These are **secondary records of earlier source reviews**, not primary source evidence. Because the corresponding candidate trees are absent, exact source file/function/line references for their parser internals cannot be rechecked in Phase 14A. No claim below should be read as a fresh inspection of candidate parser code.

### Search boundaries

Searches were limited to the project diagnostics/source listings and known local download locations, including `C:\Users\mouad\Downloads\Compressed`. The previously cited Harmony, Jekyll, and x64 ZoneTool directories were not present. This is not an exhaustive search of every disk location or the contents of uninspected archives.

## 3. Compatibility and format findings

| Question | Finding | Status |
|---|---|---|
| Is there locally available source for a standalone H2 `.ff` parser? | No candidate source tree was found in the known paths. | **NOT FOUND** |
| Does Harmony parse MW2CR CASC storage? | Earlier report records self-reported MW2CR CASC/TVFS and audio-package observations, but no evidence that it parses the internal `.ff` assets. | **PARTIAL CASC EVIDENCE; `.ff` UNKNOWN** |
| Does Jekyll parse H2 assets? | Earlier source review says it exports H2 assets from process memory; that does not qualify it as a standalone-file parser. | **PARTIALLY RELEVANT; UNSUITABLE FOR THIS TEST** |
| Does x64 ZoneTool understand H2 assets? | Earlier source review describes H2 zone/asset handling, but also runtime hooks/patches and no exact retail-build gate. Standalone read-only parsing is not established. | **PARTIALLY RELEVANT; UNSUITABLE FOR THIS TEST** |
| Is the `.ff` header/signature for this exact build established? | No reviewed primary source is available to establish it. | **UNKNOWN** |
| Are compression/encryption modes and keys understood? | No primary source is available to verify the modes, bounds, or required key behavior for this build. | **UNKNOWN** |
| Are reads bounded and malformed/truncated input handled safely? | Cannot be verified without source. | **UNKNOWN** |
| Can parsing run on a copied standalone file without process memory, injection, hooks, or runtime state? | Not established for any available candidate. Prior Jekyll/ZoneTool assessments conflict with this requirement. | **NOT VERIFIED** |
| Is exact build `1.1.2.1279292` supported? | No candidate has direct source or fixture evidence tied to `CRC_1279292A_retail_signed_ship`. | **NOT VERIFIED** |

### Candidate classifications

- **Harmony — UNKNOWN** for internal `.ff` parsing. Its recorded MW2CR evidence is at the CASC/TVFS and sound-package layer.
- **Jekyll — INCOMPATIBLE** with a standalone offline input-file test, based on the prior review's process-memory design. Exact-build compatibility is also unverified.
- **x64 ZoneTool — INCOMPATIBLE** with the non-invasive standalone test, based on the prior review's runtime hook/patch path. Exact-build compatibility is unverified.
- **Older MW2 (2009) FastFile parsers — INCOMPATIBLE** as evidence for Campaign Remastered/H2.
- **CascLib/CASCExplorer — INCOMPATIBLE** for the internal H2 `.ff` task; these address CASC transport, and CASCExplorer lacks source for this audit.

## 4. Security and parser-safety assessment

No candidate can be approved for reading the two `.ff` candidates. Without primary source, the following cannot be evaluated: integer overflow in size/offset arithmetic, allocation limits, decompression expansion limits, recursion, validation of section/resource tables, malformed input handling, or writes/side effects. The absence of this evidence is a blocker; it is not evidence that a candidate is vulnerable or safe.

The prior Jekyll and ZoneTool reviews specifically report reliance on live-process memory and runtime hooks/patches. Those approaches are outside this phase's requirements and must not be used for the proposed test. Harmony's reported CASC inventory/audio capability does not extend by implication to `.ff` parsing.

## 5. Phase 14B readiness

**Phase 14B cannot safely proceed yet.** No bounded experiment is justified because no candidate has sufficient primary-source evidence for standalone parsing of the target `.ff` format. Do not open the `arara_ui.ff`, `english/eng_ui.ff`, or `arara_common.ff` payloads as part of the next step.

### Minimum evidence required before proposing the experiment

1. Locate or obtain an already-authorized legitimate parser source snapshot; record repository identity, source version, and verifiable commit or explain the provenance gap.
2. Statically review the exact parser entry point and code handling the `.ff` header, target game/build detection, offsets, lengths, compression/encryption, allocations, and truncated/malformed inputs.
3. Confirm from source that the parser accepts a standalone file and does not require a game process, process memory, injection, hooks, network, or writes to the input file.
4. Review output-path handling and ensure inventory/header inspection can be directed to a separate workspace without recursive extraction.
5. Establish synthetic tests for valid and malformed headers, out-of-range tables, oversized declared output, truncated input, and decompression-limit enforcement.

Only after those checks pass should a separate, explicitly authorized controlled test be designed. That future test should operate on verified copies outside the game folder, record original source path, byte size, and SHA-256, impose a documented input cap and decompressed-output cap, inspect only headers/tables first, avoid recursive extraction, preserve originals, and use no process access or runtime hooks. The caps must be set from the statically reviewed format and parser limits; they cannot be safely invented from the current evidence.

## 6. Activity and file accounting

- **Files read:** the three diagnostics named in §2; local path listings and source/diagnostic filename listings under the known project and download locations.
- **Files created:** `diagnostics/phase-14a-h2-parser-source-compatibility-audit.md`.
- **Commands executed:** read-only PowerShell directory/path listings and text searches only. No parser, game, build, or extraction command was run.
- **Game installation accessed:** no. The game directory was not opened or enumerated.
- **Asset payloads accessed:** no `.ff`, `.pak`, or `.bik` payloads; no CASC shards or indices.
- **Network, registry, source edits, and downloads:** none.
