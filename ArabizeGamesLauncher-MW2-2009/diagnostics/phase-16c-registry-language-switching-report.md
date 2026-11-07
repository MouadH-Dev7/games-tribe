# Phase 16C — Safe Registry Language Switching

## Implemented

- Added `WindowsRegistryMutationAdapter`, the production adapter for the existing transaction port. It opens only `HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR`, requests `SetValue` rights for mutations, and reads/writes/deletes by the two approved names only: `LOCALE` and `LOCALE_AUDIO`. It never enumerates values or keys and never creates the target key. A missing key is returned as a safe failure.
- The adapter writes locale targets as `REG_SZ`, and can restore the exact saved Registry kind/data for an existing value. Deletion is limited to the two approved names and is invoked only when a verified snapshot records that value as absent. Each mutation is independently read back and checked for exact data and kind.
- Connected the Game Language page to a user-confirmed Apply workflow. Apply revalidates the exact game build and freshly reads the approved Registry state; it displays current and target pairs and requires a Yes/No confirmation on each attempt. After confirmation it creates a unique snapshot, reloads it, and verifies its SHA-256 before calling the transaction service.
- The page shows target values, backup status, transaction progress, per-value outcomes, and rollback status. Apply is disabled when exact-build detection fails, inspection is unavailable or invalid, the target pair is already configured, an unsupported type exists, backup verification fails, or a transaction is active. Restore and Launch remain disabled.
- On any unsuccessful transaction result, the workflow attempts rollback from the already verified snapshot. It preserves the snapshot and shows per-value apply and rollback outcomes. Failed rollback is surfaced with a manual recovery notice and the retained snapshot filename.
- Hardened Registry error messages to avoid displaying unrequested Registry data. Inspection logs include status/classification only, never locale contents.

## Transaction sequence

1. Require exact-build detection and a successful, fresh inspection of the existing LAZR key.
2. Show the observed `LOCALE` / `LOCALE_AUDIO` pair and the selected Arabic (`arSA`) or English (`enUS`) target.
3. Ask the user to confirm each apply attempt.
4. Capture only the two approved values, preserving absence, exact data, and Registry kind.
5. Write a create-new snapshot under the application data root, outside the game directory, and reload/verify it.
6. Write each target value separately and read back each value.
7. Report success only if both writes are independently verified. Otherwise report each outcome and restore each approved value from the verified snapshot, then verify rollback.

The UI does not infer that Registry configuration proves the language displayed in-game.

## Snapshot and recovery behavior

Phase 16B schema 1 and its SHA-256 envelope remain in use. Snapshot filenames use transaction IDs, writes use create-new temporary files plus a non-overwriting move, and an existing destination is rejected. Snapshots are not deleted automatically, including when post-save verification fails. Loading caps file size, validates schema/scope/exact build identity/two value names/kind-data consistency, and checks the hash. This hash detects accidental or untrusted changes only when the envelope is not also maliciously recomputed; it is not a signature.

Rollback accepts the verified envelope, checks its SHA-256 again, validates the current exact game identity, and restores only the two approved value names. If the original key was absent and remains absent, absence is verified without creating it. The writer never creates a missing key.

## Files changed

- `src/ArabizeGamesLauncher/Services/RegistryTransactionService.cs`
- `src/ArabizeGamesLauncher/Services/RegistryLanguageInspectionService.cs`
- `src/ArabizeGamesLauncher/Services/RegistrySnapshotStore.cs`
- `src/ArabizeGamesLauncher/ViewModels/MainViewModel.cs`
- `src/ArabizeGamesLauncher/ViewModels/TextCatalog.cs`
- `src/ArabizeGamesLauncher/MainWindow.xaml`
- `src/ArabizeGamesLauncher/App.xaml.cs`
- `tests/ArabizeGamesLauncher.Tests/Program.cs`
- `diagnostics/phase-16c-registry-language-switching-report.md`

No package/dependency manifests were changed.

## Test and build results

All tests use fake Registry adapters and temporary installation/app-data directories. No test calls `WindowsRegistryMutationAdapter` or the live Registry.

Command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --no-restore --project tests\ArabizeGamesLauncher.Tests\ArabizeGamesLauncher.Tests.csproj
```

Result: **43/43 passed.** New coverage includes Arabic and English target pairs, snapshot existence before first fake write, snapshot verification failure preventing mutation, second-value failure after first-value success, successful and failed rollback, missing key, denied access, unsupported type, read-back mismatch, exact two-name write scope, confirmation cancellation, already-configured disablement, and the Apply/Restore/Launch UI states.

Build command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build --no-restore src\ArabizeGamesLauncher\ArabizeGamesLauncher.csproj
```

Result: **Build succeeded, 0 warnings, 0 errors.** The test project also built with 0 warnings and 0 errors. No restore, download, or dependency installation was run.

## Remaining blockers before real-world testing

- No real Registry mutation was run in development, tests, build, or verification. The production writer is present and reachable only from the explicit, per-attempt confirmation flow, but its behavior against the user's Windows account has not been runtime-tested.
- Before a user-authorized real switch, review the displayed target pair and confirmation prompt, ensure the target LAZR key exists, and confirm a successful backup path is shown. A failed rollback may require manual recovery using the preserved snapshot.
- The Registry setting does not prove in-game rendering, text coverage, Arabic shaping, subtitles, or audio language. Those remain separate verification tasks.
- Restore and Launch remain disabled pending their dedicated implementation and verification phases.

## Safety confirmation

- No real Windows Registry write or delete occurred. Tests used only the fake adapter. The production adapter was compiled but never invoked.
- No `.reg` file was imported or executed.
- No game files or settings were accessed or modified during this phase; game detection tests used temporary synthetic fixtures.
- Neither the game nor the launcher was started.
- No dependencies were downloaded or installed.
- No Phase 16D work was started.
