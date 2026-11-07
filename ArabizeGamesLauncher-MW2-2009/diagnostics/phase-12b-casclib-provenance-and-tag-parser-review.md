# Phase 12B — CascLib Provenance and Tag-Aware INSTALL Manifest Static Review

**Review date:** 2026-11-03  
**Scope:** Static review of the local CascLib snapshot and the bundled CascLib DLL’s local metadata. Target-game paths, process, storage indexes, and CASC content were not accessed. No source or downloaded binary was executed or built; no network was used. Only this report was created.

## Executive Summary

The local source identifies itself as CascLib and names the upstream repository in its README, but it has no `.git` metadata. Its public version macro is 3.0; the DLL shipped with CASCExplorer has product-version text ending in a 40-character hex string. The local source/build files do not define that suffix as a Git commit or provide matching source. It is therefore an opaque build/version identifier here, not verified provenance.

The source shows a concrete INSTALL-manifest layout: a version-1 header, followed by `TagCount` tag records, then `EntryCount` filename/CKey records. Each tag record has a NUL-terminated name, a 2-byte field, and a bitmap whose expected length is `ceil(EntryCount/8)`. This strongly indicates that each bitmap addresses INSTALL entry ordinals. However, the INSTALL handler discards tag records and never calculates or stores entry-to-tag membership. The source does not establish INSTALL bitmap bit order; a similar DOWNLOAD parser uses high-bit-first order, but that is not proof of the INSTALL convention.

CascLib’s public APIs expose tag names and per-file tag masks populated through the separate DOWNLOAD manifest path. They do not expose the INSTALL manifest’s tag bitmaps or guarantee that DOWNLOAD tag masks equal INSTALL tag membership. A future tag-aware INSTALL reader therefore requires source changes or, preferably, a separate bounded parser for the INSTALL metadata.

The INSTALL parser has significant malformed-input safety concerns: unbounded `strlen`, unchecked pointer advances, bitmap truncation silently clamped by a helper, no entry/tag count caps, fixed-size key reads without proving enough bytes remain, and success returned after incomplete parsing. It must not be run against real storage or malformed data in this state. A future synthetic-only test can safely target a new standalone bounded parser design, but the exact INSTALL bit convention must first be confirmed from a trusted local specification or independently verified implementation.

**Overall decision: `REQUIRES ADDITIONAL STATIC REVIEW`.** The requested synthetic corpus is designed below, but this is not approval to execute it or CascLib.

## Exact Source Files and Functions Inspected

- `C:\Users\mouad\Downloads\Compressed\CascLib-master\README.md`
- `...\LICENSE`
- `...\debian\changelog`
- `...\CMakeLists.txt`
- `...\CascLib.vcxproj`, `...\CascLib_dll.vcxproj`
- `...\src\CascLib.h`
- `...\src\CascStructs.h`
- `...\src\CascCommon.h`
- `...\src\CascOpenStorage.cpp`: `GetTagBitmapLength`, `CaptureDownloadHeader`, `CaptureDownloadTag`, `LoadDownloadManifest`, `LoadInstallManifest`, `LoadBuildManifest`, `LoadCascStorage`, `CascOpenStorageEx`, `CascOpenStorage`, `CascOpenOnlineStorage`, `GetStorageTags`
- `...\src\CascRootFile_Install.cpp`: `TRootHandler_Install::Load`, `CaptureInstallHeader`, `RootHandler_CreateInstall`
- `...\src\CascFindFile.cpp`: `CopyCKeyEntryToFindData`, root search, `CascFindFirstFile`
- `...\src\CascReadFile.cpp`: `OpenDataStream`, `OpenStaticDataStream`
- `...\src\CascIndexFiles.cpp`
- `...\src\CascFiles.cpp`: `LoadInternalFileToMemory`, `FetchCascFile`, CDN/HTTP download helpers
- `...\src\common\FileStream.cpp`: `BaseFile_Create`, `BaseFile_Open`, stream creation/replacement paths
- `...\src\common\Sockets.cpp`
- `...\src\common\FileTree.cpp`: `CASC_FILE_TREE::InsertByName`
- `...\test\CascTest.cpp`: storage-tag test and generic enumeration use

