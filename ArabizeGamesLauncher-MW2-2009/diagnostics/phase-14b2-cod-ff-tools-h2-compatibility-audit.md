# Phase 14B-2 — CoD-FF-Tools H2 / MW2CR Static Compatibility Audit

## Executive verdict

**INCOMPATIBLE for the target MW2 Campaign Remastered build.** This source tree implements FastFile handling for CoD4, World at War, and original MW2 PC/console formats. Its MW2 PC path is explicitly versioned as IW4 / MW2 (2009). No H2, MW2CR, Campaign Remastered, or `lazr` implementation or build check was found. Similar file extensions and the shared “MW2” name are not compatibility evidence.

The code does demonstrate a standalone input-file workflow for the older supported formats. That does **not** make it safe or technically justified to open the target's `.ff` files: the parser has no proven matching header/asset schema for H2, its decompression path lacks an explicit output cap, and no exact-build fixture or validation exists.

**Phase 14B-3 readiness: NOT READY.** Do not open `arara_ui.ff` or `english/eng_ui.ff` with this tool.

## 1. Repository identity and provenance

| Item | Finding |
|---|---|
| Project identity | `CoD FastFile Tools` / “Call of Duty FastFile Editor”; README describes extraction, editing, and building `.ff` archives (`README.md:1–3`). |
| Local source path | `C:\Users\mouad\Downloads\Compressed\CoD-FF-Tools-main` |
| Repository provenance | Source tree has no `.git` directory. The local snapshot cannot establish a remote URL, commit, branch, tag, or whether it exactly matches an upstream revision. README release links identify `primetime43/CoD-FF-Tools`, but that is a project claim/link, not local Git provenance. |
| Version | Project files declare version, assembly version, and file version `4.0.0` (`FastFileLib/FastFileLib.csproj:1–10`; `FastFileCLI/FastFileCLI.csproj:1–13`). This is the declared package version, not a verified source release/commit. |
| License | `LICENSE:1–3` identifies GNU GPL version 3, dated 29 June 2007. |
| Languages/build | C#/.NET 8 (`FastFileLib/FastFileLib.csproj:1–10`; CLI project `:1–17`); the tree includes a Visual Studio solution, library, CLI, WinForms editor/compiler/converter/extractor, and tests (README `:92–100`; source tree listing). |
| Distribution type | This directory is a source tree, not only a binary bundle: it contains `.cs`, project/solution files, docs, and tests. No binaries were executed. |
| Explicitly listed games | README `:5–22` lists CoD4, WaW, and MW2 across platforms; MW2 PC is marked `Partial`. There is no Campaign Remastered entry. |

The comparison source at `C:\Users\mouad\Downloads\Compressed\OpenAssetTools-main` also has no `.git` directory, so its local commit/branch cannot be independently identified from Git metadata.

## 2. Supported games and engine evidence

The README's supported-games table says MW2 PC is **Partial** (`README.md:5–16`). Its scope says “MW2: Modern Warfare 2,” and line 22 calls the PC retail auth-chunk structure “IW4.” The detailed format note says it was verified against 11 retail MW2 PC samples (`docs/MW2_PC_FastFile_Format.md:1–19`), describes the MW2 PC layout and asset enum (`:22–36`), and records signed/unsigned formats and compression (`:39–77`). The examples and sample names are original MW2 PC assets; no Campaign Remastered build or H2 sample is named.

The implementation's game enum is only `Unknown`, `CoD4`, `WaW`, `MW2` (`FastFileLib/GameVersion.cs:1–12`). Header detection maps MW2 version constants to `GameVersion.MW2` and platform labels (`FastFileLib/FastFileInfo.cs:294–315`); the MW2 PC version path maps to a generic game name `MW2` (`:301–307`). It has no separate H2 or Campaign Remastered identity and no check for product `lazr`, `1.1.2.1279292`, or `CRC_1279292A_retail_signed_ship`.

A case-insensitive search of this source tree for `H2`, `MW2CR`, and `Campaign Remastered` found no H2-specific implementation or documentation reference. The documented MW2 PC version is `0x114` / little-endian (`FastFileInfo.cs:301–307, 357–358`; `docs/MW2_PC_FastFile_Format.md:10–12`). That is evidence for the source's stated older MW2 format, not the target build.

## 3. FastFile architecture and implementation evidence

### Header and decompression

