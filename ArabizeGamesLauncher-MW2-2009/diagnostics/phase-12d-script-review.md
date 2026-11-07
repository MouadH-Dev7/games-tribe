# Phase 12D (rev 6) — Script and Safety Review

**Scope:** write/edit and static review only. The game directory was not accessed. No Python, parser, or CASC tool was run.

## Local-header source review and correction

The local CascLib source was re-reviewed at `CascStructs.h` (`BLTE_ENCODED_HEADER`) and `CascReadFile.cpp` (`ParseBlteHeader`). The structure declares these fields:

```cpp
ENCODED_KEY EKey;       // Encoded key of the data beginning with "BLTE" (byte-reversed)
DWORD EncodedSize;      // Encoded size of the data data beginning with "BLTE" (little endian)
BYTE field_14;
BYTE field_15;
BYTE JenkinsHash[4];
BYTE Checksum[4];
// BLTE header. Always present.
```

Those field widths place EKey at bytes 0–15, little-endian `EncodedSize` at 16–19, bytes 20–21, Jenkins bytes 22–25, checksum bytes 26–29, and the BLTE signature at offset 30.

`CascReadFile.cpp`, `ParseBlteHeader`, documents the storage/local distinction:

```cpp
// On files within storage segments ("data.###"), there is BLTE_ENCODED_HEADER
// On local files, there is just BLTE_HEADER
```

When using the storage header, the code locates the BLTE signature in `BLTE_ENCODED_HEADER::Signature` and sets the extra-header length with `FIELD_OFFSET(BLTE_ENCODED_HEADER, Signature)`.

**Correction to rev 5:** the structure comment suggests `EncodedSize` counts bytes beginning at BLTE. However, CascReadFile's header parser does not validate or otherwise consume this local header `EncodedSize` field. In the user's target-build read, bytes 16–19 decode little-endian to 700 (`0x2bc`), matching the full indexed region. The earlier claim that this field must be 670 was incorrect. The script now follows the observed target and requires this field to equal the full 700-byte index region and `config_encoded_size + 30`.

`CascOpenFile.cpp` has a distinct normalization for the index entry's encoded size when a standalone local stream lacks the storage prefix:

```cpp
DWORD StreamHeaderSize = FIELD_OFFSET(BLTE_ENCODED_HEADER, Signature);
...
if(pCKeyEntry->EncodedSize == StreamEncodedSize + StreamHeaderSize)
    pCKeyEntry->EncodedSize = StreamEncodedSize;
```

This code concerns the index entry/local-stream size adjustment; it does not override the observed value of the target's on-disk local header field.

## Updated read and validation path

- The `.idx` entry must have size `config encoded size + 30` (700 bytes). The validated V2 header must state `SegmentBits = 30`; the archive number is the high portion of the packed five-byte offset and the byte offset is its low 30 bits.
- The script prints archive number, byte offset, and size, then reads exactly one 700-byte range from `Data\data\data.NNN`.
- It validates the local EKey against both forward and reversed byte order and reports which matches. Neither match is fatal and prints the first 64 region bytes.
- The local header size at bytes 16–19 is decoded little-endian and must equal both 700 and `config encoded size + 30`. Bytes 20–21, 22–25, and 26–29 are printed but are not validation gates. `BLTE` must appear exactly at offset 30; otherwise the first 64 bytes are printed and processing stops.
- Before decoding, the BLTE stream must be 670 bytes, have header size 60, chunk-table marker `0x0f`, and exactly two chunks. The script requires `header_size == 12 + 24 * chunk_count`, bounds each chunk size, prints each chunk's encoded size, decoded size, and mode byte, and requires `header_size + sum(encoded chunk sizes) == 670`.
- The existing decoder then supports only `N` and `Z`, verifies per-chunk MD5 values, and enforces output limits. It requires decoded MD5 `3a64e6f6c3d81ac35f9bf6d7c86e6a00` and length 663. `install_manifest.bin` is written only after all these checks pass.

## Index scan and hash behavior

- It uses the newest available `.idx` version for each bucket 00–0f in `Data\data`, with no EKey-derived bucket guess. It requires exactly one 9-byte EKey-prefix match overall.
- Guarded entry-block hash mismatch is a warning only after validating 9/5/4 widths, positive block size divisible by 18, in-file bounds, and non-decreasing keys. It prints full details once and a one-line filename/mismatch summary for subsequent index files. Structural failures stop and print the first 128 file bytes and 64 bytes at the entry-block start.
- Index-header guarded hash mismatch remains fatal. Every opened index is logged with path, offset, length, and SHA-256.

## Safety and limitations

- All game-file reads remain bounded and use `rb`. The script opens only the newest selected `.idx` files and exactly one data shard range; it never scans or hashes a whole `data.NNN` file.
- All generated outputs remain under `tools\install-manifest-prototype\out\`.
- This revision was not executed or runtime-tested. The stated 700-byte local-header field and the chunk dimensions are from the user's run output, not a new local game read.

## Run command

From the project root:

```powershell
py tools\install-manifest-prototype\run_phase12d.py "C:\Games\Call of Duty - Modern Warfare 2 CR"
```

Return the complete console output and `tools\install-manifest-prototype\out\run.log`; the results report remains deferred until those outputs are available.
