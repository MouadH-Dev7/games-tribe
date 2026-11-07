# Phase 2 verification

## Build

- SDK: .NET 10.0.401
- WPF solution build: succeeded with 0 warnings and 0 errors.
- Windows app publish: succeeded as a framework-dependent Windows build in `../publish/windows-x64/`.
- A self-contained publish was not produced: the .NET 10 Windows runtime packs are not installed locally, and restore is configured without external package feeds.

## Automated tests

The package-free test harness was run with temporary fixtures and reported **14/14 passed**:

- supported Campaign Remastered fixture detection
- unsupported executable identity and invalid path rejection
- compatible package parsing and SHA-256 validation
- unsupported build, unsafe paths, missing manifest, and corrupted payload rejection
- backup creation and integrity verification
- restoration planning without modifying the source
- missing and corrupted backup rejection
- Arabic/English interface text switching, including evidence labels

Test command:

```powershell
dotnet run --project tests/ArabizeGamesLauncher.Tests/ArabizeGamesLauncher.Tests.csproj --no-build
```

## Manual application check

The WPF app was opened and visually reviewed in Arabic and English. Fresh detection identified the requested local Campaign Remastered installation from executable metadata and build files. The app found no package in its local package directory. Install and restore stayed disabled. The game itself was not launched.

The test suite did not modify or use the real game directory. Backup and restore behavior was exercised only against temporary test files. The game installation, registry, and configuration files remain untouched.
