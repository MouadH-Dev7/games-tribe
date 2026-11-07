# Arabify — Hollow Knight Arabic Launcher

A small Arabic Windows app for detecting Hollow Knight, verifying a locally bundled Arabic package, and managing reversible installation transactions. Godmaster is handled as content for the base Hollow Knight PC installation; this launcher does not search for a separate Godmaster executable.

## Current verification status

The selected game folder has been inspected: `hollow_knight.exe` and `UnityPlayer.dll` are version `2017.4.6.12734256`. The supplied `arablocn_hk_v2` folder contains BepInEx and SnakeArabic files; its local runtime log reports Unity `6000.0.61.7643309`. This runtime discrepancy leaves package compatibility unverified. Package provenance and licensing are also not independently established.

The 20 selected package files are bundled and SHA-256 checked. **Install remains disabled** until the package is verified for the identified game build. The game executable size/version/SHA-256 are already pinned in the manifest. The guard is deliberate; do not change `compatibilityVerified` to `true` until there is reliable evidence that this package works with the identified Unity 2017.4.6 build. If that is established, update the manifest flag and its pinned SHA-256 in `App.xaml.cs`.

## Build

From this directory, with the .NET 10 Windows Desktop SDK/runtime available:

```powershell
dotnet publish src\HollowKnightArabicLauncher\HollowKnightArabicLauncher.csproj -c Release -o dist\publish
```

Then open `installer\Arabify-HollowKnight-Godmaster.iss` in Inno Setup Compiler and compile. The output path is `dist\Arabify-HollowKnight-Godmaster-Setup.exe`. The launcher installer includes the bundled `TranslationPackage` files. When the game is installed under a protected folder such as Program Files, run Arabify as administrator for install or restore operations. It does not install them into the game during setup; that is controlled by the in-app transaction flow, which is currently blocked pending exact-build verification.

The installer requires the .NET 10 Windows Desktop Runtime on the target computer. No game files, registry settings, or permissions are changed by opening the launcher or building the installer.



