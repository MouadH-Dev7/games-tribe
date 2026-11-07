# CASC parser compatibility assessment

**Target:** `mw2-campaign-remastered-pc`  
**Storage product:** `lazr`  
**Build:** `1.1.2.1279292` / `CRC_1279292A_retail_signed_ship`  
**Assessment date:** 2026-11-03

## Decision

**BLOCKED — compatibility is not established for this exact storage/build.** No parser was present on the command path, no parser was run against the installation, and nothing was downloaded or installed. Consequently, this phase did not enumerate or extract archive entries.

## Tool evidence

| Tool/source | Version assessed | Status and evidence |
|---|---|---|
| CascLib, `ladislav-zezula/CascLib` | Upstream release 3.0; repository README on `master` reviewed 2026-11-03 | **VERIFIED:** upstream describes an open-source library for reading Blizzard CASC storages and documents builds/API use. **NOT VERIFIED:** no upstream statement or test evidence found for product `lazr`, Campaign Remastered, build `1.1.2.1279292`, or `CRC_1279292A_retail_signed_ship`. General CASC support does not prove this storage is supported. |
| CascView / CascExplorer | No local version found or assessed | **NOT VERIFIED:** not installed on the command path. No exact-build compatibility evidence established in this phase. |
| `casc-modern` | No local version found or assessed | **NOT VERIFIED:** not installed on the command path. Its project description says it consumes storages CascLib can open; this does not expand or prove CascLib compatibility. |
| QuickBMS | No local version found or assessed | **NOT VERIFIED:** no version or script was selected. It is not an acceptable substitute without a known parser script and exact-build evidence. |

The upstream CascLib README identifies its scope as reading Blizzard CASC storage generally and describes compiling the library; it does not publish a supported-product/build matrix in the reviewed README. The upstream release page lists release 3.0. That release label is the upstream version assessed here, not a locally installed binary version.

## Read-only and compatibility limitations

**INFERRED:** a purpose-built inventory program using an archive library's open/list/read APIs can be constrained to read operations at the application level. That does not, by itself, prove the library never writes caches or other files, nor that it correctly interprets this game's encoding/index variations. Safe use would require source/API review, a disposable copied metadata workspace, and validation against known local hashes before opening the original storage.

**NOT VERIFIED:** whether CascLib can correctly read this installation's CDN config, `.index` files, `.idx` indices, and numbered shards; whether all content is locally available; and whether any version-specific encoding, key, or manifest requirements apply. No parser command was run against these files.

## Commands and method

Read-only local command discovery attempted for `CascView`, `CascView2`, `CascExplorer`, `CASCExplorer`, `casc-modern`, `CascLib`, and `QuickBMS` using PowerShell `Get-Command`. No executable was returned. The earlier archive investigation also found no such parser in the examined game and standard Program Files locations. That search is not a complete forensic inventory of every volume or user directory.

No install, download, clone, build, archive open, or extraction command was run. The source reviewed was [CascLib README](https://github.com/ladislav-zezula/CascLib/blob/master/README.md) and [CascLib releases](https://github.com/ladislav-zezula/CascLib/releases). `casc-modern` project scope was checked at [TrayHard/casc-modern](https://github.com/TrayHard/casc-modern).

## Integrity observations

No archive parser operation or archive copy was performed, so there is no before/after operation pair. Existing diagnostic baseline records 42 small index/config files plus `Data\\data\\data.000`; it explicitly does not cover the other 64 large shards. The previously recorded `MW2CR.exe` SHA-256 is `B0B9909199051ECF1E24A56F6A6F93F47364549F32164F7698DB789D271D8C57`; a fresh read-only hash on 2026-11-03 matched it. See `mw2-campaign-remastered-archive-hash-baseline.json` for the partial archive baseline.

## Gate

Do not open or extract the installed archives until a specific parser/version has documented or reproducible support for this exact `lazr` storage/build, is reviewed for unintended writes, and is approved for a copied diagnostic workspace. This report does not authorize installation or translation replacement.
