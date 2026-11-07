# Phase 3 — Silksong Arabic Launcher and Transactional Installer

## Result

The dedicated Windows WPF launcher, manifest validation, transactional install/restore, interrupted-transaction recovery, and game launch safeguards are implemented. The application and test project build successfully. All 16 automated tests pass, including a read-only preflight against the supplied local game and translation package.

No translation was installed into the real game directory, and the game was not launched. The implementation is ready for review; the package/runtime compatibility concerns documented in Phase 2 remain unresolved and are surfaced to the user before install and launch.

## Implemented

- A dedicated Arabic-friendly WPF application with Arabic/English interface direction toggle, game and package path selection, validated game candidates, readiness/conflict display, operation status/log, and Install, Uninstall/Restore, Recover, and Launch actions.
- Discovery based on the Silksong executable and required Unity data files, with the known candidate path, a manually selected directory, and Steam library locations from local VDF files. No Registry lookup is used.
- Strict Phase 2 manifest validation. The shipped manifest is pinned to SHA-256 `A1365072716C1D6B917212A88E8EFBF6D7A7BFA1CEB5F70AD2B6A2B60935EB88`; edits to the approval manifest are rejected. Every package file, including the excluded instructions file, is hash-checked, and unlisted files, unsafe paths, changed destinations, and the wrong game executable are rejected.
- Pre-install revalidation of the package, game identity, destination paths, process state, free space, active transactions, and pending recovery journals.
- Staging and verified backups outside the game directory under the launcher’s LocalAppData data root. A unique transaction ID, sealed transaction records, and a write-through recovery journal track each add/replacement. Files are committed from temporary siblings and verified by size and SHA-256.
- Rollback on failed installs, restart recovery for interrupted staging/commit, hash-gated uninstall, exact restoration of the replaced English font bundle, and preservation/reporting of files changed after installation. Uninstall removes only transaction-listed files; it does not remove a broad BepInEx directory.
- Game launch only after checking the selected executable and installed transaction. The launcher does not change the in-game language; the supplied package instructs the user to select English.
- Explicit confirmation before copying the package’s 21 unsigned runtime DLLs, and a separate warning confirmation before launch. The launcher itself does not load or execute supplied DLLs, installers, or scripts.

## Verification

Builds completed with **0 warnings and 0 errors**:

- `src/SilksongArabicLauncher/SilksongArabicLauncher.csproj`
- `tests/SilksongArabicLauncher.Tests/SilksongArabicLauncher.Tests.csproj`

Test result: **16/16 passed**. Coverage includes manifest and source hashes, pinned-manifest tampering, path traversal, destination conflicts, unlisted package files, runtime confirmation, running-game protection, install/verification/uninstall with exact restoration, failed-commit rollback, interrupted recovery, stale installed-record cleanup, blocking installs while recovery is pending, preservation of user-modified files, changed executable rejection, launch confirmation/path validation using a fake starter, and read-only discovery/preflight against the actual local game and package.

All destructive test operations used isolated temporary fixture directories. The real-path test only read metadata and files to validate the game/package and hashes; it did not copy, stage, or write anything there.

## Remaining limitations

The Phase 2 audit remains authoritative for compatibility: the exact game content build was not established; package DLL provenance is unverified and all 21 DLLs were unsigned; dependency versions differ; actual runtime loading, translation behavior, Arabic shaping/rendering, and font bundle compatibility have not been tested. The launcher therefore does not claim compatibility. Installing the package will configure Doorstop files so the game may load the runtime on a later user-confirmed launch. The first launch can fail or behave unexpectedly; the UI warns about this unresolved risk.

The app has not been opened interactively in this phase, and no real installation, uninstall, recovery, or game launch was performed. The actual local directory and package passed the non-mutating detection and manifest preflight checks at test time; those files can change later, so the launcher revalidates before installation.

## Safety and incidental SDK side effect

The MW2CR reference project, real Silksong installation, translation package, and Registry were not intentionally modified or accessed for writing. No game or supplied DLL was launched or loaded. No external packages or tools were downloaded. No Registry API or Registry command was used.

During the initial .NET SDK first-use setup, the SDK reported: “Installed an ASP.NET Core HTTPS development certificate.” This was an automatic tooling side effect; no certificate-store or Registry inspection/removal was attempted. It is disclosed here rather than claiming that the machine had no incidental SDK changes.

## Main files

- WPF application: `src/SilksongArabicLauncher/`
- Installer, validation, discovery, recovery, and launch logic: `src/SilksongArabicLauncher.Core/`
- Synthetic test suite: `tests/SilksongArabicLauncher.Tests/`
- Phase 2 approved manifest and compatibility audit: `diagnostics/phase-2-installation-manifest-proposal.json` and `diagnostics/phase-2-provenance-compatibility-audit.md`

Phase 3 is complete. No live installation or later phase was started.

## Arabify packaging and simplified interface follow-up (2026-11-04)

The launcher UI now uses the **Arabify** name and a simplified Arabic layout. It has a game path field, an automatic search action for the known audited folder and known Steam libraries, a manual game-folder picker, a clear install button, a remove/restore button, and a launch button. Install and remove remain visible and are enabled only when the validated state permits the action.

