#define MyAppName "WinSwitch"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "Codex"
#define MyAppExeName "WinSwitch.exe"

#ifndef Architecture
  #define Architecture "x64"
#endif

#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

#if Architecture == "arm64"
  #define MyInstallerSuffix "ARM64"
  #define MyArchitecturesAllowed "arm64"
  #define MyArchitecturesInstallMode "arm64"
#else
  #define MyInstallerSuffix "x64"
  #define MyArchitecturesAllowed "x64compatible"
  #define MyArchitecturesInstallMode "x64compatible"
#endif

[Setup]
AppId={{A5A9E5AA-DF2C-4B22-93BC-EFF287F2A9D0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma
SolidCompression=yes
OutputDir=output
OutputBaseFilename=WinSwitch-Setup-{#MyInstallerSuffix}
ArchitecturesAllowed={#MyArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#MyArchitecturesInstallMode}
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Launch WinSwitch when Windows starts"; GroupDescription: "Startup options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