- `FastFileInfo.FromReader` reads an 8-byte magic and 4-byte version, tries big-endian and then little-endian version interpretation, and maps recognized constants (`FastFileLib/FastFileInfo.cs:193–250`). The `MW2` detection cases and PC platform mapping are at `:294–315`.
- `FastFileProcessor.Decompress` opens an input path for read and a caller-supplied output path with `FileMode.Create` (`FastFileLib/FastFileProcessor.cs:44–53`). It dispatches MW2 PC to `TryDecompressMW2PC` based on `GameVersion.MW2 && info.IsPC` (`:67–82`). This is standalone path-based parsing for the tool's recognized formats; it does not itself require a running game.
- `TryDecompressMW2PC` routes to the MW2 PC auth-chunk/single-zlib implementation with stream offset `0x15` (`FastFileProcessor.cs:709–739`). `TryDecompressMW2Authed` reads the entire input into a byte array, extracts signed/unsigned payload, and uses `ZLibStream.CopyTo` into the output without an explicit decompressed-output limit (`:739–773`). The signed payload routine allocates a buffer based on the input size and skips one authentication hash chunk in each group instead of validating that hash (`:785–840`, especially `:801–824`).
- The documentation describes both unsigned single-zlib and signed “authed chunks” MW2 PC input, and notes that saving signed retail inputs produces unsigned output because the private signing key is unavailable (`docs/MW2_PC_FastFile_Format.md:10–19, 61–77`). This is again specific to the older PC format.

### Asset structures and extraction

- The README says the MW2 PC asset set is partial: `rawfile` and `localize` are supported for parse/view/edit, several other types are view-only, and unsupported types may be detected but not parsed (`README.md:24–42`). It also warns that zone rebuilds preserve only rawfile/localize while other asset types may be lost (`:42`).
- The MW2 raw-file parser checks the 16-byte record header, length caps, filename, and that the declared data lies inside the in-memory zone (`Call of Duty FastFile Editor/GameDefinitions/MW2GameDefinition.cs:157–210`). It then allocates the declared compressed or uncompressed data and, for compressed data, calls its zlib decoder (`:220–244`). This is a raw-file scanner, not evidence that the H2 asset pool/table structures match.
- `StructureBasedZoneParser.Parse` uses counts read from its zone model and attempts to locate/read tag and asset sections (`Call of Duty FastFile Editor/ZoneParsers/StructureBasedZoneParser.cs:60–110`). Its parser is platform/game-definition driven; no H2 variant was found.
- The source includes `AssetPoolParser`, `LocalizeAssetParser`, `StringTableParser`, `RawFileParser`, and other older-game asset handlers. Their presence and the MW2 PC feature table establish only the explicitly documented IW4-era formats; names alone do not prove H2 compatibility.

No H2-specific header structure, H2 version gate, H2 AssetPool type enum, H2 Localize/StringTable layout, or exact-build signature was found. The source has no parser path that can be demonstrated to match `MW2CR.exe` build `1.1.2.1279292`.

## 4. Capability table

| Capability | Result | Evidence / limit |
|---|---|---|
| Reads FastFile headers | **YES, for supported older formats** | `FastFileInfo.FromReader` reads magic/version and selects known version constants (`FastFileInfo.cs:193–250, 253–315`). Not a generic guarantee for arbitrary `.ff` files. |
| Parses original MW2 (2009) FastFiles | **PARTIAL** | README `:11–16`; MW2 PC and console definitions/version paths in `FastFileInfo.cs:294–315` and `FastFileProcessor.cs:67–94`. |
| Parses H2 FastFiles | **NO EVIDENCE / NOT SUPPORTED** | No H2/MW2CR implementation or format entry was found; game enum only has CoD4/WaW/MW2 (`GameVersion.cs:6–12`). |
| Parses MW2CR FastFiles | **NO** | The project identifies MW2 PC as IW4-era `MW2`; no Campaign Remastered schema or build check. |
| Supports build `1.1.2.1279292` | **NO EVIDENCE** | No target version/build identifier or fixture in the local source. |
| Standalone offline parser | **YES for recognized formats** | Path-based `Decompress(inputPath, outputPath)` opens input read-only and output as a new/truncated file (`FastFileProcessor.cs:44–53`); no process required in this code path. It is not thereby compatible with H2. |
| Extracts individual assets | **PARTIAL for documented asset types** | README asset support table `:24–40`; MW2 parser shown for raw files. H2 asset extraction is unestablished. |
| Requires a running game | **NO, for this standalone file path** | The inspected parsing path accepts file paths and contains no process attach in the cited code. This finding is scoped to the inspected source path. |
| Requires process memory/runtime hooks | **NO evidence in this parser path** | No process APIs or hooks in the inspected decompression/parser files. |

## 5. Static safety findings

### Positive, scoped properties

- The main decompressor opens its input with `FileAccess.Read` (`FastFileProcessor.cs:44–47`). The inspected MW2 raw-file scanner checks the header and data range against the zone buffer and caps declared lengths at 10,000,000 bytes (`MW2GameDefinition.cs:159–210`).
- For the path-based decompress method, the output is a separate caller-provided path and is created/truncated with `FileMode.Create` (`FastFileProcessor.cs:44–47`). This method does not itself overwrite the input unless the caller supplies the same path; there is no safety check shown here that prevents such a caller mistake.

### Risks and unknowns

