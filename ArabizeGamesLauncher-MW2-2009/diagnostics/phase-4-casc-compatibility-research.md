# Phase 4 — CASC compatibility research for MW2 Campaign Remastered

**Research date:** 2026-11-03  
**Target:** Call of Duty: Modern Warfare 2 Campaign Remastered PC  
**Game ID:** `mw2-campaign-remastered-pc`  
**Product:** `lazr`  
**Installed build:** `1.1.2.1279292` / `CRC_1279292A_retail_signed_ship`  
**Research-only boundary:** no tool downloaded, installed, cloned, compiled, or executed; no original archive opened; no game, registry, settings, or launcher changes made.

## Executive finding

**POTENTIAL CANDIDATE - REQUIRES CONTROLLED TEST**

The strongest lead is **Harmony 0.1.0**, a small open-source Call of Duty asset explorer. Its upstream README lists MW2 Campaign Remastered (`h2`) as **partial** support. Its design notes document real MW2CR observations: local CASC index version 7, 9-byte encoded keys in the index entries, a 16-byte alignment rule, an encoding-table parsing correction, a TVFS root, and discovery/extraction of 546 `soundfile*.pak` files containing 141,329 FLAC streams. This is direct game-specific evidence that at least one MW2CR storage was parsed by Harmony.

However, Harmony does not state that its test installation was build `1.1.2.1279292` or name the tested build ID. Nor has Harmony been independently run against the user's exact installation. The report supports a controlled test of Harmony's listing-only path, not exact-build compatibility certification or localization claims.

Generic tools are less promising as first choices. CascLib 3.0 has a TVFS root handler and standard local-storage/read APIs, but its source does not identify `lazr` or this MW2CR build. CascLib also documents an encoding-table size restriction. CASCExplorer and CASC Modern use CascLib without MW2CR-specific evidence; CascView makes a broad “all Blizzard games” claim, but that is not evidence for this Activision title/build.

## Candidate tools and versions

