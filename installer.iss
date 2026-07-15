; KOFplanner Inno Setup Installer
; Author: Lukas Sonderegger
; Version: 1.0.0.0

#define MyAppName "KOFplanner"
#define MyAppVersion "1.0.0.0"
#define MyAppPublisher "Lukas Sonderegger"
#define MyAppURL "https://github.com/soendi/KOFplanner"
#define MyAppExeName "KOFplanner.exe"
#define AppDate GetDateTimeString('yyyy-mm-dd')

[Setup]
AppId={{B8F7A3D2-9E5C-4A1B-8D6F-3C2E5A7B9D0F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName=C:\Programme\KOFplanner
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=KOFplanner-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktopverknüpfung erstellen"; GroupDescription: "Zusätzliche Symbole:"; Flags: checkedonce

[Files]
Source: "pub\*.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "pub\*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: "HKCU"; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; Flags: uninsdeletekey
Root: "HKCU"; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: "HKCU"; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletevalue
Root: "HKCU"; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallDate"; ValueData: "{#AppDate}"; Flags: uninsdeletevalue

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

