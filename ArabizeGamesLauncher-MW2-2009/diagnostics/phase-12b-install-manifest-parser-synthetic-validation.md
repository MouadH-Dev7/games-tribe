# Phase 12B — Standalone INSTALL Manifest Parser and Synthetic Validation

**Date:** 2026-11-03  
**Scope:** Local source inspection and new synthetic-only Python parser/test files. No game installation path or CASC game data was accessed. No downloaded tool was executed or built. No network or external dependencies were used.

## Executive Summary

A standalone byte-buffer parser and a 21-test synthetic test file were created under `tools\install-manifest-prototype`. The parser requires the caller to explicitly choose `msb` or `lsb` tag bitmap order, preserves entry order and duplicates, reports duplicate paths/tag names, and fails closed on malformed/truncated data. It does not know a game path, resolve CKeys, decompress data, or access CASC.

The local CascLib source verifies INSTALL magic `IN` (numeric constant `0x4E49`) separately from DOWNLOAD magic `DL` (`0x4C44`). Several CascLib comments incorrectly label INSTALL as `DL`, but its constant and comparison use INSTALL `IN`.

The source supports the 10-byte INSTALL v1 header and confirms tag bitmap byte length as `ceil(entry_count/8)`. It skips each INSTALL tag record and the 4-byte per-entry field without interpreting them. Consequently, tag-type semantics, entry-size semantics, UTF-8 path encoding, and INSTALL bitmap bit order are not independently verified locally. Tests were **not executed**: the `python` command is unavailable and the installed `py` launcher reports that no Python runtime is installed. No Python version or test pass count can be reported.

**Overall verdict: `BLOCKED BY FORMAT UNCERTAINTY`.** The parser implementation and test design exist, but actual format semantics and runtime test results remain unverified. This report does not authorize reading the real manifest.

## Local Source Magic Verification

Files inspected:

- `C:\Users\mouad\Downloads\Compressed\CascLib-master\src\CascStructs.h`
- `C:\Users\mouad\Downloads\Compressed\CascLib-master\src\CascRootFile_Install.cpp`

### Confirmed constants and bytes

| Format | Local constant | Little-endian bytes in the `USHORT` header | ASCII |
|---|---:|---|---|
| DOWNLOAD | `FILE_MAGIC_DOWNLOAD = 0x4C44` (`CascStructs.h:164`) | `44 4C` | `DL` |
| INSTALL | `FILE_MAGIC_INSTALL = 0x4E49` (`CascStructs.h:202`) | `49 4E` | `IN` |

`CaptureInstallHeader` compares `pFileHeader->Magic` to `FILE_MAGIC_INSTALL` and requires version 1 (`CascRootFile_Install.cpp:70–90`). Thus implementation uses the INSTALL constant, not the misleading comments. Specifically:

- `CascStructs.h:207` comments `FILE_MAGIC_INSTALL` as `('DL')`, although the constant immediately above is `0x4E49` (`IN`).
- `CascRootFile_Install.cpp:74` says signature `('DL')`.
- `CascRootFile_Install.cpp:99` says it captures a DOWNLOAD header.

These are comment inaccuracies; the actual numeric constant and comparison distinguish INSTALL from DOWNLOAD.

## Exact INSTALL Structure Implemented

The parser file is [install_manifest_parser.py](</C:/Users/mouad/Desktop/Call of Duty - Modern Warfare 2 CR trib/ArabizeGamesLauncher-MW2-2009/tools/install-manifest-prototype/install_manifest_parser.py>).

### Header

The source `FILE_INSTALL_HEADER` fields (`CascStructs.h:205–213`) total 10 bytes:

1. 2-byte magic (`IN` bytes `49 4E` on the target little-endian representation).
2. 1-byte version (source accepts exactly 1).
3. 1-byte key length (source comments expected `0x10`; its parser rejects values above 16 but does not require exactly 16).
4. 2-byte big-endian tag count.
5. 4-byte big-endian entry count.

`CaptureInstallHeader` converts the counts using big-endian helpers (`CascRootFile_Install.cpp:87–89`; helper implementations in `src\common\Common.h:188–219`). The prototype accepts only key size 16 because the local INSTALL entry parser reads a fixed 16-byte CKey, and the supplied expected layout specifies that size. This strict requirement is documented in the code.

### Tag records

