#define AppName "Arabify — Silksong Arabic Launcher"
#define AppVersion "1.0.0"
#define AppPublisher "Arabify"
#define AppExeName "SilksongArabicLauncher.exe"

[Setup]
AppId={{A8435EAD-7D18-4C4B-BF42-3EAAD3D7F932}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Arabify\Silksong Arabic Launcher
DefaultGroupName=Arabify
OutputDir=..\dist
OutputBaseFilename=Arabify-Silksong-Setup
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
SetupIconFile=..\logo\ArabifyApp.ico
UninstallDisplayName={#AppName}
DisableProgramGroupPage=yes

[Files]
Source: "..\dist\Arabify-Silksong-Updated-20261104\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\Arabify\Silksong Arabic Launcher"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; IconIndex: 0
Name: "{autodesktop}\Arabify Silksong"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; IconIndex: 0; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Open Arabify"; Flags: postinstall nowait skipifsilent