The earlier development UI depended on the translation package remaining at the external `C:\Users\mouad\Desktop\New folder (3)` path. The project now contains a separate `TranslationPackage` copy, and the app reads it relative to its own install directory. The original package folder was left unchanged. All 65 copied package entries were SHA-256 checked against the pinned Phase 2 manifest, including the excluded instructions file. A Release publish completed and includes all 65 files under `src/SilksongArabicLauncher/bin/Release/net10.0-windows/TranslationPackage`.

An Inno Setup script is available at `installer/Arabify-Silksong.iss`. It packages the Release output and bundled package under Program Files and creates Start Menu and optional desktop shortcuts. It does not install the translation into the game during setup; the in-app install remains a separate user action. The setup script itself was not compiled in this follow-up. The target PC needs the .NET 10 Windows Desktop Runtime.

No application window or game was launched, and no real game files were written. The Release publish only wrote build artifacts into this launcher project. No test suite was run for this UI/package follow-up.

## Launcher usability and reinstall fix (2026-11-04)

After the user reported that automatic search, folder selection, and install were unusable, a read-only check found that the local game executable still matches its audited SHA-256 and size, UnityPlayer version and required game markers. The install block came from an existing package destination: `BepInEx/config/com.hesham.rtl.runtime.cfg`. It was 2,981 bytes and its current SHA-256 differed from the package copy. No transaction records were present under the launcher's LocalAppData data root, so its provenance could not be attributed to a prior Arabify transaction. The current English font bundle still matches the audited original hash.

The installer now recognizes a file conflict only when it is at an exact file destination listed in the pinned manifest. Other preflight errors still block installation. If the user confirms the warning, Arabify records the existing file's current size and SHA-256, copies and verifies it into the new transaction backup, then replaces it. Uninstall restores that captured version if the installed file remains unchanged. The normal original-font replacement check remains pinned to its audited hash. This provides a safe path for the current modified config without deleting it silently.

The folder picker now uses a Windows folder browser with the WPF window as owner and reports dialog errors in the interface. Automatic search reports why known candidates failed. The game picker remains available for other locations, and the bundled package is no longer selected from an external folder. The screen uses the supplied Arabize palette: `#A3E635`, `#38BDF8`, `#00F0FF`, `#0B0D12`, `#171C27`, `#2A3140`, `#F4F6FA`, and `#9AA4B2`, with amber `#F59E0B` for the unresolved runtime warning.

The updated Release app published successfully. The current 65-file bundled package continues to match the Phase 2 manifest. The game was inspected read-only to identify the destination conflict; no game file was changed and the game was not launched. No automated test suite or Inno Setup compilation was performed for this follow-up.

## User-reported disabled controls: direct UI investigation (2026-11-04)

The user asked for the launcher to be tested directly. The Release app was opened and its actual accessibility/UI state inspected. Automatic discovery worked and reported the game validated. The manual folder button opened the Windows folder picker. The install action was disabled on the first launch because the simplified UI omitted the recovery action while `FindRecoverable` found one old transaction in `NeedsManualRecovery`; that transaction made `CanInstall` false. The old record contains 64 package operations and the install journal is completed. A residual modified package config is at `BepInEx/config/com.hesham.rtl.runtime.cfg`.

The correction checks the prior transaction against the current game before allowing continuation: every old replacement must already match its original hash; every remaining added file must be absent or correspond exactly to a manifest-listed conflict whose size and SHA-256 were just captured. Only one fully matched prior recovery record can be superseded. The new transaction first backs up the current residual file and records the prior transaction ID; only after the new transaction completes is the old record marked superseded. Other pending/invalid transactions or unrelated modified files still block installation. Old transaction records without the new optional linkage fields remain readable.

The updated Release app was opened again. In the live UI, auto-search returned `تم التحقق من اللعبة: C:\Games\Hollow Knight - Silksong`, the package showed the one file that will be preserved in a verified backup, and **تثبيت التعريب** was enabled. The manual folder picker also opened successfully and was cancelled without changing the selected path. The user’s supplied colors are active. The install button, recovery button, uninstall button, and launch button were not activated; no real game file was changed and the game was not launched.

The updated Release publish completed successfully. This direct UI check confirms the reported block was the stale recovery record and that the automatic search and folder chooser now respond. It does not verify installing or runtime compatibility. The Inno Setup script remains uncompiled; compile it again to distribute this updated Release build.

## Launcher controls and install flow update (2026-11-04)

Per the user’s request, the game-launch button and its launcher command were removed. The pre-install unsigned-runtime confirmation dialog and the compatibility warning banner were removed from the WPF interface. Pressing **تثبيت التعريب** is now the explicit user action that starts installation, with no additional approval popup. Existing path validation, package hashes, destination checks, free-space checks, process guard, verified backups, and transaction recovery still gate the operation. Successful completion asks the user to select English in-game. The launcher does not start the game.

The Release build was regenerated successfully. No real installation or game launch was performed for this UI change.

## Arabify branding and operation-log removal (2026-11-04)

