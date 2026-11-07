# Phase 11 — MW2CR CASC Manifest & Locale Inventory

**Inspection date:** 2026-11-03  
**Scope:** Read-only inspection of named plaintext installation metadata and filesystem names/sizes. No CASC payload was opened, decoded, decompressed, or exported; no index binary was parsed; no game/tool/registry was run or changed; no network access was used.

## 1. Executive Summary

The local installation metadata independently confirms the active product as `lazr`, branch `eu`, version `1.1.2.1279292`, and build name `CRC_1279292A_retail_signed_ship`. Its locale-tag expression includes `arSA text` and `arSA speech` as conditional tag alternatives.

The active Build Config and `.build.info` reference an install manifest by CASC content key. No standalone file named with that key was found under `Data`. The manifest contents were not accessed because doing so would require a CASC-content lookup/read, outside this phase’s restrictions. No manifest entries associate `arSA` with individual files. No Arabic text or speech resource has been identified from this inventory.

**Final verdict: `MANIFEST NOT LOCALLY ACCESSIBLE`** — the referenced install manifest is not available as a standalone named metadata file; establishing its entries requires reading CASC-managed content, which this phase explicitly forbids.

## 2. Verified Installation Identity

### CONFIRMED

File: `C:\Games\Call of Duty - Modern Warfare 2 CR\.build.info`

Relevant active row fields:

- `Branch`: `eu`
- `Active`: `1`
- `Build Key`: `ebd38497ab7dd9895822a094a31c0457`
- `CDN Key`: `29dcfc9413388701c4144c2e75aee912`
- `Install Key`: `9f86d96c2ae820eb5164d6e12f95ebf6`
- `CDN Path`: `tpr/lazr`
- `Version`: `1.1.2.1279292`

The file `C:\Games\Call of Duty - Modern Warfare 2 CR\Data\config\eb\d3\ebd38497ab7dd9895822a094a31c0457` is selected by that active Build Key. Its fields include:

- `build-product = lazr`
- `build-name = CRC_1279292A_retail_signed_ship`
- `install = 3a64e6f6c3d81ac35f9bf6d7c86e6a00 9f86d96c2ae820eb5164d6e12f95ebf6`
- `install-size = 663 670`

This independently verifies the supplied product/version/build identity against local plaintext metadata. `MW2CR.exe` was not re-inspected in this phase because the question concerns install metadata; it was not run.

### SHA-256 hashes

| File | SHA-256 |
|---|---|
| `C:\Games\Call of Duty - Modern Warfare 2 CR\.build.info` | `8E4876B7287CF4ACD37A8E5A9FB0734D3B85E8DFA3B0FA9D09D1FD724C35784E` |
| Active Build Config `...\Data\config\eb\d3\ebd38497ab7dd9895822a094a31c0457` | `9BC6952F406C86313D406D174B583E42640822BDFBAC06DD97B7FBCAE48FE9F7` |
| Active CDN Config `...\Data\config\29\dc\29dcfc9413388701c4144c2e75aee912` | `B8F1A83EE4EFDE4FF71F7FAAA147C361C23DFF5019915758E88514A3AC052259` |
| Other local CDN Config `...\Data\config\7a\62\7a626e70e3745a28974f68885684d819` | `0CBD0F4C997D4909EECF2A7F2B18EF9F98E88C00359B32287058D06437D99F85` |

These are fingerprints of the inspected local files, not publisher signature verification.

## 3. Manifest Discovery

### CONFIRMED

The active manifest references are present in the plaintext metadata:

- `.build.info` `Install Key`: `9f86d96c2ae820eb5164d6e12f95ebf6`
- Active Build Config `install`: `3a64e6f6c3d81ac35f9bf6d7c86e6a00` and `9f86d96c2ae820eb5164d6e12f95ebf6`
- Active Build Config `install-size`: `663 670`

The active CDN Config at `Data\config\29\dc\29dcfc9413388701c4144c2e75aee912` contains:

- `archives = 1a549948b3ce22ed576167195e15f9d5 cf6e06ef1e970d2ee2db419be30f5747`
- `archives-index-size = 16508 4148`
- `archive-group = 3d42e8d5e6c7f42a569e76fc29d35b21`
- `file-index = b73c8aa0a9e95880599089d4e8ff933f`
- `file-index-size = 8268`