`TRootHandler_Install::Load` skips each tag as NUL-terminated name, 2 bytes, and `GetTagBitmapLength(..., EntryCount)` bytes (`CascRootFile_Install.cpp:38–44`). `GetTagBitmapLength` calculates `(entry_count + 7) // 8` (`CascOpenStorage.cpp:547–555`). The source does not interpret the 2-byte INSTALL field; the parser reads it as a big-endian `tag_type` per the expected schema in the phase request. The source’s analogous DOWNLOAD parser calls a comparable field `TagValue`, but that does not establish the INSTALL field’s semantics.

### File entries

`TRootHandler_Install::Load` reads a NUL-terminated path, resolves the next 16 bytes as a CKey, then skips another 4 bytes (`CascRootFile_Install.cpp:46–60`). It does **not** decode or name the final 4-byte value. The prototype reads it as a big-endian unsigned `size` because that is the schema requested for this prototype. **The local INSTALL-specific implementation does not independently confirm that these bytes are file size.**

The prototype decodes tag and path byte strings as strict UTF-8. The local source treats them as `char*` and does not specify encoding, so UTF-8 is another explicit, unverified assumption.

## Bit-Order Evidence and Assumptions

The local INSTALL-specific path computes bitmap length and skips the bytes; it never tests any membership bit. Therefore INSTALL bit order is **NOT VERIFIED**.

`CascOpenStorage.cpp:731–762` parses DOWNLOAD tag bitmaps using MSB-first (`0x80 >> (entry_index % 8)`). This is evidence for DOWNLOAD only. The parser isolates bit interpretation in `_bit_is_set` and requires an explicit `bit_order="msb"` or `bit_order="lsb"` on every parse call. It does not silently pick one for real data.

The synthetic tests build equivalent manifests under both interpretations and verify exactly which synthetic entries map to each tag. They validate implementation behavior under each chosen convention; they do not prove which convention the TACT INSTALL format uses. Real-manifest Arabic classification remains blocked until the INSTALL bit order is independently established.

## Parser Design and Malformed-Input Handling

The parser operates on a caller-supplied `bytes`, `bytearray`, or `memoryview` only. It contains no installation path, file discovery, network, CASC, key resolution, compression, extraction, or output-writing logic.

Safeguards include:

- Exact INSTALL magic and v1 checks.
- Exact 10-byte header validation and 16-byte hash-size requirement.
- Maximum manifest size 64 MiB, 4,096 tags, 1,000,000 entries, and 4,096-byte strings.
- Checked remaining-byte lengths before tag type, bitmap, CKey, and size reads.
- NUL termination and strict UTF-8 validation for names/paths.
- Bitmap tail-bit validation for the selected bit convention.
- Strict rejection of unexpected trailing bytes.
- Descriptive `ManifestParseError` failures rather than returning partial entries.
- Original entry indices and order retained.
- Duplicate file paths preserved and reported; no deduplication.
- Duplicate tag names preserved and reported; filtering on a duplicate Arabic tag fails closed as ambiguous.
- Unknown tag names preserved and never treated as Arabic unless exactly equal to `arSA text` or `arSA speech`.

These are static design properties only. Because the Python runtime was unavailable, runtime behavior and test outcomes remain unverified.

## Synthetic Test Cases and Expected Outputs

The test file is [test_install_manifest_parser.py](</C:/Users/mouad/Desktop/Call of Duty - Modern Warfare 2 CR trib/ArabizeGamesLauncher-MW2-2009/tools/install-manifest-prototype/test_install_manifest_parser.py>). It generates all test bytes in memory using Python’s standard library.

The six-entry sample uses three tags and, for the selected MSB-first convention, these bitmaps:

- `arSA text`: `0xA0` → entries 0 and 2.
- `arSA speech`: `0x60` → entries 1 and 2.
- `zzZZ diagnostic`: `0x08` → entry 4.

Expected locale membership:

| Index | Synthetic path | Expected tags |
|---:|---|---|
| 0 | `synthetic/ui_strings.bin` | `arSA text` |
| 1 | `synthetic/voice_bank.bin` | `arSA speech` |
| 2 | `synthetic/shared_locale.bin` | `arSA text`, `arSA speech` |
| 3 | `synthetic/common.bin` | none |
| 4 | `synthetic/unknown_tag.bin` | `zzZZ diagnostic`; not Arabic |
| 5 | `synthetic/unrelated.bin` | none |

