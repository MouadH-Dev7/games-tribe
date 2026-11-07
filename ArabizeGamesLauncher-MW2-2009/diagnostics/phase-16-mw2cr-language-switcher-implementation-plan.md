# Phase 16 — MW2CR Arabic Language Switcher Implementation Plan

## Status and scope

This is a planning deliverable only. No Registry values were queried or changed, no game files were changed, and no game or launcher was started. No language-switching code was implemented.

The target is Campaign Remastered PC (`lazr`, version `1.1.2.1279292`, build `CRC_1279292A_retail_signed_ship`), executable `MW2CR.exe`, at `C:\Games\Call of Duty - Modern Warfare 2 CR`.

## Executive summary

The existing launcher is a Windows WPF application on .NET 10 with a bilingual Arabic/English interface, an MVVM-style `MainViewModel`, a dashboard/navigation layout, and reusable read-only game-detection services. Reuse this application; do not replace its framework.

The application does not currently implement game-language switching or game launching. Its registry-related evidence is incomplete for a safe implementation:

- Exact key path is recorded as `HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR`.
- Value names recorded are `LOCALE` and `LOCALE_AUDIO`.
- The known Arabic values are `arSA` and `arSA`.
- The **Registry value types are UNKNOWN** from the saved reports. The referenced `_Language Switcher\Arabic.reg` is not present in the project or the searched local Desktop/Downloads file listings, and prior reports do not preserve its literal value-type syntax.
- **English values are UNKNOWN.** Build metadata's `enUS text` / `enUS speech` locale tags are not evidence of the exact previous English Registry settings. Do not substitute `enUS`, `enGB`, or any other locale by guess.

Therefore the launcher UI, navigation, exact-build detection improvements, read-only inspection, and backup schema can be designed now. Enabling Apply Language or Restore Previous Settings to write the Registry must wait until the value kinds and the exact English pair are supported by reliable local evidence and are presented for user confirmation.

## Existing application and reusable code

| Existing component | Reusable behavior | Gap for Phase 16 |
|---|---|---|
| `src/ArabizeGamesLauncher/ArabizeGamesLauncher.csproj` | WPF `WinExe`, `net10.0-windows`, nullable enabled, no third-party UI dependency. | No framework change needed. |
| `src/ArabizeGamesLauncher/MainWindow.xaml` | Existing dashboard shell, navigation, status cards, responsive layout, and launcher UI controls. | Add a distinct Game Language page/card and clearly separate it from the launcher's own UI-language selector. No game-language apply/restore implementation exists. |
| `src/ArabizeGamesLauncher/ViewModels/TextCatalog.cs` | Existing Arabic and English launcher strings and right-to-left flow support. | Add strings for game language, selected/current/unknown state, detection, consent, backup, partial failure, and restore messages in both UI languages. |
| `src/ArabizeGamesLauncher/ViewModels/MainViewModel.cs` | Existing navigation, settings, status, diagnostics, and commands. `SetArabicCommand` / `SetEnglishCommand` change only the launcher's UI language. | No Registry access or game language state exists. `using Microsoft.Win32` is used for `OpenFolderDialog`; no `RegistryKey` operation was found. `CanLaunch` is currently false. |
| `src/ArabizeGamesLauncher/Services/GameDetectionService.cs` | Checks `MW2CR.exe`, executable ProductName, `.build.info`, `Data\data`, local game launcher presence, product `lazr`, and computes the executable SHA-256 without launching the game. | Current success gate accepts any non-empty build version for `lazr`; it does not require exact version `1.1.2.1279292` or build name `CRC_1279292A_retail_signed_ship`. `ReadBuildProduct` scans configs and returns the first matching line rather than selecting the config by the active Build Key. Tighten before allowing a language write or launch. |
| `src/ArabizeGamesLauncher/Services/AppPaths.cs` and `SettingsStore.cs` | User-scoped launcher data root, config/settings, logs, reports, backups, and transactions directories. | Add a dedicated, versioned language-state backup location/record; do not place backups in the game directory. |
| `src/ArabizeGamesLauncher/Services/BackupManager.cs` | Safe relative-path and SHA-256 verified **file** backup/restore planning. | It does not back up Registry values, Registry value kinds, or missing-value state. Do not reuse it as if it were a Registry backup service. Add a separate, narrowly scoped registry-state component only after evidence/approval. |
| `diagnostics/` and `StructuredLogger.cs` | Existing diagnostics/report listing and activity logging. | Report only the two approved locale values and relevant error details. Never enumerate or log unrelated Registry values. |

Current README (`README.md:28–36`) describes a non-destructive foundation: install/restore disabled, launch disabled, and direct `MW2CR.exe` launch not yet enabled. This phase changes that future direction only through a plan; it does not enable those actions.

