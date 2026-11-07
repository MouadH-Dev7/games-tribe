# Phase 14D — H2 Mod Source Audit for MW2 Campaign Remastered Compatibility

## Executive verdict

**USEFUL H2 REFERENCE, BUT NO STANDALONE PARSER.**

This is a real H2 / MW2 Campaign Remastered mod framework, not a FastFile parser distribution. The README explicitly says the mod is for *Call of Duty: Modern Warfare 2 Campaign Remastered* (`README.md:13–19`). The source includes H2 runtime asset type definitions, structures for LocalizeEntry, RawFile, StringTable and TTF, and functions that ask the game's DB to load zones and enumerate in-memory assets (`src/client/game/assets.hpp:24–97, 1664–1668, 2071–2101`; `src/client/game/symbols.hpp:49–64`; `src/client/component/fastfiles.cpp:654–723`).

That is useful H2-specific reference material. It does not provide a demonstrated offline `.ff` parser. Its zone-loading path calls the running game's `DB_LoadXAssets`; its hooks and game symbols use fixed runtime addresses (`fastfiles.cpp:729–774`; `symbols.hpp:49–64`). The source contains no standalone code path that parses a copied FastFile header, walks an on-disk H2 asset pool, or extracts assets without loading the game.

The code names `MW2CR.exe` and checks one hardcoded in-memory value (`src/client/main.cpp:118–138, 146–153`), but it does not identify the expected product `lazr`, installed version `1.1.2.1279292`, or build name `CRC_1279292A_retail_signed_ship`. The hardcoded runtime check cannot be mapped to that exact build from the available source alone.

**This phase does not authorize opening any game payload.**

## 1. Repository identity and provenance

| Item | Finding |
|---|---|
| Local source | `C:\Users\mouad\Downloads\Compressed\h2-mod-develop` |
| Project identity | README names it **H2: Mod** (`README.md:6`) and links badges/source to `alicealys/h2-mod` (`README.md:1–3`). |
| Purpose | Runtime modification framework for MW2 Campaign Remastered. README installation says place/run its executable in the Campaign Remastered folder and accept update installation (`README.md:13–19`). |
| Upstream URL | The local README documents `https://github.com/alicealys/h2-mod` in badge and action URLs (`README.md:1–4`). No network lookup was made. |
| Git metadata | No `.git` directory exists in this snapshot. Remote, commit, branch, and tags are therefore **NOT VERIFIED**. The folder name `h2-mod-develop` is not proof of a Git branch. |
| Version/release | No source version or release ID can be tied to this snapshot. `premake5.lua:1–2, 81–100, 124–190` derives version/build identifiers from Git metadata and generates build information; without `.git`, that provenance is unavailable. README refers to latest release/develop downloads but does not identify the local snapshot's release (`:17, 33–37`). |
| License | `LICENSE:1–3` is GNU GPL version 3, dated 29 June 2007. |
| Languages/build | C++ client/runtime code plus Lua mod scripts/data; Premake workspace and projects (`premake5.lua:222–240, 271–320`). The project defines C++ latest and x64, `client` as a ConsoleApp (`:283–300`), and `tlsdll` as a SharedLib (`:312–320`). |
| Repository type | Full source/mod framework plus mod data and an included Premake executable by filename. It is not a standalone offline-parser tool collection. The executable was not opened or run. |
| Submodules | `.gitmodules:1–54` lists dependencies including MinHook, zlib, curl, Lua, and others. ZoneTool is credited in README (`:55–60`) but is not listed as a submodule in the inspected `.gitmodules`. The `data/zonetool` tree is mod input/data, not the ZoneTool parser source. |

## 2. H2 implementation evidence

### Runtime engine integration

