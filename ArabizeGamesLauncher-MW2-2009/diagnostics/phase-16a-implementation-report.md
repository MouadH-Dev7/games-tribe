# Phase 16A — Game Language UI and Exact Build Gate

## Implemented

- Added a dedicated Game Language page to the existing WPF application.
- Kept the existing Arabic/English interface selector independent from the game-language selection. The latter is local UI state only and is not written to settings or the Registry.
- Added bilingual labels and explanatory text for game identity, exact build compatibility, version, build name, installation path, selected game language, current language (`Unknown — not inspected`), Registry state (`Not inspected`), and the fact that Arabic audio is unverified. Arabic interface mode continues to use right-to-left window flow.
- Added Apply Language, Restore Previous Settings, and Launch Game controls. Each remains disabled through a command whose `CanExecute` returns false, and each has a visible reason. No switching, restore, or game-launch implementation was added.
- Strengthened game detection to require `MW2CR.exe`, `.build.info`, `Data\data`, `main\fileSysCheck.cfg`, and `MW2 Campaign Remastered Launcher.exe`.
- Detection now requires the executable product metadata to identify Campaign Remastered, exactly one active `.build.info` row, a well-formed active Build Key, and the exact expected build version. It derives the config path from that active key (`Data\config\<first 2 key chars>\<next 2 key chars>\<full key>`), then reads only that config and requires exactly one `build-product` and one `build-name` field matching `lazr` and `CRC_1279292A_retail_signed_ship`. Missing, duplicate, malformed, contradictory, or unsupported metadata fails closed with a specific reason.
- Diagnostic reports now include the selected Build Key and build name as well as existing identity/version fields.

## Files changed

- `src/ArabizeGamesLauncher/Services/GameDetectionService.cs`
- `src/ArabizeGamesLauncher/Models/GameDetectionResult.cs`
- `src/ArabizeGamesLauncher/ViewModels/MainViewModel.cs`
- `src/ArabizeGamesLauncher/ViewModels/TextCatalog.cs`
- `src/ArabizeGamesLauncher/MainWindow.xaml`
- `tests/ArabizeGamesLauncher.Tests/Program.cs`
- `diagnostics/phase-16a-original-locale-backup-evidence.md`
- `diagnostics/phase-16a-implementation-report.md`

## Verification

The tests use temporary synthetic installation fixtures only. No user's game directory or Registry was used by these tests.

Test command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --no-restore --project tests\ArabizeGamesLauncher.Tests\ArabizeGamesLauncher.Tests.csproj
```

Result: **21/21 passed.** Coverage includes exact build accepted; wrong version, build name, and product rejected; missing executable/data rejected; config association through the active Build Key; missing config and multiple active rows rejected; Arabic/English text; game/UI language separation; and all three action commands disabled.

Build command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build --no-restore src\ArabizeGamesLauncher\ArabizeGamesLauncher.csproj
```

Result: **Build succeeded, 0 warnings, 0 errors.** The first attempt to invoke `dotnet` used its default CLI home and was denied access to `C:\Users\mouad\.dotnet`; the successful commands redirected CLI state to the existing project-local `.dotnet-cli` directory. No dependency restore or installation was performed.

## Locale backup evidence

The requested `%USERPROFILE%\Desktop\lazr_locale_backup.reg` file existed. In its target LAZR key section, `LOCALE` and `LOCALE_AUDIO` were both present with literal data `enUS`. The syntax represents both as `REG_SZ`. See `phase-16a-original-locale-backup-evidence.md`. This is backup-snapshot evidence only; live Registry state was not inspected.

## Remaining blockers and limits

- The user's actual installation was not inspected in this phase, so this implementation report makes no claim that live detection accepts that installation. The exact-build gate was validated with synthetic fixtures.
- The current game language and current Registry configuration remain explicitly uninspected by the app.
- Arabic audio support and completeness of Arabic game content remain unverified.
- Apply, Restore, and Launch remain intentionally unavailable. The selected game language does not change installed files or system configuration.

## Safety confirmation

No live Registry access or modification occurred. No game installation file or setting was read or modified. No `.reg` import or execution occurred. Neither the game nor the launcher was launched. No dependencies were downloaded or installed.
