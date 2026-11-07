# Phase 1 — Hollow Knight: Godmaster package and game audit

## VERIFIED — selected installation

- Selected game path: `C:\Program Files (x86)\Hollow Knight Godmaster`.
- Game executable: `hollow_knight.exe`, 639,488 bytes, FileVersion/ProductVersion `2017.4.6.12734256`, SHA-256 `779EFDC34F408F6DA456FD21A11DAB6F4A73B4288EC87978A52B9A453E1CB85F`.
- `UnityPlayer.dll` is 17,123,264 bytes, version `2017.4.6.12734256`, SHA-256 `E7CFD2B185F849D1C4F8172C46F2A509F9AA0262BFC494CB16F1AE162C3930A9`.
- Unity data markers exist: `hollow_knight_Data\globalgamemanagers` (1,488,020 bytes) and `hollow_knight_Data\Managed\Assembly-CSharp.dll` (3,407,360 bytes).
- The local game executable identity is now pinned in the package manifest. The launcher can verify this selected build precisely.

## VERIFIED — supplied Arabic package

- The supplied folder `C:\Users\mouad\Desktop\arablocn_hk_v2` contains Doorstop (`winhttp.dll`, `.doorstop_version`, `doorstop_config.ini`), BepInEx core DLLs, and SnakeArabic plugin files (`SnakeArabic.Core.dll`, `SnakeArabic.HollowKnight.dll`, `SnakeArabic.pak`, x64 and x86 native libraries).
- Its `BepInEx\LogOutput.log` identifies Hollow Knight, SnakeArabic 1.0.35, 4,091 pack texts, and reports Unity `6000.0.61.7643309` for that session.
- Twenty files are bundled in the launcher. Runtime logs, cache and BepInEx user configuration were excluded. Their sizes and SHA-256 digests are recorded in `godmaster-package-manifest.json`.

## NOT VERIFIED — material compatibility mismatch

- The selected game reports Unity version `2017.4.6.12734256`, while the supplied package's local runtime log reports Unity `6000.0.61.7643309`. Those environments differ substantially. The log may be stale or from another runtime, but it does not provide evidence that these binaries support the selected game build.
- Package publisher identity, source chain, license, redistributability, and compatibility with this exact game build remain unverified.
- Arabic text/rendering behavior in this exact build has not been verified.

## External reference check

- Team Cherry's Steam announcement describes Godmaster as Hollow Knight's fourth free content pack, not a separate executable. The same official announcement feed lists patch 1.5.12620 on March 26, 2026; it does not state that the locally selected executable is that build.
- The Arabic package page describes its target as Hollow Knight and gives manual game-folder installation steps, but it does not identify the exact game build or publish hashes matching these local files.
- BepInEx documentation gives broad Unity support but also separates Mono and IL2CPP setup and notes Unity-version-specific behavior. That general support does not verify this particular bundled BepInEx/SnakeArabic combination against the selected executable.

## BLOCKED — installation remains disabled

The launcher now detects and verifies the user's selected game executable and UnityPlayer version. It separately blocks installation because package compatibility is unverified and the local log reports a different Unity runtime. The `compatibilityVerified` flag remains false. Do not change it to true without reliable build-specific compatibility evidence. Existing destination files are also conflicts and will not be overwritten.

## Safety and packaging

The selected game folder was inspected read-only. No game files, registry settings, permissions or configurations were changed. The game and launcher were not run. The .NET 10.0.401 `dotnet publish` build succeeds and the bundled package SHA-256 checks pass. Inno Setup Compiler is not available in the checked environment, so no Setup.exe was produced. Install/restore transactions and backups remain outside the game directory.

References: [Team Cherry/Steam announcements](https://steamcommunity.com/app/367520/announcements/), [Arablocn Hollow Knight translation page](https://arablocn.com/hollow-knight/), [BepInEx installation documentation](https://github.com/BepInEx/bepinex-docs/blob/master/articles/user_guide/installation/index.md), [BepInEx troubleshooting documentation](https://docs.bepinex.dev/master/articles/user_guide/troubleshooting.html).