`src\CascRootFile_Install.h` does not exist. No INSTALL-manifest synthetic fixture/test was found under `test`.

## Workstream A — Source Provenance

### Local source snapshot

- `README.md` describes CascLib as an open-source reader for Blizzard CASC since 2014 and explicitly names `https://github.com/ladislav-zezula/CascLib.git` as the clone URL.
- `LICENSE` identifies the MIT License and copyright © 2014 Ladislav Zezula.
- `src\CascLib.h` declares `CASCLIB_VERSION_STRING "3.0"` and a version-history comment dated 19 Dec 2022.
- `debian\changelog` says package `libcasc (3.2)` with an initial release in 2021. This is Debian packaging metadata, not evidence that this source header is 3.2.
- `CMakeLists.txt` sets shared library version/SOVERSION metadata separately (1.0.0 / 1); the Visual Studio project files name outputs but do not embed an upstream commit in the reviewed configuration.
- No `.git` directory, commit, branch, or tag is present. No generated version header or `git rev-parse`/`git describe` stamping rule was found in the inspected build metadata.
- The bounded local search of the named `Downloads\Compressed` directory and immediate Downloads subdirectories found no independently identifiable second CascLib source checkout matching the DLL suffix.

**Source identity:** CascLib lineage is well supported by the README/license/project files. **Exact snapshot provenance and commit: NOT VERIFIED.**

### Bundled DLL suffix

`CASCExplorer\CascLib.dll` has local PE product version text `1.0.0+79abf98f17ac9bfc5b96922f4ccde9338a45a924`. The suffix is 40 hex characters and resembles a SHA-1/Git object ID, but the local CascLib source and build files neither mention it nor establish its semantics. No matching source checkout was found locally. It is **NOT VERIFIED** as a commit hash or as the revision used to build the DLL. The DLL’s Authenticode status is `NotSigned`; version text is not a signature.

## Workstream B — INSTALL Manifest Format and Tag Association

### Layout accepted by this source snapshot

Evidence is in `src\CascStructs.h` lines 197–213, `src\CascCommon.h` lines 217–226, and `src\CascRootFile_Install.cpp` lines 28–60, 70–90.

1. **Header:** `FILE_INSTALL_HEADER` contains a 2-byte magic (`FILE_MAGIC_INSTALL` is `0x4E49`, comment says `IN`), 1-byte version, 1-byte key-length field, 2-byte big-endian `TagCount`, and 4-byte big-endian `EntryCount`. The C struct totals 10 bytes under the declared field layout. `CaptureInstallHeader` checks buffer length, magic, version exactly 1, and rejects key length greater than 16. It converts the count fields with `ConvertBytesToInteger_2/_4` and sets `HeaderLength = sizeof(FILE_INSTALL_HEADER)`.
2. **Tag records:** `TRootHandler_Install::Load` starts immediately after the header. For each tag it skips: NUL-terminated string + 2 bytes + bitmap length from `GetTagBitmapLength(..., EntryCount)`. The helper computes `ceil(EntryCount/8)` and clamps to the remaining byte count if truncated. The shared DOWNLOAD parser names the 2-byte field `TagValue`; INSTALL code does not read or validate that field.
3. **Entries:** after the tag table, each entry is parsed as a NUL-terminated filename followed by a fixed 16-byte CKey and an additional 4 bytes that the parser skips without interpretation. The header’s key-length byte is not used to vary this fixed entry read. The CKey is resolved through `FindCKeyEntry_CKey`; known entries are inserted into the root file tree under the filename.
4. **Bitmap-to-entry association:** sizing each per-tag bitmap from `EntryCount` strongly indicates one bitmap bit per ordered INSTALL entry. A tag’s bit at entry ordinal `i` would then associate that tag with the `i`th INSTALL entry. Separate tag bitmaps naturally permit one entry to be in multiple tags. **This association is structurally inferred from the format walk; the INSTALL implementation discards the bitmaps and does not itself confirm their semantics.**
5. **Bit order:** the separate DOWNLOAD implementation uses `0x80 >> (i % 8)` for its bitmap’s entry ordering (`CascOpenStorage.cpp` lines 731–762). The INSTALL handler does not apply or document a bit mask. The INSTALL bit order is therefore **NOT VERIFIED** from the local INSTALL code, even though DOWNLOAD provides a plausible convention.
6. **Names/encoding:** strings are read as `char*` NUL-terminated names. No UTF-8 or other encoding validation/conversion appears in the INSTALL parser. The on-disk filename encoding is **NOT VERIFIED** here.
7. **Unknown tags/versions:** INSTALL tag names/values are ignored rather than interpreted. Unsupported INSTALL versions are rejected by `CaptureInstallHeader` (`ERROR_BAD_FORMAT` for anything other than 1). The accepted key-length field is only upper-bounded (`<= 16`), not checked for the expected exact value.

