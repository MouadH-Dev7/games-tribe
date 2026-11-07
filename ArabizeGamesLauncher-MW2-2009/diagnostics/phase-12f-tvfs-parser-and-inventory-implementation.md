# Phase 12F — TVFS Parser and Read-Only Inventory Implementation

## Status

**PARTIALLY IMPLEMENTED.** The artifacts are prepared for static review; real-format compatibility and synthetic test results remain unverified.

`run_phase12f.py` and an isolated synthetic test module were authored but neither was executed. This report does not claim that the installed game's TVFS root has been read or parsed successfully.

## Source files inspected

Local CascLib source snapshot: `C:\Users\mouad\Downloads\Compressed\CascLib-master\src`.

- `CascRootFile_TVFS.cpp`: header, table structures, path-node decoding, VFS span decoding, CFT references, recursive directory handling.
- `CascFiles.cpp`: build configuration field interpretation (`root`, `vfs-root`, `vfs-*`) and VFS-root list parsing.
- `CascOpenStorage.cpp`: root selection and TVFS dispatch.
- `CascIndexFiles.cpp`: V2 `.idx` naming, bucket/version selection.
- `CascStructs.h`: EKey width constants and V2 index structure declarations.
- `common\RootHandler.h`: TVFS signature.
- `CascRootFile_Install.cpp`: INSTALL parser entry point, used to distinguish INSTALL from TVFS.

Existing project sources inspected:

- `tools\install-manifest-prototype\run_phase12d.py`
- `tools\install-manifest-prototype\install_manifest_parser.py`
- `tools\install-manifest-prototype\test_install_manifest_parser.py` (header/description only; no test execution)
- `tools\install-manifest-prototype\test_phase12f_tvfs.py` (created this phase; not executed)

No files in the game installation were accessed. No network access or registry access occurred.

## CascLib findings

### `root`, `vfs-root`, and `vfs-1`

In `CascFiles.cpp` around lines 886–888, `root*` is loaded into `hs->RootFile`. Lines 913–918 load `vfs-root*` into `hs->VfsRoot` and numbered `vfs-*` entries into `hs->VfsRootList`. `LoadVfsRootEntry` around lines 478–505 parses the decimal number after `vfs-` and stores the entry by one-based index.

`CascOpenStorage.cpp` around lines 908–916 selects `hs->VfsRoot` when it has a content size, ahead of the legacy `RootFile`. `CascRootFile_TVFS.cpp` around lines 877–904 inserts the main VFS root, parses its directory data, and later processes VFS entries for identifying subdirectory roots. Therefore:

- `root` is a separate legacy ROOT reference, not an alias for the TVFS root.
- `vfs-root` is the primary TVFS root reference.
- `vfs-1` is a numbered VFS-root-list entry used by subdirectory detection. It is not an alias by definition.
- The supplied/reference config values make `vfs-root` and `vfs-1` share the same CKey/EKey and sizes; this is an observed equality to verify at runtime, not proof their fields have identical semantics.

The script stops if runtime `root` CKey, primary VFS keys, build product/name, or primary and numbered VFS sizes differ from the request's reference values. It does not use copied values to proceed after a mismatch.

### TVFS header and tables — corrected header size finding

`CascRootFile_TVFS.cpp`, `TVFS_DIRECTORY_HEADER` (around lines 38–83), and `CaptureDirectoryHeader` (around lines 170–233) support these interpretations:

- Signature bytes are `TVFS` (`CASC_TVFS_ROOT_SIGNATURE` is `0x53465654` in `common\RootHandler.h`).
- Format version is 1. The EKey and patch-key widths must match and be 9 or 16 bytes.
- The exact source comments in `CascRootFile_TVFS.cpp` are:

  ```cpp
  DWORD  EstTableOffset;                            // The offset of the encoding specifier table. Only if the write-support bit is set in the header flag
  DWORD  EstTableSize;                              // The size of the encoding specifier table. Only if the write-support bit is set in the header flag
  ```

  The source flag comments are:

  ```cpp
  #define TVFS_FLAG_WRITE_SUPPORT      0x0002         // Write support. Include a table of encoding specifiers. This is required for writing files to the underlying storage. This bit is implied by the patch-support bit
  #define TVFS_FLAG_PATCH_SUPPORT      0x0004         // Patch support. Include patch records in the content file records.
  ```

