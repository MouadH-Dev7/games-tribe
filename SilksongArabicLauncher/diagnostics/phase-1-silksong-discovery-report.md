# Phase 1 — Hollow Knight: Silksong Arabic Localization Discovery

## Scope and result

This was a read-only discovery and compatibility audit. No launcher installer was implemented. No supplied executable or DLL was run or loaded, no game was launched, and no game, translation-package, Registry, or MW2 launcher file was modified.

The evidence identifies a strong local Silksong installation candidate and a supplied Arabic package that is designed to use BepInEx/Doorstop, Patchwork text files, and an English-named Unity font asset bundle. The package README describes copying its files into the game folder and selecting English in-game. The exact game release/build and the authenticity/safety of the supplied DLLs remain unverified, so installation should not yet be automated.

## A. Environment and project separation

- Existing UI reference project: `C:\Users\mouad\Desktop\Call of Duty - Modern Warfare 2 CR trib\ArabizeGamesLauncher-MW2-2009\src\ArabizeGamesLauncher\ArabizeGamesLauncher.csproj`. It is a WPF C# project targeting `net10.0-windows`; the installed .NET SDK reports `10.0.401`.
- A separate project directory was created at `C:\Users\mouad\Desktop\Call of Duty - Modern Warfare 2 CR trib\SilksongArabicLauncher`. It currently contains discovery diagnostics only; no installation code was started.
- No MW2CR launcher source or configuration was changed.

## B. Game discovery

### Candidate found

`C:\Games\Hollow Knight - Silksong`

Evidence observed in that directory:

- `Hollow Knight Silksong.exe` — 672,256 bytes; SHA-256 `72FAA5AC3F3D8CD781B962EE1D842F435300A270CE8AC76D9F6DC9283002E074`.
- Executable file version: `6000.0.50.3512832`, which is the Unity player/engine build version. `UnityPlayer.dll` has the same version and is 33,677,168 bytes.
- Matching game-data directory: `Hollow Knight Silksong_Data`, including `globalgamemanagers` (303,060 bytes), `Managed\Assembly-CSharp.dll` (6,944,256 bytes), and `boot.config`.
- `MonoBleedingEdge` and a Unity `Plugins\x86_64` directory are also present. The game data includes Steamworks components, but those alone do not prove the installation source or ownership.
- Marker hashes: `globalgamemanagers` SHA-256 `3CE206B562680A41DE8271D2546C2462414CC7A3FAD3EED7139BC32328B45BBD`; `Assembly-CSharp.dll` SHA-256 `C1A97ACD7F38A84450CFB43C09AB128325B0CAADC266EC407E52A56A9439B40C`.

### Identity limits and search coverage

The executable name, paired `_Data` directory, managed game assembly, and Unity runtime are strong evidence that this is a Silksong installation. The executable metadata does **not** expose a Silksong game release number; `6000.0.50.3512832` is the Unity version, not the game's content version. No `appmanifest_1030300.acf` was found in the checked common Steam locations, and the default Steam install folders checked were absent. Therefore Steam App ID `1030300`, exact Steam build, installation ownership, and package-to-build compatibility are **not confirmed**.

The search was limited to common Steam locations, direct folders under `C:\Games`, and likely top-level user Desktop/Downloads locations. No full-disk scan was performed. No other Silksong-looking game installation candidate was found in those locations. Users should be able to add another folder manually; the future UI should list all validated candidates rather than choose by folder name.

## C. Translation package inventory

Source directory: `C:\Users\mouad\Desktop\New folder (3)`

