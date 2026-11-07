# Phase 16D — Safe Registry Restoration and Verified Game Launch

## Result

Implemented the Phase 16D restoration and launch flows. Automated checks passed: **61/61 isolated tests**, and the WPF application built successfully with **0 warnings and 0 errors**.

No live Registry values were written or deleted, no game was launched, and the installed game directory was not accessed during this phase. Registry mutations and process startup were exercised only through fake adapters in the test suite.

## Restoration

- `RegistrySnapshotStore.ListValidSnapshots` lists only top-level JSON snapshots in the designated backup directory that pass integrity/schema checks and match the detected game identity. Invalid and incompatible files are excluded. The store rejects reparse points for the backup directory and candidate files.
- The UI shows the selected snapshot ID, creation time, saved locale values, Registry value types or absent state, and the current inspected configuration separately.
- Restore is enabled only when the exact supported game is detected, current Registry inspection succeeds, the LAZR key and expected value states are available, and a compatible verified snapshot is selected.
- On explicit Restore, the app revalidates the game and Registry state, reloads and verifies the selected snapshot, then displays a confirmation containing the current and saved values. Cancellation performs no writes.
- After confirmation, the app creates and verifies a recovery snapshot before changing either approved value. It restores only `LOCALE` and `LOCALE_AUDIO`, verifies both, and preserves both the selected and recovery snapshots.
- If restoration fails partway, the app attempts rollback from the recovery snapshot and reports whether rollback verified. If rollback cannot be verified, the recovery file path is presented for manual recovery.

## Launch

- Launch remains unavailable until fresh game detection confirms product `lazr`, version `1.1.2.1279292`, build name `CRC_1279292A_retail_signed_ship`, a valid build key, and the matching `MW2CR.exe` inside the detected installation directory.
- The process adapter starts the absolute executable with that directory as its working directory, no arguments, and no elevation request. It checks for an existing `MW2CR` process and prevents a duplicate launch.
- Launch occurs only after an explicit UI command. Errors and already-running status are surfaced in the UI; the launcher does not close itself.

## Test coverage and verification

The test suite uses fake Registry and process adapters with temporary files. It covers compatible snapshot listing, both restore directions, cancellation, recovery-snapshot ordering and verification, partial failure and rollback, rollback failure reporting, unsupported/missing build data, duplicate process detection, process startup errors, UI command gating, and bilingual status text.

Commands run from the project directory:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --no-restore --project tests\ArabizeGamesLauncher.Tests\ArabizeGamesLauncher.Tests.csproj
dotnet build --no-restore src\ArabizeGamesLauncher\ArabizeGamesLauncher.csproj
```

Results: `RESULT 61/61 passed`; application build succeeded with `0 Warning(s)` and `0 Error(s)`. The app was built but not started. No network access, dependency installation, game-file write, real Registry mutation, or game launch was performed.

## Remaining boundary

The real Windows Registry restore and real game launch paths have deliberately not been exercised. The installation identity is checked by the existing detector at runtime; this report does not assert a new inspection of the installed game. The user should use the app only after confirming that the detected installation shown in the UI is the intended MW2 Campaign Remastered directory.