- `src/client/main.cpp:118–138` selects `MW2CR.exe` (with a Battle.net-named fallback), reads that executable into a buffer, prepares a patched copy in the mod's app-data directory if needed, and loads/maps it through the custom loader. `main.cpp:146–153` checks a DWORD at fixed address `0x140123456` against `0xE465E151`, otherwise reporting an unsupported Campaign Remastered version.
- The check is H2-specific runtime compatibility logic, but the source does not state which retail build corresponds to `0xE465E151`. It is not a build-name/version check for `1.1.2.1279292` or `CRC_1279292A_retail_signed_ship`.
- `src/client/game/symbols.hpp:49–64` binds DB functions such as `DB_EnumXAssets_Internal`, `DB_LoadXAssets`, `DB_FindXAssetHeader`, and `DB_GetAllXAssetOfType` to hardcoded virtual addresses.
- `src/client/component/fastfiles.cpp:32–45, 654–723` hooks and calls engine FastFile/DB functions, enumerates engine asset entries, checks zone files through the game filesystem, and loads a zone through `DB_LoadXAssets`.
- `fastfiles.cpp:729–774` installs detours, NOPs, jumps, calls, and memory patches at fixed executable addresses. This is a runtime hook/patch architecture, not a read-only offline parser.

### H2 asset types and data structures

`src/client/game/assets.hpp:24–97` defines the H2 `XAssetType` enumeration, including `ASSET_TYPE_LOCALIZE_ENTRY`, `ASSET_TYPE_RAWFILE`, `ASSET_TYPE_STRINGTABLE`, and `ASSET_TYPE_TTF`. It defines the runtime LocalizeEntry as two pointers (`:1664–1668`), RawFile with name/length/buffer fields (`:2071–2077`), and StringTable with row/column counts and a cell pointer (`:2089–2101`). These structures are useful for understanding H2 runtime objects, but they describe in-memory objects, not serialized `.ff` layouts or offsets.

### UI asset access is runtime-backed

- `src/client/component/gui/assets/stringtable.cpp:25–66, 68–133` walks a `game::StringTable*` and copies its displayed contents to the clipboard. It receives an already materialized game asset; it does not decode a FastFile.
- `src/client/component/gui/assets/rawfile.cpp:19–52, 57–64` reads a `game::RawFile*`, decompresses its buffer, displays it, and permits clipboard copying. This also operates on a runtime asset pointer, not a standalone file.
- The source tree has no `localize` GUI asset module matching the StringTable and RawFile viewers. Although the runtime type exists, a LocalizeEntry extraction workflow is not established by these files.
- `src/client/component/fonts.cpp:183–253` uses DB lookups, loads font zones, and retrieves TTF assets through the game engine. That supports runtime font handling, not standalone `.ff` font extraction.

### ZoneTool inputs are not a parser implementation

The local tree contains `data/zone_source` CSVs with H2 mod-zone names and `data/zonetool` input data. The README credits ZoneTool (`README.md:55–60`), but `.gitmodules` does not include ZoneTool and the source projects do not expose a standalone ZoneTool/Unlinker executable. Such data may be intended as input for an external toolchain; it does not prove that this repository can read original H2 FastFiles offline.

## 3. Parser and extraction capabilities

| Capability | Finding | Basis |
|---|---|---|
| H2 game modification framework | **YES** | README explicitly targets Campaign Remastered (`README.md:13–19`); client and runtime components in `src/client`. |
| H2 runtime loader / hooks | **YES** | Custom executable loader and runtime addresses/hooks (`main.cpp:95–138`; `loader/loader.cpp:9–31, 53–70`; `fastfiles.cpp:729–774`). |
| H2 FastFile/Zone header parser | **NOT FOUND** | No standalone on-disk header reader, format structure, or H2 file signature/version parser was found in the source tree. Zone loading is delegated to engine DB (`fastfiles.cpp:705–723`). |
| Standalone offline FastFile reader | **NO EVIDENCE** | No parser entry point accepts an input file and decodes H2 `.ff` without a game process. |
| Runtime AssetPool access/enumeration | **YES** | H2 asset enums/structures and DB enumeration symbols; runtime asset iteration uses engine hash/entry pools (`assets.hpp`; `symbols.hpp:49–64`; `fastfiles.cpp:654–693`). |
| On-disk AssetPool parser | **NOT FOUND** | In-memory pool traversal is not a serialized file parser. |
| Individual asset viewing/extraction | **PARTIAL, runtime only** | StringTable and RawFile viewers display/copy live asset data (`gui/assets/stringtable.cpp:25–65`; `gui/assets/rawfile.cpp:19–52`). No general standalone extractor. |
| Asset serialization/rebuilding | **NOT ESTABLISHED in this repository** | The tree includes mod-zone input data, but no demonstrated offline `.ff` writer/parser pipeline. External ZoneTool is credited but not included as parser source. |
| Localization/string extraction | **PARTIAL, runtime-backed** | StringTable display/copy and H2 localization structures exist; no verified LocalizeEntry extraction or offline extraction path (`assets.hpp:63, 1664–1668`; `stringtable.cpp:25–65`). |
| Arabic font use | **H2 mod data includes an Arabic-named font-zone source/input** | `data/zone_source/ara_h2_mod_font_default_bold.csv` and corresponding `data/zonetool/ara_h2_mod_font_default_bold` paths exist by name. This does not prove glyph coverage, RTL shaping, complete translation, or applicability to the original assets. |