## Registry evidence and unresolved values

### Exact known Registry target

| Item | Available local evidence | Status |
|---|---|---|
| Hive and subkey | `HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR` in `diagnostics/mw2-campaign-remastered-diagnostic-report.json` and `diagnostics/mw2-campaign-remastered-localization-feasibility.md`. | **KNOWN from prior plain-text report** |
| Value name | `LOCALE` | **KNOWN** |
| Value name | `LOCALE_AUDIO` | **KNOWN** |
| Arabic value for `LOCALE` | `arSA` | **KNOWN from the prior `Arabic.reg` text inspection and user's successful Arabic UI/subtitle test** |
| Arabic value for `LOCALE_AUDIO` | `arSA` | **KNOWN as the prior configured value; effect on voice language is not verified** |
| Registry value kinds | Not recorded in the saved evidence; the original `.reg` text is not currently available in the project or the searched Desktop/Downloads paths. | **UNKNOWN** |
| Exact English values for either name | No English `.reg` contents or verified pre-import Registry snapshot was found in the reviewed reports. `enUS` metadata alternatives do not establish user settings. | **UNKNOWN** |
| Current live Registry state | No live key/value query was performed for this plan. The older diagnostic recorded the key absent before the user later imported `Arabic.reg`; it is stale for current state. | **UNKNOWN** |
| Official status of the `.reg` mechanism | Phase 10 describes it as an undocumented third-party workaround; official Battle.net selection behavior remains unverified. | **NOT VERIFIED as official** |

Do not infer `REG_SZ` from a summary that only says `LOCALE=arSA`; obtain the original text or another reliable, explicit type record. Do not infer English as `enUS` solely because the build metadata includes `enUS` tags. Do not treat the two values as independent controls: the user's test used `arSA` for both but voices remained English.

## Proposed implementation design

### 1. Read-only language inspection

After exact game compatibility is enforced, a read-only Registry adapter may query only the named `LOCALE` and `LOCALE_AUDIO` values under the exact LAZR subkey. It should report each value as one of: known string value, missing, unsupported type, or access error. It must not enumerate other values or unrelated keys. Current value display must not assume it reflects the actual in-game language until tested.

No live Registry query is part of this plan. The first code milestone may present current locale as **Unknown / not inspected** until the user authorizes a read-only inspection.

### 2. Language selection and user consent

The Game Language page should have distinct choices for Arabic and English and display the launcher's UI language separately. It should show both Registry value names and their individual desired values before apply. Text/audio should not be described as independent unless separate behavior is verified.

Before any write, show a consent dialog that names the exact HKCU subkey, the two values only, current state, target state, and the backup file to be created. Cancel must leave Registry untouched. Apply must be disabled when exact build, Registry types, target values, or backup verification is uncertain.

### 3. Backup and restore

Use a versioned JSON snapshot in the launcher's user-data backup directory, outside the game folder. Each value record should store:

- Schema version and unique transaction ID.
- UTC timestamp and game ID/build identity.
- Exact hive/subkey and the two approved value names only.
- Whether each value existed before the change.
- The exact pre-change value and exact `RegistryValueKind` when present.
- SHA-256 over a canonical representation of the immutable backup payload as a corruption check.

Create a new backup atomically with create-new semantics; never overwrite a valid backup. Re-read and verify the saved backup before allowing a Registry write. The change journal should separately record requested target values, per-value write/validation outcomes, and any partial failure. Restoration must put back the original type and value or delete a value that was originally missing. It must preserve every unrelated Registry value. Restore must verify the resulting state and report partial failure without claiming success.

Because the current `.reg` file/type evidence and English values are missing, the exact serialization for target values is not yet final. Do not implement Registry writes from this plan alone.

### 4. Game detection and compatibility gate

Reuse `GameDetectionService`, then strengthen it to require all recorded target identity fields before enabling Apply or Launch:

- Product `lazr`.
- Version `1.1.2.1279292`.
- Build name `CRC_1279292A_retail_signed_ship` from the active build configuration associated with the active `.build.info` Build Key.
- Expected product identity, `MW2CR.exe`, and required game data paths.

Unknown, missing, contradictory, or unsupported metadata must fail closed. The detection report should say which field failed. Do not use a generic `lazr` product match or non-empty version as exact-build compatibility.

### 5. Game launching

Once exact detection and selected-language verification are available, the requested Launch Game action can launch `MW2CR.exe` with its working directory set to the detected installation folder, without injection, hooks, or process memory access. Check for an existing `MW2CR` process before starting to avoid duplicates, and report process-start errors. Do not launch if the game is missing/incompatible or the selected locale configuration failed validation. The app currently disables launch; this code path is not implemented or tested.

### 6. UI and diagnostics