- `HeaderSize` is the total byte count of the header. `CaptureDirectoryHeader` first consumes the 4-byte signature and 4-byte version/size/key-width prefix, then uses:

  ```cpp
  pbDataPtr = CaptureByteArray(pbDataPtr, pbDataEnd, DirHeader.HeaderSize - FIELD_OFFSET(TVFS_DIRECTORY_HEADER, Flags), (LPBYTE)(&DirHeader.Flags));
  ```

  `Flags` is at offset 8, so this copies `HeaderSize - 8` bytes and brings the total consumed header to `HeaderSize`.
- The base layout ends at byte 38: the 8-byte prefix, 4-byte flags, six 4-byte path/VFS/CFT offset-and-size fields, and the 2-byte maximum depth. The documented extended layout ends at byte 46 when write support is set; patch support implies write support. The installed run's observed `HeaderSize=38` is consistent with the base layout, pending successful offline/runtime parsing.
- Source nuance: the code checks `HeaderSize < 8`, copies the variable-sized portion, and then unconditionally converts the EST members at lines 208–209. It does not branch on the write-support bit before those conversions. The parser follows the struct's explicit conditional-field comments and rejects lengths inconsistent with flags. This is a stricter, fail-closed interpretation of the source documentation, not a claim that CascLib validates those combinations itself.
- Flags are converted little-endian. Path/VFS/CFT offsets and sizes, maximum depth, and present EST offsets and sizes are converted with big-endian integer helpers.
- Table offsets are treated as offsets from the TVFS buffer. CascLib's legacy range checks at header capture are commented out; the new parser checks every present table range against the decoded buffer and rejects overlap. EST is range-checked only for the extended header; absent EST fields are not read as table values.
- CFT offset width is derived from CFT table size: 1 byte through `0xff`, 2 through `0xffff`, 3 through `0xffffff`, otherwise 4 (`GetOffsetFieldSize`, around lines 142–151).

### Path and reference records

`CapturePathEntry` (around lines 326–382) defines an optional zero-byte prefix separator, one-byte component length, component bytes, optional zero-byte postfix separator, and optional `0xff` node marker followed by a four-byte value. `ParseDirectoryData` (around lines 826–862) defines the root folder marker and big-endian folder length. Folder node values use the high bit; low 31 bits give folder length including the four-byte node value. The parser reconstructs paths, bounds every component and directory range, rejects duplicate or unsafe paths, and caps depth, component length, total path length, and path count.

`CaptureVfsSpanCount` (around lines 236–250) accepts span counts 1–224. `CaptureVfsSpanEntries` (around lines 252–306) describes each span as a big-endian 4-byte offset, big-endian 4-byte length, and a variable-width CFT reference. The CFT entry begins with the header-declared EKey width and has a following four-byte encoded-size field. The implementation validates each range and records the span offset, content size, CFT offset, EKey and encoded size without opening the referenced resource.

The source defines flags for embedded CKeys, patch support, write support, and lowercase paths, but this code path does not fully parse all optional CFT fields or patch/EST records. The inventory preserves the raw flags and table measurements in its JSON header report; it leaves the `content_key` CSV column empty instead of guessing optional CKey layout. Optional metadata beyond the verified EKey/encoded-size portion remains unparsed.

The source treats path components as byte strings, but the inspected code does not establish a text encoding contract. The new parser requires strict UTF-8 and stops on undecodable components. UTF-8 is therefore a fail-closed implementation assumption, not a verified property of this specific build.

## Reused Phase 12D code

The new script loads `run_phase12d.py` as a helper module with bytecode generation disabled, and reuses:

- V2 index parsing and EKey-prefix lookup (`parse_idx`). It examines only the newest available index version per bucket, expects the known 64 KiB index size, and requires exactly one matching VFS-root entry.
- Local 30-byte header validation (`validate_local_data_header`), including EKey comparison and full indexed-size versus config encoded-size-plus-header check.
- Bounded BLTE decoding (`decode_blte`) for modes N and Z, including chunk-table and per-chunk MD5 checks, decompression limits, and rejection of unsupported modes.