The same logical manifest is generated/tested with LSB-first bitmaps as well. Test methods cover both bit orders; text-only, speech-only, overlap, no locale tag, unknown tag; duplicate paths and duplicate tag names; truncated bitmap/tag type; unterminated tag and path; bad magic; unsupported version; hash lengths 0, 15, 17, and 255; incomplete CKey and size; trailing bytes; six entries with a one-byte bitmap; invalid unused tail bits for both conventions; entry-count cap; and explicit bit-order selection. There are 21 test methods.

## Actual Test Results

- **Python version:** unavailable. `python --version` returned “The term 'python' is not recognized.” The `py` launcher exists at `C:\WINDOWS\py.exe`, but `py -3 --version` returned `No installed Python found!`.
- **Exact test command attempted:** `py -3 test_install_manifest_parser.py` (from `tools\install-manifest-prototype`).
- **Result:** test file did not execute; 0 tests executed, 0 passed, 0 failed. The absence of a Python runtime prevented validation.
- **Malformed inputs:** test cases are present in the test source, but rejection behavior is **NOT VERIFIED by execution**.
- **Both bit-order interpretations:** test cases are present for MSB-first and LSB-first; the test results are **NOT VERIFIED by execution**.
- No runtime was installed and no alternative interpreter or third-party package was used.

## Verdicts

| Area | Verdict | Basis |
|---|---|---|
| Local INSTALL magic identification | `VERIFIED` | Constants and actual comparison: `IN` (`0x4E49`) vs `DL` (`0x4C44`); conflicting comments identified. |
| INSTALL Version 1 structure | `PARTIALLY VERIFIED` | Header, counts, tag skipping, and entry byte spans are visible locally; tag-type meaning, size meaning, and string encoding are not established by INSTALL-specific parsing. |
| Bitmap length handling | `VERIFIED` | Local helper formula is `ceil(entry_count/8)`; prototype implements the same with bounded reads. Runtime tests did not run. |
| Bit-order correctness | `NOT VERIFIED` | INSTALL parser does not interpret bits. Both candidate conventions are covered in test source but not executed. |
| File-to-tag association logic | `PARTIALLY VERIFIED` | Per-tag bitmap length is based on entry count; intended ordinal mapping is implemented, but INSTALL bit semantics/order remain unresolved. |
| Malformed-input handling | `PARTIALLY VERIFIED` | Bounds/fail-closed checks are present in source; no runtime test result is available. |
| Synthetic parser test coverage | `PARTIALLY VERIFIED` | 21 test methods are present, including both orders and required malformed cases; none executed. |
| Suitability for future isolated manifest read | `BLOCKED` | Parser is bytes-only and structurally isolated, but key format assumptions and runtime validation remain unresolved. No real-manifest access is authorized by this phase. |

**Overall verdict: `BLOCKED BY FORMAT UNCERTAINTY`.**

## Remaining Unknowns

- INSTALL bitmap bit ordering (MSB-first or LSB-first).
- Exact semantics of the 2-byte tag field and the trailing 4-byte entry field.
- INSTALL string encoding and any normalization rules.
- Whether the target build’s manifest follows this exact v1 layout.
- Runtime behavior of the new parser/tests; no Python runtime is installed.

## Recommended Next Phase

First confirm the INSTALL-specific tag bit order and the two skipped field semantics from a trusted, already-local format reference or independently identifiable parser source. Separately provide an existing Python runtime through the environment’s normal means in a later authorized phase; do not download/install it under this phase’s restrictions. Then run only the new synthetic test file. Any future real INSTALL manifest read requires a new explicit phase and must remain separate from these synthetic tests.

## Safety Confirmation

1. **Parser and test files created:** yes, only inside `tools\install-manifest-prototype`.
2. **Synthetic tests passed:** no test ran; Python runtime unavailable.
3. **Report path:** `C:\Users\mouad\Desktop\Call of Duty - Modern Warfare 2 CR trib\ArabizeGamesLauncher-MW2-2009\diagnostics\phase-12b-install-manifest-parser-synthetic-validation.md`.
4. **Existing file modified:** no. Only the two new prototype files and this new report were created.
5. **Game data, manifest, or CASC payload accessed:** no.
6. **Downloaded tool executed or built:** no.
7. **Network used:** no.