### Pseudocode implied by the format walk

```text
read header: version, tag_count, entry_count
for each tag in tag_count:
    read nul-terminated tag_name
    read 2-byte tag_value
    read ceil(entry_count / 8) bitmap bytes
for entry_index in 0 .. entry_count:
    read nul-terminated filename
    read fixed 16-byte CKey
    read/skip 4-byte field
    for each tag:
        if bitmap[tag][entry_index] is set:
            associate tag_name with this filename/CKey
```

The last association step is **not implemented** by `TRootHandler_Install::Load`. The exact bit order, meaning of the tag-value and trailing entry DWORD, and string encoding remain unresolved locally.

### DOWNLOAD is not INSTALL

`CascOpenStorage.cpp::CaptureDownloadTag` and `LoadDownloadManifest` retain tag names and bitmap-derived masks for DOWNLOAD entries. `CASC_COMMON.h` explicitly calls tag entries “loaded from the DOWNLOAD file.” The DOWNLOAD path iterates its own `EntryCount`, maps bits high-bit-first to central EKey/CKey entries, and sets `TagBitMask`.

This proves CascLib understands a tagged DOWNLOAD table, not that it preserves INSTALL membership. Even if CKeys overlap, the source does not prove that DOWNLOAD bitmasks equal INSTALL entry-tag associations.

## Workstream C — Tag-Aware API Assessment

| Data | Existing API/structure | What the source exposes | INSTALL-specific conclusion |
|---|---|---|---|
| Root file names | `CascFindFirstFile` / `CascFindNextFile`; `CASC_FIND_DATA.szFileName` | Names from the active root handler | INSTALL names can be listed if INSTALL handler is selected and loaded successfully. |
| Root CKey/EKey | `CASC_FIND_DATA.CKey/EKey`; `CASC_FILE_FULL_INFO.CKey/EKey` | Key and file metadata for root/CKey entries | INSTALL parser resolves the entry CKey to central CKey entry, but does not return raw INSTALL row metadata. |
| Tag names | `CascGetStorageInfo(..., CascStorageTags, ...)`; `CASC_STORAGE_TAGS` | Tags in `hs->TagsArray`, created by DOWNLOAD-manifest loading | Not populated by `TRootHandler_Install`. |
| Per-file tag mask | `CASC_FIND_DATA.TagBitMask`; `CASC_FILE_FULL_INFO.TagBitMask` | A mask stored on the central CKey entry | Download-manifest path sets it; INSTALL tag bitmaps are not copied into it. It is not a verified proxy for INSTALL tag membership. |
| Locale flags | `CASC_FIND_DATA.dwLocaleFlags`; `CASC_FILE_FULL_INFO.LocaleFlags`; `CascOpenFile` locale parameter | Locale masks for handlers/storage that provide them | INSTALL handler sets no locale flags. Header constants in `CascLib.h` list older named locale masks and no `arSA` constant. INSTALL `arSA text`/`speech` tags would be string tags, not an existing Arabic locale flag. |
| Product/build metadata | `CascGetStorageInfo(..., CascStorageProduct, ...)` | Product code/build number if recognized by storage parsing | Generic field only; no evidence the target build/version is recognized correctly. |