- **Unbounded decompression:** the MW2 decompressor copies decompressed zlib output until the stream ends (`FastFileProcessor.cs:755–759`) with no explicit maximum output size in that routine. An input cap or bounded destination is not enforced there.
- **Whole-file allocation:** `TryDecompressMW2Authed` casts stream length to `int` and reads the complete file into memory (`:743–750`). No pre-allocation file-size limit is visible in this path. Oversized files may fail or exhaust memory; integer conversion behavior also warrants review before untrusted input.
- **Authentication checks:** the auth payload walker deliberately skips group hash chunks rather than verifying them (`:801–824`). The format notes disclose the signed layout but do not make this a verified authenticity check.
- **Asset parsing:** the raw-file path has useful bounds and length checks, but this does not validate all zone structures or prove hostile/truncated input safety in every parser. The structure parser's tag-count mismatch branch says it continues anyway (`StructureBasedZoneParser.cs:93–101`). No full fuzzing or runtime behavior was tested.
- **Writes:** the library includes editing, compiling, converting, and saving functionality (README `:44–52`; `FastFileProcessor.cs` also has recompression methods). This audit did not execute any path. Any future use would need a separate output directory and an explicit no-overwrite policy.
- **H2 parsing:** because target layout support is absent, applying this parser risks interpreting unrelated bytes as older MW2 structures. Do not use it to test the candidate payloads.

This is a static review, not a proof that all code paths are memory-safe or that output is confined to a workspace.

## 6. Comparison with OpenAssetTools

Local OAT source: `C:\Users\mouad\Downloads\Compressed\OpenAssetTools-main`.

- OAT's README says it targets older Call of Duty titles and explicitly lists IW4 as **Modern Warfare 2 (2009)**; it describes its Unlinker as standalone for supported games (`README.md:8–20, 28–36`).
- The source tree has game-specific loader directories IW3, IW4, IW5, QOS, T4, T5, and T6; there is no H2 directory in the inspected game list.
- Its IW4 loader checks IW4 zone version/magic and identifies the result as `GameId::IW4` (`src/ZoneLoading/Game/IW4/ZoneLoaderFactoryIW4.cpp:46–106`). It does not identify Campaign Remastered or the `lazr` build.
- CoD-FF-Tools' MW2 format research note explicitly cites OAT's IW4 loader and authentication-block processor as references (`CoD-FF-Tools-main/docs/MW2_PC_FastFile_Format.md:3–19`). This establishes a documented reference relationship for MW2 PC format research, not shared code and not support for H2.
- Both local snapshots lack `.git` metadata, so exact source revision comparison, ancestry, and code-sharing claims cannot be established here. They target the same broad original MW2/IW4 family, not the H2 target.

**Comparison result:** OAT is also not established as an H2/MW2CR parser. Its standalone loading capability applies to its supported games; it cannot fill the missing target-build evidence for this tool.

## 7. Phase 14B-3 readiness and proof required

**Decision: NOT READY.** A copied-file test is not technically justified with this source because its only concrete MW2 PC implementation targets original IW4-era MW2. Static source review has not demonstrated matching H2 headers, compression/encryption, asset tables, or target build identifiers.

Before any payload is opened, the following must be proven from a legitimate source implementation and/or exact-build synthetic fixtures:

1. A parser explicitly supports H2 / MW2 Campaign Remastered and a format revision demonstrated to match build `1.1.2.1279292`; a generic “MW2” or `.ff` claim is insufficient.
2. The parser's exact header, version/build gate, compression/encryption path, and asset-table schemas match this target; unsupported variants fail closed.
3. All archive-controlled offsets, sizes, counts, and allocations have bounds/overflow checks, and decompression has a strict output cap.
4. The parser accepts copied standalone files without a game process, memory access, hooks, injection, network, or writes to source inputs.
5. Synthetic valid, truncated, malformed, oversized-length, out-of-range-table, and decompression-expansion cases are reviewed or validated without touching game assets.
6. The eventual test has an isolated output directory, input and output size caps justified by the reviewed implementation, SHA-256 records for verified copies, and no recursive extraction. The originals remain untouched.

## 8. Actions not performed

- Did not execute any repository program, build, compile, install dependencies, or run tests.
- Did not access the game installation, game process, registry, or configuration.
- Did not open any game `.ff`, `.pak`, CASC index/shard, or asset payload.
- Did not access the network, download, or extract archives.
- Did not modify either source tree, launcher files, or project configuration.

## 9. Remaining unknowns

- The upstream commit/branch/tag corresponding to either local source snapshot.
- Whether the target `.ff` files share any outer structure with original IW4 FastFiles; extensions alone cannot answer this.
- MW2CR/H2 header and asset-table schema, compression/encryption variants, and exact-build identifiers.
- Whether a different legitimate standalone H2 parser exists and is compatible with this build.
- Whether a parser could recover the specific UI/localization assets losslessly while preserving unknown fields.

**Report status:** static findings are limited to local source/docs and earlier Phase 14A context. No runtime compatibility or payload behavior is claimed.
