# Phase 12D — INSTALL Manifest Extraction (Read-only)

**Date:** 2026-11-03  
**Status:** **BLOCKED before index or data-shard access**. The local Windows Python launcher exists, but no Python runtime is installed (`py -0p`: `No installed Pythons found!`; `py -3 --version`: `No installed Python found!`). The supplied phase context states Python 3.15.2, but that runtime is not available in this environment. The requested index lookup, exact-range BLTE read, manifest parsing, and CSV classification require the new Python scripts and were not attempted without that runtime.

## 1. Verified installation identity

The local metadata was parsed independently. Values below are from the active row in `C:\Games\Call of Duty - Modern Warfare 2 CR\.build.info`:

| Field | Value | Result |
|---|---|---|
| Branch | `eu` | **CONFIRMED** |
| Active | `1` | **CONFIRMED** |
| Build Key | `ebd38497ab7dd9895822a094a31c0457` | **CONFIRMED** |
| CDN Key | `29dcfc9413388701c4144c2e75aee912` | **CONFIRMED** |
| Install Key | `9f86d96c2ae820eb5164d6e12f95ebf6` | **CONFIRMED** against supplied value |
| Version | `1.1.2.1279292` | **CONFIRMED** |
| Tags | Includes conditional `arSA speech` and `arSA text` alternatives, among others | **CONFIRMED** as metadata text only |

SHA-256 of bytes read: `.build.info` = `8E4876B7287CF4ACD37A8E5A9FB0734D3B85E8DFA3B0FA9D09D1FD724C35784E`.

## 2. Active Build Config and install keys

The Build Key resolves to this active text config:

`C:\Games\Call of Duty - Modern Warfare 2 CR\Data\config\eb\d3\ebd38497ab7dd9895822a094a31c0457`

Relevant lines:

```text
install = 3a64e6f6c3d81ac35f9bf6d7c86e6a00 9f86d96c2ae820eb5164d6e12f95ebf6
install-size = 663 670
build-name = CRC_1279292A_retail_signed_ship
build-product = lazr
```

The two values are a CKey followed by an optional EKey. This interpretation is confirmed by the local CascLib source comment in `CascFiles.cpp` (`ParseFile_CdnBuild`, lines 890–893: “Content key [+ encoded key] of the INSTALL file”) and by `LoadCKeyEntry` (lines 403–426), which reads the first hash as CKey and the second as EKey. The second value exactly matches the local `.build.info` `Install Key`. Therefore:

- INSTALL CKey (expected decoded-content MD5): `3a64e6f6c3d81ac35f9bf6d7c86e6a00`
- INSTALL EKey (usable for index lookup): `9f86d96c2ae820eb5164d6e12f95ebf6`

SHA-256 of bytes read: active Build Config = `9BC6952F406C86313D406D174B583E42640822BDFBAC06DD97B7FBCAE48FE9F7`.

No key was guessed. The local `install` line supplies both values.

## 3. Static `.idx` layout review

No game `.idx` file was opened. Before any index access, the local CascLib source was inspected:

- `CascLib-master\src\CascIndexFiles.cpp`: `CaptureIndexHeader_V2`, `LoadIndexFile_V2`, `LoadIndexItems`, and `CopyEKeyEntry`.
- `CascLib-master\src\CascStructs.h`: `FILE_INDEX_HEADER_V2`, `FILE_EKEY_ENTRY`, guarded-block structures.
- `CascLib-master\src\CascCommon.h`: normalized index header and `SegmentBits` definition.
- `CascLib-master\src\CascOpenFile.cpp`: storage-offset split into archive/segment number and offset.

### Source-confirmed version-2 layout

- V2 index revision is `0x07`; `CaptureIndexHeader_V2` validates revision, bucket, flags, and expected field widths (`CascIndexFiles.cpp:350–381`).
- For this implementation, key width is 9 bytes, storage-offset width is 5 bytes, and encoded-size width is 4 bytes; an entry is therefore 18 bytes (`CascIndexFiles.cpp:363–380`).
- The stored EKey is the first 9 bytes of the full encoded key (`CascStructs.h:77–83`). Lookup must compare the EKey prefix and detect multiple matching prefixes rather than silently choosing one.
- The 5-byte storage offset is big-endian (`FILE_EKEY_ENTRY::FileOffsetBE`; `CopyEKeyEntry` uses `ConvertBytesToInteger_5` at `CascIndexFiles.cpp:795`). `SegmentBits` packs the segment/archive number in the high bits and the byte offset in the low bits; `CascOpenFile.cpp:119–137` splits it with a shift and mask.
- **Endianness discrepancy:** `CascCommon.h`’s `FILE_EKEY_ENTRY` comment labels the four-byte encoded size big-endian, but the actual local lookup path at `CascIndexFiles.cpp:796` calls `ConvertBytesToInteger_4_LE`, which reads it little-endian. The implementation is the stronger evidence for how this CascLib snapshot interprets the local entry. This discrepancy must be recorded and checked against the actual index header/layout before using it.
- V2 files contain guarded blocks and may have multiple entry block forms (`CaptureGuardedBlock1/2/3`, `CascIndexFiles.cpp:166–269, 350–453`). A lookup must validate these structures; scanning the file as a flat array is not justified by source.