## 4. Exact target compatibility matrix

| Target property | Classification | Evidence and limit |
|---|---|---|
| MW2 Campaign Remastered title | **COMPATIBLE (project target)** | README explicitly names the game and installation (`README.md:13–19`). This identifies the intended game family, not parser compatibility. |
| Product `lazr` | **UNKNOWN** | No `lazr` identifier was found in the reviewed project source/docs. |
| Version `1.1.2.1279292` | **UNKNOWN** | No literal version/build identifier or mapping from the hardcoded runtime sentinel was found. |
| Build `CRC_1279292A_retail_signed_ship` | **UNKNOWN** | No build-name reference or fixture was found. |
| Executable `MW2CR.exe` | **PARTIALLY COMPATIBLE** | Main source explicitly selects this filename (`main.cpp:118–129`) and has an H2 version guard (`:146–153`), but the guard's sentinel is not identified as the requested exact build. |
| Standalone parsing of target `.ff` files | **INCOMPATIBLE / NOT IMPLEMENTED** | No offline file parser; runtime loading delegates to the game's DB and accesses in-memory assets. |

Support for the Campaign Remastered title in the mod's runtime loader must not be read as support for reading archived `.ff` files independently or as validation against the specified installed build.

## 5. Safety and architecture audit

### Process memory, hooks, and runtime patches

- `src/client/loader/loader.cpp:9–31, 53–70` maps executable sections, writes them into a target memory image, changes page protections with `VirtualProtect`, and patches imports/entry data.
- `src/client/component/fastfiles.cpp:729–774` creates hooks and writes jumps, calls, NOPs, and constants into the game executable at fixed addresses.
- `src/client/game/symbols.hpp:49–64` exposes engine DB and asset functions at fixed addresses. These are runtime symbols, not portable file offsets.
- `src/client/main.cpp:146–153` dereferences a fixed address for version validation. No source-level link between its expected value and the user's build is available.

These properties make the source unsuitable for the requested no-process standalone inspection. They are not a finding about whether the project is safe or appropriate for its intended mod use; they are an architectural mismatch for this diagnostic task.

### Bounds, allocation, and malformed data

There is no standalone FastFile parser in which archive-controlled offsets, table counts, or allocation caps can be audited. Therefore those parser-safety properties are **NOT APPLICABLE / NOT VERIFIED**, rather than passed.

Some runtime viewers do consume engine-owned asset pointers. The RawFile viewer passes the runtime buffer range to the zlib helper and stores decompressed output in an unbounded `std::string` (`gui/assets/rawfile.cpp:19–34`; `common/utils/compression.cpp:55–93`). The StringTable copy loop trusts runtime row/column counts and cell pointers (`gui/assets/stringtable.cpp:25–65`). These are not standalone archive readers and do not provide hostile-file validation evidence.

### Writes, registry, and network

- Main can write a patched copy under the mod app-data directory and removes a crash marker (`main.cpp:72–90, 141–144`).
- Main creates/opens an Image File Execution Options registry key and writes `MaxLoaderThreads` (`main.cpp:168–188`).
- The project contains HTTP/curl code and updater/MOTD-related components; `.gitmodules:43–45` declares curl, and source searches identify HTTP download/write paths (`src/common/utils/http.cpp`; `src/client/component/motd.cpp`). Startup/network behavior was not executed or exhaustively traced in this audit.
- GUI/runtime functionality can write files, and the repository's purpose includes loading mod content. None of those behaviors were run here.

## 6. Comparison with previous tools

