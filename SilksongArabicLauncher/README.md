# Arabify — Silksong Arabic Launcher

Arabify is a small Windows launcher for the locally supplied Hollow Knight: Silksong Arabic package. The verified package is bundled under `TranslationPackage`; the user does not need to browse to a separate translation folder.

## Simple use

1. Open Arabify.
2. Choose **بحث تلقائي عن اللعبة** to check the audited game folder and known Steam library folders. If it is not found, use **اختيار مجلد اللعبة**.
3. When the game and bundled package both pass validation, choose **تثبيت التعريب**.
4. Choose **إلغاء التعريب واستعادة الأصل** to remove the installed translation and restore the backed-up original files.

The install and remove buttons remain visible. They are enabled only when the selected game and package state allows that action. The launcher checks the bundled package on startup and again before installation. There is no extra confirmation popup before install: pressing **تثبيت التعريب** starts the operation. If a file already exists at a package destination, its path is shown and Arabify backs it up for restoration during removal.

## Build and create the setup package

Build a Release version of the WPF project:

```powershell
dotnet restore src\SilksongArabicLauncher\SilksongArabicLauncher.csproj --configfile NuGet.Config
dotnet publish src\SilksongArabicLauncher\SilksongArabicLauncher.csproj -c Release --no-restore -o src\SilksongArabicLauncher\bin\Release\net10.0-windows
```

Open `installer\Arabify-Silksong.iss` with Inno Setup Compiler and compile it. The setup script includes the launcher output and the bundled `TranslationPackage` directory. The target computer needs the .NET 10 Windows Desktop Runtime.

## Safety and compatibility

- The package is copied into the project from the supplied folder; the original package folder is not changed. Its Phase 2 manifest and SHA-256 checks remain in force.
- The game folder is selected or discovered by checking the executable identity and required Unity files. Automatic search checks the known game folder and known Steam library locations; it does not scan every drive.
- Backups and transaction records are stored under `%LOCALAPPDATA%\SilksongArabicLauncher`, outside the game directory.
- The launcher does not change the in-game language. Choose English in the game as the package instructions require.
- The package contains unsigned runtime DLLs, its provenance is unverified, and dependency versions differ. Compatibility is not claimed. Start the game from its usual game client after installation; the launcher has no game-launch button.
- The launcher does not load package DLLs itself. Doorstop is configured by the package and may load them when the user starts the game.
- The Inno Setup script installs the launcher under Program Files. It does not install the translation into the game unless the user later chooses the in-app install action.
