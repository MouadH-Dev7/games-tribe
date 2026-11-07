# Phase 16B — Registry Inspection and Transaction Infrastructure

## Summary

Implemented a narrowly scoped, read-only Windows Registry inspection adapter, an explicit user-triggered inspection action, versioned integrity-checked Registry snapshots, and transaction orchestration that is exercised only with fake adapters in tests. There is no real Registry mutation adapter, and Apply, Restore, and Launch remain disabled in the launcher.

## Registry access boundary

The Windows adapter opens only:

`HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR`

It queries only `LOCALE` and `LOCALE_AUDIO` by name. It does not call value/key enumeration methods and does not read any other Registry path. It opens the key with `writable: false` and uses `DoNotExpandEnvironmentNames`, preserving the literal Registry string.

The adapter is constructed at application startup but performs no inspection then. Its `ReadApprovedValues` method is called only by the Game Language page’s **Inspect Current Settings** command. The UI labels this as Registry configuration and does not infer that the game is displaying a language from those values. Inspection result logging contains only the classification and per-value statuses; literal values are not written to logs or reports.

## Value and classification behavior

Each approved value reports existence, data, and `RegistryValueKind`. The model distinguishes a supported `REG_SZ`, missing value, unsupported type, denied access, missing key, and unexpected error. Classification is:

- Arabic pair only when both values are `REG_SZ` and exactly `arSA`.
- English pair only when both values are `REG_SZ` and exactly `enUS`.
- Mixed for other combinations of present supported strings.
- Missing if the key or either value is absent.
- Unsupported if either existing value has a non-`REG_SZ` type.
- Unknown on denied access or unexpected read failures.

## Snapshot format and integrity

Schema version 1 stores a unique transaction ID, UTC timestamp, exact Campaign Remastered build identity, approved hive/subkey, key existence, and fixed fields for `LOCALE` and `LOCALE_AUDIO`. Each value records its existence; if present, its Registry kind and typed JSON data are retained. The fixed model cannot include unrelated values.

Snapshots are serialized deterministically using compact JSON with declared model-field order and camel-case property names. SHA-256 is computed over the canonical snapshot payload and stored in the envelope. Loading validates the schema, exact target build identity, Registry scope, value names, presence/kind/data consistency, a 1 MiB file-size cap, and the hash. Hash verification detects corruption; it is not a digital signature or proof of authorship.

`RegistrySnapshotStore` stores files in `<AppPaths.Root>\registry-backups`. It writes to a uniquely named temporary file using create-new semantics, flushes it, atomically moves it to a transaction-ID filename without overwrite, and reloads/verifies it. A duplicate filename is rejected. The API checks that this backup directory remains under the application root and outside the supplied game directory.

## Transaction boundary

`IRegistryMutationAdapter` describes the future narrow write/delete/read-back port. `RegistryTransactionService` can stage Arabic or English pairs and restore a verified snapshot, including deleting only values recorded as originally absent. It verifies each value independently and returns per-value outcomes plus an overall partial-failure indicator.

No production implementation of `IRegistryMutationAdapter` exists, and the transaction service is not connected to the UI. Tests use only the fake adapter. Apply, Restore, and Launch remain disabled.

## Files changed

- `src/ArabizeGamesLauncher/Models/RegistryLanguageModels.cs`
- `src/ArabizeGamesLauncher/Services/RegistryLanguageInspectionService.cs`
- `src/ArabizeGamesLauncher/Services/RegistrySnapshotStore.cs`
- `src/ArabizeGamesLauncher/Services/RegistryTransactionService.cs`
- `src/ArabizeGamesLauncher/Services/AppPaths.cs` (added an injectable app-data root for isolated tests)
- `src/ArabizeGamesLauncher/App.xaml.cs` (registers the read-only adapter; does not invoke it)
- `src/ArabizeGamesLauncher/ViewModels/MainViewModel.cs`
- `src/ArabizeGamesLauncher/ViewModels/TextCatalog.cs`
- `src/ArabizeGamesLauncher/MainWindow.xaml`
- `tests/ArabizeGamesLauncher.Tests/Program.cs`
- `diagnostics/phase-16b-registry-inspection-and-transaction-report.md`

No dependency or package manifest was changed.

## Verification

Tests use a fake Registry adapter and temporary directories; they do not use the real Registry. Coverage includes Arabic/English pair recognition, missing values, unsupported types, denied/missing/unexpected states, mixed values, exact read scope, explicit-click-only UI inspection, snapshot round-trip and SHA-256 verification, tampering and unsupported schema rejection, duplicate filename rejection, missing-value and type restoration, and partial fake transaction failure.

Test command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet run --no-restore --project tests\ArabizeGamesLauncher.Tests\ArabizeGamesLauncher.Tests.csproj
```

Result: **34/34 passed.**

Build command:

```powershell
$env:DOTNET_CLI_HOME = (Join-Path (Get-Location) '.dotnet-cli')
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build --no-restore src\ArabizeGamesLauncher\ArabizeGamesLauncher.csproj
```

Result: **Build succeeded, 0 warnings, 0 errors.** No dependency restore or installation was performed.

## Remaining Phase 16C blockers

- The Registry transaction port has no real writer; no operational Registry changes are available.
- A future write flow needs explicit authorization, verified snapshot creation before any change, careful handling of key absence and partial failure, and read-back verification against the real adapter.
- The inspection result reports Registry configuration only; it does not verify the game’s in-game language or Arabic rendering/audio behavior.
- Game compatibility tests remain synthetic; the live game installation was not accessed in this phase.

## Safety confirmation

- No live Registry read or write occurred during implementation, build, or tests. The real read adapter was not invoked.
- No Registry values were written or deleted.
- No game installation files or settings were accessed or changed.
- No `.reg` file was imported or executed.
- Neither the game nor the launcher was started.
- No dependencies or tools were downloaded or installed.