**Can existing APIs retrieve per-file INSTALL tag membership? NO.** A future implementation must either add INSTALL tag retention and entry-ordinal association to CascLib, or preferably implement a small standalone, bounds-checked INSTALL metadata parser. A separate parser reduces coupling to a broad library and can emit raw rows/tags without opening any game asset entries, but it still needs a verified method to read the INSTALL manifest content from CASC storage.

## Workstream D — Offline and Read-Only Execution Surface

| Path or feature | Classification | Evidence and implications |
|---|---|---|
| `CascOpenStorage` | `LOCAL READ` by default for recognized local build files | `CascOpenStorage` calls `CascOpenStorageEx(..., false, ...)`. Local `.build.info` is a recognized build type. It still loads local indices/encoding/download/root metadata and may read archive bytes for those metadata files. |
| `CascOpenOnlineStorage` / `CascOpenStorageEx(..., bOnlineStorage=true)` | `NETWORK CAPABLE` | Online storage setup and product/region path selection are explicit. Do not use for an offline audit. |
| `CASC_FEATURE_ONLINE`, `CASC_FEATURE_ALLOW_DOWNLOAD`, `CASC_FEATURE_FORCE_DOWNLOAD` | `NETWORK CAPABLE` | Defined in `CascLib.h`; `LoadEncodingManifest`, `LoadDownloadManifest`, and `LoadBuildManifest` may retry local metadata reads online if `ALLOW_DOWNLOAD` is enabled. `BuildFileType == CascVersions` also enables online behavior. |
| CDN/Ribbit/HTTP and sockets | `NETWORK CAPABLE` | `CascFiles.cpp` includes default CDN URL and Ribbit/HTTP helpers; `common\Sockets.cpp` creates/connects sockets and sends/receives. |
| Local `data.NNN` reads | `LOCAL READ` | `CascReadFile.cpp::OpenDataStream` opens existing local data segments with `STREAM_FLAG_READ_ONLY`. The synthetic reader must never call ordinary asset `CascOpenFile`/`CascReadFile` paths except for explicitly selected metadata content. |
| Download/cache files | `WRITES FILES` | `FetchCascFile` and `FileStream_CreateFile` can create/truncate local files for fetched content. `FileStream.cpp::BaseFile_Create` uses `CREATE_ALWAYS` / `O_TRUNC`; download fallback is not read-only. |
| Debug dump helpers | `WRITES FILES` | `CascDumpData.cpp` is gated by `CASCLIB_DEBUG`, but `OpenDumpFile` uses `fopen(..., "wt")` when a path is supplied. Do not enable/use dump helpers for a no-write test. |
| Logs | `UNKNOWN` / no default file logger found in reviewed paths | Progress callbacks report activity; no general default log-file path was identified in reviewed code. This is not an exhaustive review of every application wrapper. |
| Global read-only enforcement | `NOT PRESENT` in reviewed public API | There is no global read-only sandbox switch. Safety depends on a constrained caller, local-only open mode, download flags disabled, no dump/export calls, and output paths outside the game. |

A future caller **could be designed** to open a local `.build.info` storage using `CascOpenStorage` or `CascOpenStorageEx` with online/download flags unset, then read only the well-known INSTALL metadata item and parse only that content. This is **PARTIALLY VERIFIED as a code path, not demonstrated**. Storage open also loads local index, encoding, download, and root metadata; depending on storage state, the INSTALL root handler is only used as fallback when the ROOT load fails. It is not an INSTALL-only API. A separate resolver/parser could minimize reads, but its implementation and exact build compatibility remain to be reviewed. Do not run either path in this phase.

## Workstream E — Parser Safety Review

### INSTALL parser findings