Add a dedicated Game Language page using the existing WPF navigation and `TextCatalog` pattern. Show:

- Game identity/build compatibility and install path.
- Current Registry state for only `LOCALE` and `LOCALE_AUDIO`, or an explicit Unknown/error state.
- Selected game language, separate from UI language.
- Apply Language and Restore Previous Settings controls with eligibility/disabled reasons.
- Last action, backup identifier/status, per-value validation, Registry access errors, and launch status.

Keep the existing language buttons for the launcher's interface. Translate new strings in Arabic and English. Avoid displaying “Arabic audio selected” as proof of Arabic audio content; the prior user test heard English voices.

## Milestones

1. **UI and navigation:** reuse the existing WPF shell; add Game Language/status page and bilingual strings. Controls remain non-writing and clearly show unresolved configuration as Unknown.
2. **Exact build gate:** make detection select the active build config from `.build.info`, check the exact version/build name, and present all mismatch reasons. No Registry writes.
3. **Read-only Registry inspection and backup schema:** only after the exact `Arabic.reg` type evidence and English values are available, add read-only inspection and a versioned backup component. No live read occurs without consent for that read if required by the user.
4. **User-consented switching:** implement only the two exact values after presenting the Registry change report and receiving explicit approval. Validate each write and partial failure path.
5. **Launch and restore:** add direct `MW2CR.exe` launch after exact checks, duplicate-process handling, and tested restore of values/missing state.
6. **Diagnostics and verification:** produce limited diagnostics and verify backup integrity/restore behavior with an isolated test Registry abstraction or temporary test hive; never use the user's live Registry for tests.
7. **Packaging:** only after the earlier milestones work and release paths/settings have been reviewed.

## Risks and unresolved details

1. **English values are unknown.** Switching to English is blocked until reliable local evidence identifies both intended values.
2. **Registry value types are unknown.** Do not write or coerce to a presumed type.
3. **Official status is unverified.** The known method is an undocumented third-party `.reg` workaround, even though the user reports Arabic UI/subtitle behavior after using it.
4. **Audio result is ambiguous.** `LOCALE_AUDIO=arSA` was recorded, but voices remained English. Do not promise Arabic voices or claim independent text/audio selection.
5. **Old current-state finding is stale.** A prior report said the key was absent before the later user import. The present state has not been read.
6. **Exact build enforcement is incomplete in current code.** Product `lazr` and a non-empty version are not enough for compatibility.
7. **Launch flow was previously disabled.** The user now requests direct `MW2CR.exe` launch, but this plan does not start the game or prove a normal platform/authentication flow.
8. **Registry write failures can be partial.** Each value must be written and read back independently; an error must trigger truthful partial-state reporting and a verified restore offer.

## First implementation milestone

Deliver a non-writing Game Language page in the existing WPF application with Arabic/English launcher UI strings, separate game-language selection state, game/build compatibility display, and disabled Apply/Restore/Launch controls with clear reasons until later gates are met. Reuse the existing navigation, theme, `MainViewModel`, `TextCatalog`, and `GameDetectionService` rather than introducing another framework. This milestone must not read or change the Registry and must not launch the game.

## Operations requiring explicit approval

- Any write to `LOCALE` or `LOCALE_AUDIO`, including applying Arabic or English.
- Any restore operation that writes a previous value or deletes a value that was originally absent.
- Any Registry read if the user requires that even diagnostic reads be consent-gated; this plan performs none.
- The first live Apply/Restore operation must be preceded by a review of the exact target values, types, backup behavior, and consent dialog, followed by explicit user confirmation.

No other Registry key/value may be changed by this launcher. No Registry operation was performed while preparing this plan.

## Evidence reviewed

- `src/ArabizeGamesLauncher/ArabizeGamesLauncher.csproj`, `MainWindow.xaml`, `ViewModels/MainViewModel.cs`, `ViewModels/TextCatalog.cs`.
- `src/ArabizeGamesLauncher/Services/GameDetectionService.cs`, `BackupManager.cs`, `AppPaths.cs`, `SettingsStore.cs`, and `Models/LauncherSettings.cs`.
- `README.md` current-behavior section.
- `diagnostics/phase-10-official-arabic-localization-and-local-build.md`.
- `diagnostics/mw2-campaign-remastered-diagnostic-report.json`.
- `diagnostics/mw2-campaign-remastered-localization-feasibility.md`.
- `diagnostics/phase-15-official-arabic-localization-resource-verification.md`.
- Local project, Desktop, and Downloads file listings for the original `Arabic.reg`; the original file was not present in those searched locations. No live Registry data was read.

**Planning-stage confirmation:** this report is the only Phase 16 file created. No Registry modification, game-file modification, dependency installation, build, or game launch occurred.