- 65 files, 26,266,619 bytes total.
- 36 `.yml`, 21 `.dll`, 2 `.txt`, 1 `.ttf`, 1 `.ini`, 1 `.cfg`, 1 `.assetbundle`, 1 `.bundle`, and 1 `.doorstop_version`.
- The 36 YAML files are under `BepInEx\plugins\Ashiepaws-Patchwork\Text\...` and are named `EN.yml`. Static text inspection found Arabic characters in 35 of the 36 files. For example, `Text\MainMenu\EN.yml` contains Arabic menu labels and interface strings. `Deprecated\EN.yml` had no Arabic code points. This supports that the package replaces/overrides the English text set; it does not prove every string is translated or displayed correctly in the game.
- Repeated basename `EN.yml` occurs in separate subdirectories; there are no duplicate relative file paths in the package.
- `BepInEx\plugins\HeshamRTL\Arabic.ttf` is 180,460 bytes, SHA-256 `DC09ECFDA83A30F5CE055A765FEFB5CE2802311AB202AB2B0372664346FECE97`. Its adjacent `OFL.txt` identifies the font as Noto Arabic under SIL Open Font License 1.1. Font presence alone does not prove the runtime loads it or that every required glyph/rendering case works.
- `BepInEx\config\com.hesham.rtl.runtime.cfg` is version-labelled “HeshamRTL Runtime 1.0.0” and has `Verbose = false`.
- `.doorstop_version` contains `4.4.1`; `doorstop_config.ini` sets `enabled=true` and targets `BepInEx\core\BepInEx.Preloader.dll`.
- Static file metadata reports BepInEx preloader `5.4.22.0`, Patchwork `2.5.0.0` (product version `2.5.0+a6a806ede1354aafdd5f4c19319739085d2d89bc`), and `HeshamRTL.Runtime` `1.0.0.0`. These are file metadata, not independently authenticated upstream provenance.
- `BepInEx\patchers\silksong_modding-SilksongPrepatcher\SilksongPrepatcher.dll` is evidence that the package is intended for Silksong's modding setup, but its code was not inspected or executed.

A complete SHA-256 inventory of all 65 package files is in [translation-package-sha256.csv](C:/Users/mouad/Desktop/Call%20of%20Duty%20-%20Modern%20Warfare%202%20CR%20trib/SilksongArabicLauncher/diagnostics/translation-package-sha256.csv). Notable hashes: `doorstop_config.ini` `2255E7640434FDFCCBFEB123A5F4FCCB05032481B39C2BA822E905CCBA58D20E`; `winhttp.dll` `CF9DD372CA0DDBE01153502C49F8F756197BB260001792FE766F6C0242DC7FC0`; `BepInEx\core\BepInEx.Preloader.dll` `D247B6A3B9A04F0D96105CDA2961864A404AEDB9193C59663AAC5FA153D6FFBD`; `Patchwork.dll` `F93F25CDBF32601977734DC232091D13214BF6C980CDE7B10343B3F0362B755F`; `HeshamRTL.Runtime.dll` `B238594B8D6F3D977E1ED9A55A6E19F07F19D874A726B84E8D26B43D2E444584`.

### Instructions and archive inspection

`اقراني.txt` identifies the package as “Hollow Knight: Silksong — تعريب هشام”. It says to remove any old translation first, copy the directory contents except the readme into the game directory, accept merge/replace, then select English in the game because the translation replaces English. Its removal guidance is to delete copied files or use Steam's verify-integrity feature. It gives no exact game-version requirement and no transaction-safe uninstall manifest.

The two bundle files were not extracted. Their first bytes contain the `UnityFS` signature and a Unity `6000.0.50f1` version string. The package's `patchwork.assetbundle` is 7,094 bytes. The package's `Hollow Knight Silksong_Data\StreamingAssets\aa\StandaloneWindows64\fonts_assets_english.bundle` is 23,349,233 bytes, SHA-256 `52F2D2BBF6D5EA90976718021EAE5985F846C5010B145BB286E062F7574BAE2E`.

Relative-path comparison against the candidate installation found exactly one existing destination path: `Hollow Knight Silksong_Data\StreamingAssets\aa\StandaloneWindows64\fonts_assets_english.bundle`. The installed file is 8,517,424 bytes with SHA-256 `125993E4121D83F5D7F72F3193E820375A1FE86BF63B6061BC48B479675BBC9A`, so it would be replaced by a different package file. The other 63 payload paths (excluding the readme) were absent from the candidate installation at inspection time. No files were copied or changed.

## D. Mechanism and compatibility assessment

### Confirmed from package evidence

1. This is not a simple translation-only file set. It includes Doorstop configuration, a `winhttp.dll` proxy, BepInEx core/preloader, patchers, and plugin DLLs.
2. The Doorstop configuration is enabled and names the BepInEx preloader. If installed and then the game is started, the configured loader path is intended to load mod code in the game process.
3. Patchwork's directory contains Arabic YAML text under an `EN` locale name. The supplied instructions explicitly say to choose English.
4. The package includes an Arabic TTF and an English-named Unity font asset bundle replacement. The replacement targets an existing game path and has a different hash/size.
5. Package instructions describe manual copying and broad deletion/Steam verification for removal. There is no supplied installer, per-file installation manifest, backup, or selective rollback mechanism.

