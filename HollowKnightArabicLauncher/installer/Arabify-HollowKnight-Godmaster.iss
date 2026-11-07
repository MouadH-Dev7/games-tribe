#define AppName "Arabify — Hollow Knight Godmaster Arabic Launcher"
#define AppVersion "1.0.0"
#define AppPublisher "Arabify"
#define AppExeName "HollowKnightArabicLauncher.exe"

[Setup]
AppId={{5D99F3C0-63D8-4E0F-A6B4-0D642B26D59E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Arabify\Hollow Knight Godmaster Arabic Launcher
DefaultGroupName=Arabify
OutputDir=..\dist
OutputBaseFilename=Arabify-HollowKnight-Godmaster-Setup
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
SetupIconFile=..\logo\ArabifyApp.ico
UninstallDisplayName={#AppName}
DisableProgramGroupPage=yes

[Files]
Source: "..\dist\publish-runtime-check\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\Arabify\Hollow Knight Godmaster Arabic Launcher"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; IconIndex: 0
Name: "{autodesktop}\Arabify Hollow Knight"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; IconIndex: 0; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Open Arabify"; Flags: postinstall nowait skipifsilent