The INSTALL-specific `validate_blte_layout` was deliberately not reused: it hardcodes the 670-byte INSTALL stream and two chunks. The new script applies general checks using runtime VFS-root size, then calls the reusable decoder. The script checks the decoded VFS-root size and MD5 against runtime config before parsing.

Potential integration limit: loading the helper imports the complete Phase 12D module. That module has no main-path action on import, and `sys.dont_write_bytecode` is set first, but import behavior is still a dependency on the existing project file. If that file is moved or changed, this script fails closed.

## New parser and output design

Created `tools\install-manifest-prototype\run_phase12f.py`. It:

1. Accepts the game path as a required argument.
2. Reads `.build.info` and the active config with binary read-only opens, verifies the active `eu` product/build/version and the reference identities, and derives the config path from the verified active build key.
3. Reads index metadata only (newest index file per bucket), requires one VFS-root EKey match, and reads one bounded byte range from one `data.NNN` shard.
4. Verifies local header, BLTE structure, supported modes, per-chunk MD5 through the reused decoder, decoded MD5, and decoded size before parsing.
5. After VFS-root MD5 and decoded-size verification, prints the verified MD5/size and saves `out\phase12f\vfs_root.bin` before TVFS parsing. A malformed TVFS can be retried offline without rereading the game. Header/parse failures print the decoded blob's first 64 bytes and the header fields parsed so far.
6. Supports `--from-file out\phase12f\vfs_root.bin`; this mode opens only that exact saved file (bounded, `rb`) and never opens the game directory. Its JSON marks build, index, local-header, BLTE, and decoded-key validations as not performed in the offline invocation.
7. Parses only the TVFS path inventory and span/CFT references. It does not resolve or read any listed asset.
8. Writes `out\phase12f\vfs_resource_inventory.csv` and `out\phase12f\execution_report.json` after a complete successful parse. The report lists each input read path, offset, length and SHA-256.

The CSV uses the actual path suffix as `path_extension`; this is not a claim about the file's internal format. Content keys are left blank where the inspected source does not establish a safe extraction of that optional field. Resource type and Arabic classification are not inferred.

## Validation, tests, and limits

- The code was reviewed statically only; it was not run, imported, compiled, or unit-tested in this phase.
- Added synthetic coverage for valid 38-byte/no-EST and 46-byte/with-EST headers, truncated forms of both, a minimal path/CFT reference, bad signature, invalid VFS reference, duplicate path, invalid UTF-8 component, unsupported span count, malformed BLTE dimensions, and wrong decoded CKey/size. The tests were not run, so they provide no passing evidence yet.
- Real-game compatibility is unknown until the user runs the script and all build, index, local-header, BLTE, hash, and TVFS checks pass.
- The script does not read referenced localize, subtitle, dialogue, font, or audio payloads. A path inventory cannot establish locale or Arabic content.
- Nested TVFS assets are not followed because that would require another archive payload read beyond the single VFS-root region.
- The script does not parse optional patch/encoding-specifier tables or optional CKey data. Unsupported structures must not be treated as absent assets.

## Exact user command

From the project root, game-backed run:

```powershell
py tools\install-manifest-prototype\run_phase12f.py "C:\Games\Call of Duty - Modern Warfare 2 CR"
```

Offline retry (reads only the saved blob):

```powershell
py tools\install-manifest-prototype\run_phase12f.py --from-file tools\install-manifest-prototype\out\phase12f\vfs_root.bin
```

## Files read and changes

Created:

- `tools\install-manifest-prototype\run_phase12f.py`
- `diagnostics\phase-12f-tvfs-parser-and-inventory-implementation.md`

Modified:

- `tools\install-manifest-prototype\test_phase12f_tvfs.py`

No existing project code or game files, settings, or registry entries were modified. The new test module is separate from existing tests. No script, test, compiler, extractor, or game process was executed.