### Not established

- The source/provenance and code behavior of the supplied DLLs were not independently verified. BepInEx/Harmony-related names suggest runtime patching, but exact hooks, network behavior, and side effects remain unknown without a separate static binary/source audit.
- No target Silksong game version is stated in the readme, and the candidate's exact game content version is not available from the examined executable metadata. The matching Unity generation is useful evidence, not proof of exact compatibility.
- The UnityFS header proves bundle container identification, not the bundle's contents or correctness. No bundle contents, font asset tables, or plugin behavior were inspected.
- Arabic shaping, right-to-left layout, mixed Arabic/Latin handling, font fallback, controller glyphs, and actual in-game rendering remain unverified.
- The language is text/UI oriented based on the YAML and font files. No distinct Arabic voice/audio assets were identified in the package inventory; this is not proof that the game/package has no other audio localization.
- The package has no exact version pin, publisher signature evidence, upstream download provenance, or installer integrity manifest. Locally calculated SHA-256 values provide a baseline for these exact files only; they do not authenticate the package.

## E. Future launcher architecture proposal

Keep the application Silksong-specific and in the separate `SilksongArabicLauncher` project.

1. **Discovery service:** check known Steam library configuration/manifests and reasonable common locations; support user-selected paths; avoid whole-disk scans; return every candidate with its evidence.
2. **Installation validator:** require `Hollow Knight Silksong.exe`, matching `Hollow Knight Silksong_Data`, `globalgamemanagers`, and managed assembly markers. Add Steam App ID/build checks when authoritative local metadata is available. Do not accept a folder name alone.
3. **Package inspector:** read a package manifest and all file hashes; validate its declared game target, paths, versions, dependencies, and expected existing-file hashes. Never load supplied DLLs.
4. **Compatibility gate:** compare a verified game build with package-supported builds, loader/runtime versions, required English game language, and the existing bundle hash. Until these are known, keep install disabled.
5. **Package manifest:** enumerate the 64 files the supplied readme intends to copy (excluding `اقراني.txt`), including relative destination, source hash, expected role, and whether the path is new or replacing the English font bundle. Use the hash inventory created in this phase as input evidence, not as proof of authenticity.
6. **Backup manager and transaction:** before any future replacement, back up the original font bundle and record every pre-existing/absent destination. Stage files outside the game folder, verify hashes, apply as one transaction, and roll back changes on failure. Preserve the source translation folder.
7. **Restore service:** restore the exact original bundle and remove only files recorded as newly installed by a successful transaction; verify the restored hash and never delete unrelated BepInEx files.
8. **Launch service:** after fresh game validation, start the discovered absolute executable with its game folder as working directory, without arguments or elevation. Launch only after an explicit UI action.
9. **Arabic/English UI:** WPF is supported by the local .NET 10 environment and existing project reference. Provide translated statuses, show detected path/build and package compatibility clearly, and keep discovery, inspection, install, restore, and launch states separate.

## Risks, unresolved questions, and recommended next phase

The largest blockers are exact game-build identification and trust/compatibility evidence for the supplied mod-loader/plugin DLLs. The package instructions are not sufficient authorization or technical detail for an automated installer that can overwrite a 23 MB asset bundle and activate runtime-loaded code.

Recommended next phase: a read-only package provenance and exact-build compatibility audit. Establish the game source/build using the local launcher/library metadata, inspect only safe static metadata/source available for Doorstop, BepInEx, Patchwork, HeshamRTL, and SilksongPrepatcher, and document expected package paths and supported versions. Keep install and restore disabled until those checks and a user-approved, reversible transaction design are complete.

## Actions not performed

- No game or package file was modified, renamed, removed, installed, copied, or extracted.
- No supplied executable, DLL, mod loader, or game was run or loaded.
- No Registry value was read or changed.
- No network access, download, build, or dependency installation was performed.
- The existing MW2CR launcher project was not modified.
- The package source and game candidate were inspected read-only. Hashing and metadata reads were performed; the game installation itself was not launched.