The operation-history list and its backing collection were removed; the compact current-status message remains for search and install results. The supplied Arabify logo sheet was cropped to create `logo/ArabifyAppIcon.png`, then packaged as `logo/ArabifyApp.ico`. The image appears in the launcher header, the executable receives the ICO as its application icon, and Inno Setup uses the ICO for its installer icon.

The first Release publish found a missing `ObservableCollection` namespace import still needed by the game-candidate and validation lists. The import was restored without restoring the operation log, and the second Release publish completed successfully. The Inno Setup script references the new installer icon; the setup executable itself was not compiled. No game installation was accessed or changed, and neither launcher nor game was started.

## UI cleanup from user screenshots (2026-11-04)

Repeated low-level validation messages are no longer printed as long paragraphs in the main screen. The UI now gives a short summary and issue count, while retaining the installer’s validation gates. The header uses separate icon and title columns with additional window width so the logo and mixed Arabic/Latin title do not collide. Release publishing succeeded after this update. The app was not launched for a visual runtime check.

## Clickable actions and neutral in-game extras label (2026-11-04)

The language-toggle control was removed. Install and restore buttons remain clickable while idle so users can see a concise reason when prerequisites are not met; the same validation, transaction, backup, and recovery guards still reject unsafe actions. The `MAIN_EXTRAS` entry in the bundled Arabic `MainMenu/EN.yml` was changed from the translator credit to the neutral label `إضافات`. Its package size/hash, package inventory CSV, total byte count, manifest digest, and launcher-pinned manifest digest were updated together so package verification remains active. No game files were changed. A first publish attempt found the running launcher holding its output executable; after the launcher was no longer running, Release publishing completed successfully. The application was not launched by this work.

## Uninstall feedback and active-game guard fix (2026-11-04)

The user's screenshot showed the game still running. `TransactionalInstaller.UninstallCore` correctly refuses to change game files while the game process is active, but the view model's `finally` refresh replaced the refusal message with the normal installed status. Refresh can now preserve operation results, and uninstall failures have concise Arabic feedback, including an instruction to close the game first. The install/uninstall guards and backup verification were not weakened. Release publishing succeeded; no game process was stopped and no game files were changed. To update the already-installed launcher, use the newly published Release executable or rebuild the Inno Setup package from the updated Release output. The existing in-game menu text changes only after uninstalling the old package and installing the updated package.

## Read-only reproduction of package-not-ready state (2026-11-04)

The launcher was opened and its visible state was inspected. It had selected `C:\Games\Hollow Knight - Silksong` and reported 64 preflight issues. A read-only check of the 64 manifest destinations found 63 existing `ADD` destinations and one `REPLACE` destination whose current SHA-256 (`52F2D2BBF6D5EA90976718021EAE5985F846C5010B145BB286E062F7574BAE2E`) does not match the recorded original font hash (`125993E4121D83F5D7F72F3193E820375A1FE86BF63B6061BC48B479675BBC9A`). All 64 package sources matched the updated manifest. Thus this folder cannot be safely treated as a fresh installation or have its replaced font restored without a matching transaction backup.

Read-only transaction metadata under the ordinary user LocalAppData contained an active 64-operation transaction for `C:\Hollow Knight - Silksong` (`Installed`), while the transaction for `C:\Games\Hollow Knight - Silksong` was `Uninstalled`. The discovery code previously did not include game roots referenced by installed transactions, so it stayed on the other valid game copy and could not offer the correct restore record. The launcher now discovers and validates installed transaction roots and, when there is exactly one valid registered target and the user has not manually selected a folder, selects it automatically. Manual folder selection remains respected. Release publishing succeeded after this fix. No game files were changed, and no install/uninstall button was activated.

## Remove the remaining extras label (2026-11-04)

The `MAIN_EXTRAS` entry was first blanked at the user's request, then restored to `إضافات` on 2026-11-04. The menu entry itself is controlled by the game and is not removed by this text package. The file inventory, package size/hash, manifest digest, and pinned digest were restored to match the localized label.

The Release package was published to `dist/Arabify-Silksong-Updated-20261104` because prior output folders were locked. The Inno Setup source packages this directory. The manifest pin and bundled menu-text hash were verified against their source files. Inno Setup itself was not compiled because `ISCC.exe` was not found in PATH or its standard Program Files locations.

The WPF window now explicitly uses the bundled multi-size ICO and `ShowInTaskbar=True`; the ICO is embedded as a WPF resource and remains the executable application icon. Inno Setup shortcuts explicitly use the executable icon. Successful installation now leaves the main installed status but no longer shows the additional English-language reminder. The installer and UI XAML built successfully. A direct visual taskbar check was inconclusive because the UI automation returned two identical launcher windows, so no taskbar-rendering claim is made.

The user provided a successful Inno Setup compile log with the deprecated `x64` architecture identifier warning. The script now uses the official recommended `x64compatible` identifier. The user's compile output was successful; this environment does not have ISCC, so the warning-free recompile was not run here. Official Inno documentation explains that `x64` aliases `x64os` and is deprecated, while `x64compatible` is preferred for x64-capable systems, including Windows 11 on Arm: https://jrsoftware.org/ishelp/topic_archidentifiers.htm