| Source/function | Finding | Status / potential impact |
|---|---|---|
| `CascRootFile_Install.cpp::CaptureInstallHeader` lines 70–90 | Checks that the fixed header exists, magic matches, version is exactly 1, and key length is at most 16. Does not require key length to equal 16 or validate count plausibility against the available manifest length. | **CONFIRMED.** Basic checks exist, but malformed count/length combinations remain accepted into later parsing. |
| `CascOpenStorage.cpp::GetTagBitmapLength` lines 547–555 | Computes bitmap length from 32-bit entry count; pointer addition is used for bounds comparison. On truncation it returns the remaining length rather than an error. It assumes pointers belong to the same valid allocation and does not reject `pbFilePtr > pbFileEnd`. | **CONFIRMED concern.** Can hide truncated bitmap data and permits unsafe/undefined pointer arithmetic for malformed state. |
| `TRootHandler_Install::Load` lines 38–44 | For each tag, reads `strlen(szString)` without a bounded terminator scan or prior end check, then advances by string + 2 bytes + clamped bitmap length. No failure is raised for incomplete tag data. | **CONFIRMED.** Out-of-bounds read on unterminated input; truncated records can desynchronize parsing and appear successful. |
| `TRootHandler_Install::Load` lines 47–60 | Entry filename uses unbounded `strlen`; then `FindCKeyEntry_CKey` reads a fixed 16-byte key without checking that those bytes plus the trailing DWORD remain. Entry count has no explicit upper limit. | **CONFIRMED.** Out-of-bounds read, excessive work/tree growth, or incomplete listing from crafted/truncated metadata. |
| `TRootHandler_Install::Load` lines 47–63 | Loop stops when end is reached but returns `ERROR_SUCCESS` even if `nFileCount` remains nonzero. It ignores the return from `FileTree.InsertByName`. | **CONFIRMED.** Malformed/truncated manifests can yield partial root listings reported as successful. |
| `CASC_FILE_TREE::InsertByName` lines 319–367 | If the filename hash is already present, it “does nothing”; duplicate names or hash collisions are not reported to caller. INSTALL loader ignores the return. | **CONFIRMED.** Duplicate/collision entries can silently disappear from listing. |
| `RootHandler_CreateInstall` lines 93–113 | Assigns the handler to storage after `Load`; propagates the parser return code. | **CONFIRMED.** Caller gets success for the parser’s partial-success cases; error-state cleanup behavior was not fully audited. |
| `CASC_BLOB` / `LoadInternalFileToMemory` path | The complete selected manifest is loaded into memory before parsing. No INSTALL-specific maximum size is enforced in the reviewed parser. | **CONFIRMED concern.** A very large metadata payload can cause memory pressure; exact allocation caps elsewhere were not exhaustively audited. |
| INSTALL names and tag names | Raw `char*` strings; no byte encoding validation, length cap, or normalization. | **CONFIRMED.** Malformed strings or embedded path syntax need safe treatment by any new parser; encoding/duplicate semantics remain unknown. |
| Tag reference handling | Existing INSTALL code does not validate tag-value uniqueness/references or bitmap tail bits; it ignores these fields. | **CONFIRMED.** Unknown tags are silently skipped; invalid references cannot be surfaced. |

### Related DOWNLOAD parser observations

`CaptureDownloadTag` does check for a NUL before `pbFileEnd` and for a minimum tag-value field, but bitmap length is still clamped. `LoadDownloadManifest` allocates based on `TagCount`; its tag loop advances by `TagLength` even after capture failure, and the entry loop can break on a bad entry while the function retains `ERROR_SUCCESS`. These behaviors are not INSTALL parsing, but show that the generic tag path is not a safe substitute or a validated oracle for malformed INSTALL data.

**Parser safety verdict:** unsafe for adversarial/truncated input as written. No fuzzing or execution was attempted. These findings block any test that invokes the existing parser on crafted malformed bytes; they do not block designing a separate bounded synthetic parser test.

## Workstream F — Exact MW2CR Compatibility Boundary

Only prior Phase 10–12A reports and local CascLib source were used; the game directory and its binary metadata were not accessed.