Named local files corresponding to those CDN index references are present under `Data\indices` (name/size inventory only). A second local CDN Config, key `7a626e70e3745a28974f68885684d819`, refers to archive key `1a549948b3ce22ed576167195e15f9d5`, archive group `3d663dc393ccb6bb31c7602cc33bf020`, and file index `4ca6087547a4a5f5d22b51eb4aa7c07a`. It is not the active CDN key in `.build.info`.

### NOT CONFIRMED / BLOCKED BY SCOPE

A recursive filename-only search under `Data` found no standalone file named `9f86d96c2ae820eb5164d6e12f95ebf6`. The manifest is therefore referenced by metadata but was not available as a plainly named file. The config references and sizes do not reveal manifest entries or locale-to-file associations.

Whether the manifest content is physically represented in local CASC storage is **NOT CONFIRMED**. Determining that would require resolving the content key against CASC indices and reading the referenced content; this phase requires stopping before that operation.

## 4. Locale Metadata Findings

### CONFIRMED

The `Tags` field in `.build.info` contains conditional locale tag alternatives including:

- `arSA speech`
- `arSA text`

The same expression also includes multiple other locale text/speech tags. This confirms that Arabic text and speech are named in the active installation metadata’s tag expression.

### NOT CONFIRMED

- That either Arabic tag is selected for the current installed payload.
- That the install manifest contains entries gated by either Arabic tag.
- That the local CASC storage contains corresponding Arabic text/audio payloads.
- That the presence of both tags means both contain complete localization.

The tag strings establish locale metadata, not installed content.

## 5. Arabic Text Resource Evidence

**NOT CONFIRMED.** No individual manifest entry, internal archive path, text-table name, string database, or Arabic text content was identified. The manifest was not read and CASC payloads were not inspected. The current evidence is limited to the `arSA text` tag in `.build.info`.

## 6. Arabic Speech Resource Evidence

**NOT CONFIRMED.** No individual manifest entry, audio path, voice resource, or audio payload associated with `arSA` was identified. The current evidence is limited to the `arSA speech` tag in `.build.info`.

## 7. CASC Index Findings

### CONFIRMED — names and sizes only

- `Data\indices` contains files named for the active CDN Config’s archive, archive-group, and file-index keys with sizes matching the corresponding `*-size` values in the text config.
- `Data\data` contains many `.idx` files (each 65,536 bytes in the observed listing), large `data.NNN` shards, and a `shmem` file.

### NOT INVESTIGATED

The contents of CDN index files and `Data\data\*.idx` files were not read or parsed. Names/sizes alone do not provide locale-specific file paths, tag associations, or evidence that any entry contains Arabic. No archive payload bytes were read.

## 8. Safety and Scope Compliance

- Inspected `.build.info`, the active Build Config, both local CDN Config text files, metadata filenames, and file sizes.
- Calculated SHA-256 hashes for `.build.info`, active Build Config, and the two CDN Config files.
- Searched filenames only for the active install key; did not search CASC payload contents.
- Did not inspect index binary contents or archive shards.
- Did not launch the game, use a parser, access process memory, use the registry, use the network, or modify the game/project files during inspection.
- Report written outside the game installation directory.

## 9. Remaining Unknowns

- Whether the install manifest is physically present in the local CASC content store and can be resolved using existing local metadata.
- What entries and locale conditions the manifest contains.
- Whether any `arSA`-tagged files are installed.
- Which text/audio resource paths, if any, belong to Arabic.
- Whether Arabic text coverage is complete and whether Arabic audio is present.

## 10. Recommended Phase 12

Before any extraction, define a narrowly scoped read-only operation to determine whether the existing install manifest can be resolved from local storage. It must use an independently vetted, build-compatible parser, work offline, avoid payload export, and be configured to read only the required manifest metadata. If manifest resolution necessarily decodes CASC content, explicitly authorize that limited metadata read in a new phase first. Then inspect only the manifest’s install-tag and file-entry metadata for `arSA text` and `arSA speech`; do not infer Arabic from hashes, tags, or index records alone. Any later resource inventory must remain a separate approved step.

## 11. Final Verdict

**`MANIFEST NOT LOCALLY ACCESSIBLE`**

The active installation metadata references the manifest by CASC content key, but no standalone file with that key was found. Its entries were not inspected because accessing them would cross the phase’s explicit no-CASC-content-read boundary. Accordingly, this phase confirms Arabic locale tags only; it does not confirm Arabic text or speech resources in the installed build.