| Candidate | Version reviewed | MW2CR / `lazr` evidence | Read-only considerations | Confidence |
|---|---|---|---|---|
| **Harmony** — [repo](https://github.com/g-rl/harmony), [README](https://github.com/g-rl/harmony/blob/main/README.md), [design notes](https://github.com/g-rl/harmony/blob/main/DESIGN.md), [Cargo manifest](https://raw.githubusercontent.com/g-rl/harmony/main/Cargo.toml) | Cargo package `0.1.0`, Rust edition 2024; branch `main` reviewed. GitHub repo page showed no published Releases. | **Strongest direct evidence:** MW2CR is listed `partial`; design notes give observed local index/encoding/TVFS details and counts from a real MW2CR install. Tested build is not identified, so the exact target build is **NOT VERIFIED**. | CLI `--casc <game folder> [extension]` is documented as a headless inventory. Upstream `main.rs` shows it opens storage, reports product/build/file counts and extensions, and optionally reads one matching file into memory; this branch does not invoke the export queue. However process startup loads settings and boots logging/crash reporting before dispatch. A first run may write app data. The UI, scans, and exports definitely write caches/output. A strictly isolated data directory and source review of the called storage reader are needed before a controlled run. | **High** for the upstream project's stated support and format observations; **medium** for practical compatibility with the untested exact build; **low** for asserting no side effects without reviewing/isolating its full runtime. |
| **CascLib** — [repo](https://github.com/ladislav-zezula/CascLib), [README](https://github.com/ladislav-zezula/CascLib/blob/master/README.md), [release list](https://github.com/ladislav-zezula/CascLib/releases), [versioned header](https://github.com/ladislav-zezula/CascLib/blob/master/src/CascLib.h) | Upstream release/header **3.0**; current source branch `master` reviewed. No local binary version is relevant; no binary was installed. | **No exact support claim found** for Call of Duty, `lazr`, or build `1.1.2.1279292`. It handles TVFS roots in general, which is necessary evidence but not sufficient. | Header exposes local open, enumerate, and read operations separately from online/download flags. A caller can request local storage with online/download disabled and direct any extracted output elsewhere. CascLib's own console/test code has explicit extraction/output paths, so the front end's behavior still matters. | **High** on source/API behavior; **low** on exact-build compatibility. |
| **CascView** — [official CASC tools page](https://www.zezula.net/en/casc/main.html) | Current release/version could not be established from the browsed page. No binary was installed. | Official page describes browsing/extracting and broadly says it supports Blizzard games. No MW2CR, `lazr`, or exact-build test evidence found. | Designed to browse and extract. Extraction writes output; the target must be a separate workspace. Whether this build's open path is offline/local-only would need to be checked in its selected version. | **Medium** that it is a CASC browser; **low** for exact game/build support. |
| **CASCExplorer / CASCConsole** — [repo](https://github.com/WoW-Tools/CASCExplorer), [releases](https://github.com/WoW-Tools/CASCExplorer/releases), [console source](https://github.com/WoW-Tools/CASCExplorer/blob/master/CASCConsole/Program.cs) | Latest visible release **CASCExplorer v1.0.240**, marked pre-release. | CLI source requires product and locale arguments and uses CascLib. No `lazr`/MW2CR-specific support statement or successful build evidence found. | Console has an explicit destination-folder parameter and an online-mode switch, so extraction can be directed outside the installation and offline behavior selected. It is still an extraction-oriented console, not a proven safe MW2CR inventory workflow. | **High** that the CLI supports explicit product/locale/destination options; **low** on MW2CR exact support. |
| **CASC Modern / casc-modern** — [repo README](https://github.com/TrayHard/casc-modern/blob/main/README.md), [releases](https://github.com/TrayHard/casc-modern/releases) | Latest release visible: **0.3.0**. | README says it reads local CASC storages CascLib can open, but its stated workflows and decoders focus on Diablo II: Resurrected. No MW2CR, `lazr`, or target-build evidence found. | Supports browse/list and external export; GUI writes settings and caches. It should not be treated as read-only without isolating its configuration and export paths. | **High** on stated scope; **low** on exact MW2CR support. |

## Source and format findings

### Harmony: direct but build-unspecified MW2CR evidence

The upstream [README support table](https://github.com/g-rl/harmony/blob/main/README.md) identifies `mw2cr` / `h2` and marks it `partial`, with audio stored in `Data\\data\\` under Battle.net CASC. The README explains that “partial” means audio streams are found/extracted but names or categories are incomplete. It reports 546 sound packages and 141,329 sounds, while encrypted blocks stay encrypted because the tool has no keys.

The more detailed [design notes](https://github.com/g-rl/harmony/blob/main/DESIGN.md) record these MW2CR-specific observations from a real install:

- Local `.idx` indices use version 7.
- The index entry table begins at the next 16-byte boundary after the header; the notes give `EncodedSizeLength=4`, `StorageOffsetLength=5`, `EncodedKeyLength=9`, and `FileOffsetBits=30`.
- The encoding table's `espec` block length was observed at byte 18; reading it from byte 17 produced an unresolved TVFS root in their prototype.
- The root manifest is TVFS; the notes identify its path/VFS/CFT offsets and report 546 `soundfile*.pak` entries.
- The sound paks yielded 141,329 FLAC streams; the project's MW2CR support remains `partial`, with path/name resolution incomplete.

These are strong format-family and implementation observations, but the design page's specific path is a generic `D:\MW2 Campaign Remastered`, not the user's path, and the tested build number is not given. It is self-published project evidence, not independent reproduction.

The upstream [Harmony CLI source](https://raw.githubusercontent.com/g-rl/harmony/main/src/main.rs) shows `--casc` calling the storage open routine, printing product/build/file counts and extension counts, and optionally reading one matching resource into memory to report its size and leading bytes. That branch does not call the export queue. The same `main()` loads settings and boots logging/crash handling before argument dispatch, so the whole process cannot be called side-effect-free: isolate its app-data/config locations. `--probe`, normal scans, and extraction are explicitly cache/export writing paths and are not the inventory-only route.

### CascLib: relevant general support and caveats

The [CascLib 3.0 header](https://github.com/ladislav-zezula/CascLib/blob/master/src/CascLib.h) documents local storage open, file enumeration, file reads, and `CASC_FEATURE_ONLINE` / `CASC_FEATURE_ALLOW_DOWNLOAD` separately. In principle, a caller can configure local-only reading and write any extracted output to another directory. The source is not a blanket read-only guarantee for every GUI or wrapper.

The [storage source](https://github.com/ladislav-zezula/CascLib/blob/master/src/CascOpenStorage.cpp) dispatches recognized root signatures including TVFS, and notes that TVFS roots commonly reference 9-byte EKeys. That aligns in part with Harmony's observed MW2CR TVFS root. However, `CaptureEncodingHeader` explicitly rejects ENCODING files whose CKey/EKey widths differ from 16 bytes. The retrieved code does not establish whether all MW2CR's index-v7 handling and byte-offset quirks are supported. No explicit `lazr` branch or fixture for the target build was located.

An example of format drift is [CascLib issue #285](https://github.com/ladislav-zezula/CascLib/issues/285), which reports local D2R extraction issues involving metadata blobs, archive-style index handling, and TVFS-prefixed paths. That is a different game and does not prove a MW2CR failure; it does show why “CASC” alone is not a compatibility guarantee.

### Other named tools

- **CascView:** its official page describes a CASC browser/extractor for Blizzard games. Its broad wording does not identify MW2CR's `lazr` product or this build.
- **CASCExplorer:** its CLI source exposes product, locale, online mode, and destination path, and depends on `CASCLib`. Its product-aware options do not show that `lazr` is accepted or that the target's index-7 layout works.
- **CASC Modern:** its README says it wraps CascLib and aims at D2R browsing/extraction. This is another CascLib-based frontend, not independent game-specific support evidence. Its visible latest release is 0.3.0.
- No independently documented exact-build experience was found for the literal `CRC_1279292A_retail_signed_ship` string. Harmony documents MW2CR-specific experience, but omits its build ID.

## CASC, index, encoding, keys, and data availability

| Layer | Evidence | Assessment |
|---|---|---|
| Storage product | Local Phase 1/2 diagnostics identify `.build.info` product `lazr` and build `1.1.2.1279292`. Harmony recognizes MW2CR by its own `h2` title identifier. | **Not equivalent identifiers.** Harmony's game key `h2` is not proof that its tested storage product/build equals this exact local product/build. |
| Local index format | Harmony reports MW2CR `.idx` version 7 and a custom 16-byte entry-table alignment, with 4/5/9-byte fields. | **Direct MW2CR evidence**, but the tested build is unspecified. No proof found that CascLib v3.0 accepts these precise indexes. |
| Encoding | Harmony reports an `espec` offset quirk (byte 18, not 17) needed to resolve the root. CascLib parses the declared `ESpecBlockSize` and requires 16-byte CKey/EKey lengths in the ENCODING header. | Plausibly interoperable if this build follows the expected standard ENCODING header; exact storage not verified. Harmony’s code/design is the only direct success evidence found. |
| Root manifest | Harmony reports a TVFS root. CascLib source has a TVFS root handler and supports 9-byte TVFS EKeys in its storage maps. | **Good format-level overlap**, not exact build certification. |
| Archive data/shards | Previous local inventory found 65 `data.NNN` shards and 32 `.idx` files under `Data\\data`, plus the config/index files. Harmony says its tested MW2CR copy kept content in CASC and could locate sound paks there. | **Plausible compatibility** for local Battle.net layout. It has not been tested against this installation. |
| Keys/encryption | Harmony says encrypted blocks remain unreadable and it ships no keys. | No keys should be guessed, fetched, or bypassed. A localized resource in an encrypted entry may not be readable by this candidate. |
| Read vs download | CascLib API distinguishes local opening from online/download feature flags. CASCExplorer has an online mode switch. | A future test must explicitly disable online/download behavior and fail if local resources are missing; do not rely on defaults. |

## Locale-specific groups and downloadable localization content

**VERIFIED from prior local metadata:** the installed `.build.info` Tags includes `arSA text?` and `arSA speech?`. This proves the locale labels appear in build metadata; it does not prove the corresponding files are installed or complete.

**VERIFIED as a secondary product-config listing:** the public [BlizzTrack `lazr` PC config mirror](https://blizztrack.com/config/lazr/pc/ccd6442568d0947402f75ab067051d07) lists `arSA` among `supported_locales`, and identifies the product as `lazr` with NGDP update method. This suggests that Arabic is a product-level supported locale and that locale content may be distributed through Battle.net's content system. It is not the publisher's official documentation, does not identify which locale assets are currently downloadable, and does not establish that the user's installed archives contain those assets.

Harmony's MW2CR findings locate `soundfile*.pak` resources inside CASC but do not report `arSA` directory groups or attribute specific paks to Arabic. Its README says the streams are found, but names are incomplete and encrypted blocks remain unreadable. No primary evidence found in this research establishes locale-specific CASC group names for this build or identifies a downloadable Arabic text/font package. Whether the user's 65 data shards include Arabic text/audio remains **NOT VERIFIED**.

## Limitations and confidence

1. **Harmony can parse at least one MW2CR CASC installation — HIGH confidence** for what the upstream project claims and documents; **MEDIUM confidence** as independent technical proof, because the evidence is self-reported and not reproduced here.
2. **Harmony supports this exact `1.1.2.1279292` build — NOT VERIFIED, medium confidence.** The project does not state its test build ID. Build similarity may be likely for an older, largely static release, but that is not a substitute for evidence.
3. **CascLib 3.0 can open this exact storage — NOT VERIFIED, high confidence in the evidence gap.** Its code supports several relevant general structures but has no discovered `lazr` fixture/claim; MW2CR index-v7 behavior is unresolved.
4. **Arabic resources are installed — NOT VERIFIED, high confidence.** Locale metadata exists; no archive content was inspected in Phase 4.
5. **Inventory-only command avoids game writes — MEDIUM confidence from Harmony's CLI function body.** `--casc` has no extraction/output-file call in the displayed body, but runtime startup writes application state/logs, and the storage-open implementation was not fully reviewed or run. Isolate app data and verify process/file writes before any approved test.
6. **No implementation impact — VERIFIED.** This phase changed no launcher code and did not change Install or Restore controls.

## Recommended next step

Use **Harmony 0.1.0 as the first controlled-test candidate**, subject to authorization before any acquisition or execution. Before running it, pin the exact upstream commit/source and verify any release artifact's provenance/hash. Inspect the implementation behind `Storage::open` and its `--casc` path for writes and online fallbacks. Run only a listing-only inventory in an isolated environment with Harmony's config/cache redirected away from the game, networking disabled, and output outside the game. Prefer a verified copy of the required metadata and data shards; if the original must be read, enforce read-only access externally without changing the installation and capture relevant before/after hashes. Do not run `--probe`, `--grab`, `--pull`, or GUI export for this initial compatibility check.

The test must first report the detected product and build. Stop if they do not match `lazr` and `1.1.2.1279292`. If matched, compare any reported index/encoding/TVFS facts with the project's observations, then inventory only paths/metadata for locale candidates. Extract at most a small, explicitly selected resource to an external workspace after confirming safe writes; hash it and classify it by contents rather than filename. Do not make localization or Arabic-rendering claims from audio-only results. Install/Restore remain disabled.

## Decision

**POTENTIAL CANDIDATE - REQUIRES CONTROLLED TEST**