| Question | Status | Evidence / boundary |
|---|---|---|
| Generic INSTALL manifest support | **PARTIALLY VERIFIED** | Source has a version-1 INSTALL root handler; accepts `IN` magic, 16-byte maximum key length, entry/tag counts. |
| Exact INSTALL manifest version in MW2CR build `1.1.2.1279292` | **NOT VERIFIED** | Target install key exists in prior local metadata report, but manifest bytes/version were not read. |
| Compatibility with target index layout | **NOT VERIFIED** | CascLib source contains generic index readers including revisions 5 and 7; target index bytes were not examined and no exact `lazr` branch is present. |
| Compatibility with target encoding/index metadata | **NOT VERIFIED** | Source has generic ENCODING parser; no target metadata payload or exact build parser evidence. |
| Product `lazr` recognition | **NOT VERIFIED in CascLib source** | No `lazr`/MW2 string was found in CascLib `src`. CASCExplorer config has a `lazr` label, but that is separate and not proof this source handles the build. Generic product-code arguments exist. |
| Correct `arSA text`/`arSA speech` association | **NOT VERIFIED** | Local build metadata has these tags, per prior reports; this source’s INSTALL handler discards INSTALL tags. DOWNLOAD tags are a separate source and not proved equivalent. |
| List manifest metadata without ordinary asset payloads | **PARTIALLY VERIFIED as a possible design** | Public root enumeration exists and INSTALL is a well-known item. A constrained caller could target metadata, but normal storage open parses other metadata and compatibility is untested. |

The prior Phase 11 metadata references active Install Key `9f86d96c2ae820eb5164d6e12f95ebf6`, but this review did not access that file, any index, or any payload. Generic CASC/INSTALL support does not establish compatibility with this exact build.

## Workstream G — Synthetic Test Design Only

**Status:** design only; no bytes were created and no test was run. Use a new bounded parser implementation, not CascLib’s current `TRootHandler_Install::Load`, until its safety issues are resolved. All fixtures must be generated in an isolated test workspace and contain synthetic keys/names only.

### Candidate fixture layout

Based on the source’s layout walk, use a 10-byte version-1 header with magic `IN`, key length 16, `TagCount = 3`, and `EntryCount = 6`. Add three tag records, each `NUL-terminated name + 2-byte value + one bitmap byte`, followed by six records of `NUL-terminated filename + 16 synthetic CKey bytes + 4 synthetic bytes`.

For a candidate MSB-first bitmap convention (borrowed from the DOWNLOAD parser and **NOT VERIFIED for INSTALL**):

- `arSA text`: bitmap `0xA0` → entry ordinals 0 and 2.
- `arSA speech`: bitmap `0x60` → entry ordinals 1 and 2.
- `zzZZ diagnostic`: bitmap `0x08` → entry ordinal 4.

Entries:

| Ordinal | Synthetic name | Expected parsed membership |
|---:|---|---|
| 0 | `synthetic/ui_strings.bin` | `arSA text` only |
| 1 | `synthetic/voice_bank.bin` | `arSA speech` only |
| 2 | `synthetic/shared_locale.bin` | both `arSA text` and `arSA speech` |
| 3 | `synthetic/common.bin` | no locale tag |
| 4 | `synthetic/unknown_tag.bin` | preserve unknown `zzZZ diagnostic`; do not classify as Arabic |
| 5 | `synthetic/duplicate_name.bin` | no tag; separate duplicate-name test below |

The fixture must declare its bit-order assumption; it is a parser-consistency test until INSTALL bit order is independently verified.

### Required negative/edge fixtures and expected behavior

| Case | Mutation | Expected result from a hardened parser |
|---|---|---|
| Valid tags | As above | Return all six rows with exact memberships. |
| Text-only | Entry 0 set only in text bitmap | Text membership only. |
| Speech-only | Entry 1 set only in speech bitmap | Speech membership only. |
| Both tags | Entry 2 set in both bitmaps | Both memberships; do not collapse to one. |
| No locale tag | Entry 3 unset in all tag bitmaps | Explicit empty membership. |
| Unknown tag | Unknown tag name has entry 4 bit set | Preserve raw tag and membership; classify as unknown, not Arabic. |
| Duplicate name | Add a second entry with same filename and different synthetic CKey | Return explicit duplicate diagnostic or preserve both rows; never silently drop/overwrite. |
| Truncated bitmap | Remove required bitmap byte while keeping counts | Reject the manifest with a bounds error; never clamp and report success. |
| Invalid bitmap reference | For `EntryCount=6`, set a tail bit for ordinal 6 or 7 (e.g. low-order bit in the only byte under MSB-first convention) | Reject or report out-of-range set bits; do not associate with an entry. Note: INSTALL layout has no separate per-entry tag-ID reference, so bitmap tail bits are the meaningful invalid-reference case. |
| Unsupported version | Change header version from 1 to 2 | Reject explicitly as unsupported. |
| Unterminated tag/file name | Remove NUL terminator before end of buffer | Reject without reading past buffer. |
| Truncated CKey/entry trailer | End after partial CKey or before 4-byte trailer | Reject; never return partial success. |
| Duplicate tag name/value | Repeat a tag record name or value | Report ambiguity; do not merge silently unless independently specified by format. |

