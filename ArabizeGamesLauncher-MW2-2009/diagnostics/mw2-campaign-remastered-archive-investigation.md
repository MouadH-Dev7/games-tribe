# MW2 Campaign Remastered archive investigation

**Game ID:** `mw2-campaign-remastered-pc`  
**Inspected directory:** `C:\Games\Call of Duty - Modern Warfare 2 CR`  
**Mode:** read-only; no archives were extracted, opened with third-party tools, or modified.

## Findings

The local `Data` tree contains the expected build/CDN configuration pattern: `.build.info`, four hash-named text configuration files under `Data\config`, hash-named `.index` files under `Data\indices`, and `Data\data` containing 65 numbered `data.000`–`data.064` shards, 32 `.idx` files, and `shmem`. The small text configurations label archive groups and file indexes. The shards and indexes are binary and have not been decoded.

This layout is **consistent with Blizzard CASC storage**, based on the local CDN/archive/index configuration and the numbered data plus index arrangement. This is a format-family inference from the directory evidence, not a confirmed parse of this specific build. The [CascLib project](https://github.com/ladislav-zezula/CascLib) describes itself as an open-source library for reading Blizzard CASC storages. That provides a known candidate for a later controlled inspection, but does not establish exact compatibility with this Campaign Remastered build or reveal any localization resource by itself.

## Tool availability and action taken

No CascLib, CascView/CASCExplorer, or QuickBMS command was available through the current command search, and none was found in the inspected game or standard Program Files locations. The installation includes a `QuickSFV.EXE` utility, but that is not a CASC asset browser and was not run. No tools were downloaded or installed. Since no known-compatible read-only parser was available locally, archive extraction was not attempted.

## Hash preservation

The companion [archive hash baseline](mw2-campaign-remastered-archive-hash-baseline.json) records SHA-256 hashes for the small `.idx`, `.index`, configuration, and `shmem` files under `Data`, plus the `data.000` shard hash captured in Phase 1. The other 64 large data shards were not fully hashed because no extraction or writes were performed; the baseline is intentionally labeled as partial. No archive content was modified, so no post-investigation restoration was necessary.

## Localization conclusion

Phase 1 build tags include `arSA text?` and `arSA speech?`, which show Arabic locale identifiers in build metadata. The archive inventory alone does not show whether Arabic text, fonts, or audio are present in the installed archive set. No archive filenames or file contents were mapped to Arabic, no font glyph coverage was inspected, and no Arabic shaping or right-to-left rendering was tested.

A later read-only extraction attempt should use a vetted parser that supports this exact `lazr`/`1.1.2.1279292` storage, operate on copies in a separate temporary directory, and map output names back to locale tags before drawing conclusions. Extracted content must remain inert and must not be launched. Until then, there is no verified destination path or safe replacement method, and launcher install/restore actions must remain disabled.

## Safety record

- No game file, registry entry, permission, or game configuration was modified.
- No `.reg` file was run.
- No executable or extracted content was launched.
- No archive was extracted or replaced.
- No unknown archive tools were downloaded.