| Tool | Layer / architecture | Relationship to this source | Relevance to offline H2 `.ff` parsing |
|---|---|---|---|
| **h2-mod-develop** | H2 runtime mod/client framework; relies on the game's DB and in-memory asset types; installs hooks. | Adds H2-specific runtime definitions and asset access references absent from generic CASC readers. | **No standalone parser demonstrated.** Useful for understanding H2 runtime assets and load conventions only. |
| **OpenAssetTools** | Standalone Unlinker/Linker for its supported older games, including IW4/MW2 (2009). | Its IW4 support is a different game/engine generation; no H2 loader was established in the previous audit. | Does not fill the H2/Campaign Remastered parser gap. |
| **CoD-FF-Tools** | Standalone FastFile tools for older IW formats; MW2 PC implementation is original MW2/IW4. | Prior audit found no H2 structures/build gate. | Not compatible evidence for target H2 files. |
| **CascLib** | CASC storage/index/TVFS layer. | Reads outer storage, not H2 asset schemas. | Cannot parse internal `.ff` assets by itself. |

H2-Mod contributes the missing **H2 runtime/asset-model reference layer**, but it does not contribute the missing **standalone H2 file parser**. README credit to ZoneTool does not establish that ZoneTool's parser source is part of this snapshot.

## 7. Relevance to existing inventory candidates

The candidate paths/sizes (`arara_ui.ff` 248 decoded bytes, `english/eng_ui.ff` 248 bytes, `arara_common.ff` 21,336 bytes) are taken only from the previously established inventory supplied in project context; none of those payloads was opened here.

- H2-Mod's runtime `DB_LoadXAssets`/asset-viewer architecture could potentially help inspect assets **after the game engine has loaded a zone**, but this audit does not verify its use against the exact installed build or these candidates.
- The project contains runtime types for LocalizeEntry, RawFile, StringTable, and TTF, but the StringTable and RawFile GUI components require live asset objects. The existence of those types does not show that `arara_ui.ff` contains any of them or that all three candidate files are supported.
- No source evidence establishes that `arara_ui.ff`, `english/eng_ui.ff`, and `arara_common.ff` have identical H2 zone layouts or can be read by one offline parser.
- The Arabic-named H2 mod data files are mod inputs/content. They do not establish that the installed game contains Arabic assets, that these candidate files are replacement targets, or that the mod's Arabic resources are complete or compatible with the target retail build.

## 8. Recommended next phase

Use this tree as a **static H2 runtime reference**, not as a parser. A later parser investigation should locate and audit the actual ZoneTool/Unlinker source that understands H2 serialized zones, or another legitimate standalone H2 parser. Before any payload is opened, establish:

1. Verifiable source identity/commit and a specific standalone read-only file entry point.
2. H2 serialized FastFile/Zone header, build/version discrimination, asset-pool and string/localize schemas.
3. Bounds/overflow checks for file offsets, counts, pointers, and allocation sizes, plus strict decompression-output limits and fail-closed malformed-input handling.
4. Evidence that parsing requires no running game, process memory, injection, hooks, network, or source-file writes.
5. Synthetic-file validation for valid/truncated/malformed headers, out-of-range tables, excessive counts, and decompression bombs.
6. Only after those are documented, a separately authorized test using verified copies outside the game directory, SHA-256 records, strict input/output caps, and inventory-only operation before any asset extraction.

**No payload inspection is authorized by this report.**

## 9. Actions not performed

- No executable, script, Premake program, or build tool was run; no compilation, dependency installation, or tests were performed.
- No game installation directory, game process, CASC shard/index, `.ff`, `.pak`, or asset payload was accessed.
- No network request or download was made; no archive was extracted.
- No registry, game configuration, launcher, source tree, or other project file was modified.

## 10. Remaining unknowns

- Exact upstream commit/branch/tag and release identity of the local `h2-mod-develop` snapshot.
- Which precise MW2CR retail build the fixed-address version sentinel represents.
- Whether its in-memory LocalizeEntry/StringTable/RawFile/TTF definitions match the supplied retail build exactly.
- The serialized H2 `.ff`/Zone format, asset-pool layout, compression/encryption variants, and whether a standalone parser exists in another source tree.
- Whether the three candidate resources contain Localize, StringTable, RawFile, UI, font, or other asset types.
- Whether the Arabic mod input files contain complete Arabic strings or suitable font glyphs, and whether the renderer supports shaping/RTL.

**Scope statement:** all conclusions are static and limited to the local source snapshot, its documentation, and the inventory metadata already provided by the project. No runtime compatibility is claimed.