Before these expectations can be called format-correct rather than internally consistent, verify the INSTALL bitmap bit order, tag-value semantics, duplicate policy, and entry trailer meaning from a trusted local specification or an independently identifiable implementation. Do not run the current CascLib parser on these fixtures.

## Workstream H — Evidence-Based Verdicts

| Area | Verdict | Rationale |
|---|---|---|
| Local CascLib source provenance | `PARTIALLY VERIFIED` | Upstream identity/README/license are present; no commit/tag or exact match to DLL suffix. |
| INSTALL manifest parsing correctness | `UNSAFE` | Version-1 handler exists but bounds/truncation/partial-success concerns are confirmed; it discards tag data. |
| Per-file locale-tag recovery | `REQUIRES SOURCE CHANGES` | Raw layout strongly suggests tag bitmaps cover ordered entry ordinals, but INSTALL handler/API does not preserve membership; bit order remains unverified. |
| Exact MW2CR build compatibility | `NOT VERIFIED` | No exact product/build support in source, and target manifest/index/encoding content was not inspected. |
| Offline/read-only suitability | `PARTIALLY VERIFIED` | A local offline caller is possible with online/download flags disabled, but the library has network and write-capable paths and no global read-only guard. |
| Parser safety | `UNSAFE` | Confirmed unbounded string/bounds and partial-success issues in INSTALL path. |
| Future synthetic-only test design | `PARTIALLY VERIFIED` | A safe, synthetic-only design is specified for a new bounded parser; exact INSTALL bit semantics remain unverified, and current parser must not be the test target. |

**Overall decision: `REQUIRES ADDITIONAL STATIC REVIEW`.**

## Remaining Unknowns

- Whether the DLL’s `79abf98…` suffix is actually a Git commit ID and which source it identifies.
- Whether this source snapshot matches any known CASCLib upstream revision.
- Authoritative INSTALL bitmap bit order and tag-value semantics.
- Exact meaning of the INSTALL entry’s trailing four bytes and filename encoding.
- Whether the target build’s INSTALL manifest is version 1 and uses this layout.
- Whether target index/encoding files match this implementation’s supported revisions.
- Whether an exact target build can be opened fully offline by this library without fallback behavior that reads additional content.

## Recommended Next Phase

Continue static review only until the DLL’s exact source revision can be established or the bundled binary is rejected as unverifiable. In parallel, obtain an authoritative local INSTALL-manifest specification or independently identifiable format implementation and confirm tag bitmap ordering and entry semantics. Then design a standalone bounded parser with explicit byte-span checks, count/size caps, exact NUL termination, duplicate detection, unknown-tag preservation, and fail-closed errors. The synthetic fixture can be implemented and executed only in a separately authorized phase, using generated bytes and no game files. Exact MW2CR compatibility must remain a separate gate; do not point the existing CascLib parser or CASCExplorer binary at the game.

## Safety Confirmation

- Report saved at: `C:\Users\mouad\Desktop\Call of Duty - Modern Warfare 2 CR trib\ArabizeGamesLauncher-MW2-2009\diagnostics\phase-12b-casclib-provenance-and-tag-parser-review.md`
- No other file was modified during this phase.
- No tool was executed or built; no test was run; no network was used.
- No game directory, game process, game `.idx`/`.index`, INSTALL manifest, encoding table, archive shard, or CASC payload was accessed.
