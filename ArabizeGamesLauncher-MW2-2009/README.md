# Arabize Games Launcher – MW2 Campaign Remastered

Windows WPF launcher foundation for **Call of Duty: Modern Warfare 2 Campaign Remastered – PC**.

- Game ID: `mw2-campaign-remastered-pc`
- Default game folder: `C:\Games\Call of Duty - Modern Warfare 2 CR`
- Target framework: .NET 10 for Windows, WPF
- No third-party NuGet packages are required.

## Build and run

Install the .NET 10 SDK and Windows Desktop Runtime, then run from this folder:

```powershell
dotnet restore ArabizeGamesLauncher.sln --configfile NuGet.Config
dotnet build ArabizeGamesLauncher.sln -c Release --no-restore
dotnet run --project src/ArabizeGamesLauncher/ArabizeGamesLauncher.csproj
```

`NuGet.Config` clears external package sources because the solution has no external package dependencies. For development, the Debug build stores its local test settings under the build output directory. A Release build stores configuration, reports, packages, backups, transactions, and logs under `%LOCALAPPDATA%\ArabizeGames\MW2CampaignRemasteredLauncher`. `ARABIZE_DATA_ROOT` can override that root for isolated development runs.

To run the temporary-file test suite:

```powershell
dotnet run --project tests/ArabizeGamesLauncher.Tests/ArabizeGamesLauncher.Tests.csproj -c Release
```

## Current behavior

- Read-only game validation checks `MW2CR.exe` product metadata, `.build.info`, the `lazr` build product, and required Campaign Remastered paths. It never starts the game to detect it.
- Package folders are scanned for `package.json` or `manifest.json`. The validator checks the game ID, Campaign Remastered edition, exact supported build, relative paths, forbidden executable/script destinations, source-file existence, and SHA-256 values.
- Backup creation and restoration planning are implemented as isolated services and exercised only with temporary files.
- Install and restore buttons are deliberately disabled. No real package was supplied, and no file destination or archive replacement method has been established.
- Game launch is disabled because this installation's normal platform launch flow has not been documented or verified. The launcher never starts `MW2CR.exe` directly.
- Arabic and English launcher strings are provided. This translates the launcher UI; it does not add Arabic to the game.

See `diagnostics/` for the Phase 1 identity/feasibility reports and the separate archive investigation.