### Target-layout status

**NOT VERIFIED.** Target `.idx` headers were not read, so it is unknown whether the relevant local index is revision 7, whether its widths match these defaults, which bucket files are relevant, and what `SegmentBits` value the target declares. The static source establishes the candidate format only; it does not establish this exact build’s actual index header values.

## 4. BLTE and manifest processing

**NOT PERFORMED.** No index lookup ran, so no `data.NNN` path, offset, or encoded length was established. No `data.NNN` file was opened. No BLTE header/chunk/mode was read or decoded. No decoded manifest, hash validation, tag summary, bit-order comparison, or locale file CSV was produced.

This is an intentional stop before reading any data-shard region. There is no evidence yet that the selected region is the INSTALL manifest.

## 5. Bit order and locale classification

**NOT INVESTIGATED against real data.** The INSTALL bitmap bit order remains unverified. No bitmap tail bits or extension distribution were examined. No entry was classified as `arSA text`, `arSA speech`, `enUS text`, or `enUS speech`.

No real-manifest result should be inferred from the existence of locale tags in `.build.info` or from Phase 12B synthetic fixtures.

## 6. Game-file access log

All output remains outside the game directory. The file `tools\install-manifest-prototype\out\run.log` records every game file opened in this phase, including offset, byte length, and SHA-256 of the bytes read:

| Game file | Offset | Length read | SHA-256 |
|---|---:|---:|---|
| `C:\Games\Call of Duty - Modern Warfare 2 CR\.build.info` | 0 | 1,572 | `8E4876B7287CF4ACD37A8E5A9FB0734D3B85E8DFA3B0FA9D09D1FD724C35784E` |
| `C:\Games\Call of Duty - Modern Warfare 2 CR\Data\config\eb\d3\ebd38497ab7dd9895822a094a31c0457` | 0 | 738 | `9BC6952F406C86313D406D174B583E42640822BDFBAC06DD97B7FBCAE48FE9F7` |

The `.idx` files and `data.NNN` shards were not opened and therefore do not appear in the log.

## 7. Assumptions and remaining unknowns

- **Confirmed:** `.build.info` Install Key matches the second `install` value; local CascLib source identifies the pair as CKey plus optional EKey.
- **Source-derived, not target-verified:** candidate index V2 entry widths and offset packing.
- **Unresolved discrepancy:** encoded-size byte order comment versus CascLib’s little-endian read code.
- **Not verified:** the actual target `.idx` version, widths, bucket(s), and `SegmentBits`.
- **Not available:** Python 3 runtime. The environment does not match the supplied Phase 12C runtime claim. No third-party code or alternative implementation was used.
- **Not performed:** `find_in_idx.py`, `read_blte.py`, parser invocation on real bytes, `install_manifest.bin`, or `arSA_files.csv`.

## 8. Recommended next step

Make the existing Python 3.15.2 runtime available to the execution environment through its normal managed setup, without downloading/installing anything during this phase. Then resume from the verified metadata and source layout: implement the new bounded index lookup to inspect only relevant `.idx` files and log each read; require the actual header to confirm revision and field widths before interpreting entries; use the located EKey record to derive one exact `data.NNN` range; read and log only that range; validate BLTE modes and decoded CKey; then parse the manifest under both bit orders. If the actual header differs from the statically reviewed layout or the EKey lookup is ambiguous, stop without reading a shard.

## 9. Safety confirmations

- Game directory was not modified.
- No network access, downloads, or package installation occurred.
- No `.idx`, `data.NNN`, or other game asset payload was opened.
- No CascLib/CASCExplorer/downloaded binary was run or built.
- Only `.build.info` and the active Build Config were read from the game directory, and both are logged with byte ranges and hashes.
- No Python script was executed because the Python runtime is unavailable.
