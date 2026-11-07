# CASC archive inventory — Phase 3

**Target:** `mw2-campaign-remastered-pc`  
**Installation:** `C:\Games\Call of Duty - Modern Warfare 2 CR`  
**Build:** `lazr`, `1.1.2.1279292`, `CRC_1279292A_retail_signed_ship`

## Result

**BLOCKED for internal archive inventory.** Parser compatibility could not be established, so the archives were not opened, copied, or extracted. There are **no internal archive paths or resource entries verified by Phase 3**. The presence or absence of Arabic resources in packed data is therefore **NOT VERIFIED**.

## Existing on-disk structural inventory

The following is a read-only filesystem inventory recorded in the Phase 1/2 diagnostic, not an archive parser result:

- `Data`: 106 files, 69,551,773,736 bytes.
- Four short hash-named text configuration files under `Data\config`.
- 65 numbered data shards `Data\data\data.000` through `data.064`.
- 32 `.idx` files and one `shmem` in the data directory.
- Four hash-named `.index` files under `Data\indices`.
- Config text refers to archive groups/indexes; shard and index payloads were not decoded.

These structures are **INFERRED consistent with CASC**, not proof of successful parsing or a confirmed mapping between archive entries and this game's resources. See `mw2-campaign-remastered-archive-investigation.md` for prior discovery notes.

## Hashes and coverage

The existing `mw2-campaign-remastered-archive-hash-baseline.json` contains SHA-256 for 42 files: small index/config files plus `data.000`. It does not hash all 65 shards; the other 64 large shards are outside that baseline. In particular, this phase did not claim a complete archive integrity snapshot. The game executable hash was re-read and matched the prior diagnostic (`B0B9909199051ECF1E24A56F6A6F93F47364549F32164F7698DB789D271D8C57`). No post-parse hashes exist because no parse/copy took place.

## Required next evidence

No resource rows are fabricated. Once exact-build parser support and non-writing behavior are established, a limited locale/resource inventory must record each candidate's internal path, filename, detected type, locale evidence, and SHA-256 of the extracted/copied resource. Language-like names alone are insufficient to classify content as Arabic.

**Safety:** no archive extraction, game-file writes, registry operation, configuration edit, permission change, or game launch occurred in this phase.
