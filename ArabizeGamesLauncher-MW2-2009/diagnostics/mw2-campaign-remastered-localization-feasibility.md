# MW2 Campaign Remastered localization feasibility

**Inspection mode:** read-only  
**Target:** Call of Duty: Modern Warfare 2 Campaign Remastered for PC  
**Game ID:** `mw2-campaign-remastered-pc`  
**Inspected directory:** `C:\Games\Call of Duty - Modern Warfare 2 CR`

## Confirmed identity and build evidence

The directory contains `MW2CR.exe`, whose Windows product metadata identifies **Call of Duty: Modern Warfare 2 Campaign Remastered** (file version `1.0.0.0`, original filename `h2_sp64_ship.exe`, 14,803,528 bytes). `MW2 Campaign Remastered Launcher.exe` identifies itself as the Campaign Remastered launcher (version `1.16.7.3017`). The local `.build.info` identifies branch `eu`, product `lazr`, build `CRC_1279292A_retail_signed_ship`, and version `1.1.2.1279292`. SHA-256 values are recorded in the JSON diagnostic.

## Installed structure and relevant formats

Top-level content includes `Data`, `main`, `players2`, `_Language Switcher`, `_Redist`, the game executable, and a launcher executable. `Data` contains 106 files (69,551,773,736 bytes): four small hash-named text configuration files, 65 numbered `data.000`–`data.064` shards, 32 `.idx` files of 65,536 bytes each, four hash-named `.index` files, and a `shmem` file. The text configs refer to archive groups and file indexes. The shard and index contents are binary; this inspection did not parse or extract them. Their precise internal asset format and file-to-archive mapping therefore remain unconfirmed.

`main` contains a save folder and `fileSysCheck.cfg`; that config contains only a filesystem-check comment. `players2` contains profile-like directories and `config.cfg`/`keys.cfg` files. Their contents were not read because they are not needed to establish localization feasibility and may include user preferences or bindings.

No standalone Arabic text, font, or localization asset was identified by filename. This does not prove none are embedded in the packed data.

## Locale evidence

The installed `.build.info` Tags field includes the literal entries `arSA speech?` and `arSA text?` among locale tags. This confirms that the local build metadata names Arabic Saudi locale variants; it does **not** establish that the corresponding Arabic assets are installed, complete, or available to this exact build.

`_Language Switcher` contains 14 small `.reg` files. The Arabic one is UTF-16 text and sets `LOCALE` and `LOCALE_AUDIO` to `arSA` under `HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR`. All `.reg` files were read as plain text and none was executed or imported. These are locale-setting snippets, not translation packages. The corresponding user registry key was absent during inspection, so the currently selected locale could not be established.

The official Call of Duty Campaign Remastered product page is available in Arabic, but that is Arabic marketing content on the website; it is not evidence of Arabic text or RTL support inside the game client ([official Arabic product page](https://www.callofduty.com/ar/mw2campaignremastered)).

## Feasibility assessment

| Capability | Evidence and status |
|---|---|
| Arabic locale identifier | **Confirmed in build metadata:** `arSA speech?` and `arSA text?` tags are present. |
| Arabic locale selection mechanism | **Partially evidenced:** bundled plain-text settings use Battle.net `LOCALE`/`LOCALE_AUDIO` values. No setting was applied, and official in-client behavior was not verified. |
| Arabic text assets installed | **Unconfirmed:** no loose assets found; packed archives were not decoded. |
| Arabic font glyph coverage | **Unconfirmed:** no font was identified outside opaque archives. |
| Arabic shaping and bidirectional ordering | **Unconfirmed:** neither source nor runtime rendering was inspected. Locale tags alone do not prove shaping or RTL correctness. |
| Localized UI/game text | **Unconfirmed:** no Arabic strings or user-visible in-game text resources were extracted or tested. |
| Safe external file replacement method | **Not established:** exact asset paths, archive format, and replacement rules are unknown. Do not guess destinations or replace packed archives based on the MW2 (2009) release. |

A plausible route, if a real compatible package exists, is to use the game's own supported locale content and selection mechanism or a package explicitly documented for this exact Campaign Remastered build. The package would need to provide verifiable Arabic text and font assets, explain any archive requirements and locale selection, and be tested for Arabic shaping, mixed-direction strings, UI layout, and audio. The current evidence is insufficient to select a safe installation method. No registry edits are recommended or performed.

## Package and implementation gate

A real Arabic package is still needed. None was supplied or verified. A future manifest must identify `gameId: mw2-campaign-remastered-pc`, exact supported build constraints, documented relative destinations, and SHA-256 hashes. No manifest exists in the current project yet; the prior MW2 (2009) diagnostic artifact was removed from the project directory when the target was corrected.

Launcher work can safely proceed for non-destructive functions such as detection, read-only reporting, Arabic RTL launcher UI, local package selection, manifest validation, SHA-256 checking, and normal game launching. Installation and restoration must remain disabled until a compatible package and exact file method are confirmed and Arabic rendering has been validated. Any eventual installer must retain the existing backup, hash verification, rollback, and restoration safeguards.

## Inspection safety

No game files, configuration files, registry values, or permissions were modified. No registry file was run, and the game executable was not launched.
