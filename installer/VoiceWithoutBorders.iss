; Per-user installer around the single-file exe: no admin rights, installs to %LOCALAPPDATA%\Programs.
; Built by .github/workflows/release.yml after the publish step:
;   iscc installer\VoiceWithoutBorders.iss   ->  publish\VoiceWithoutBorders-Setup-x.y.z.exe
; The in-app updater looks for exactly that asset name (GitHubUpdateChecker.Pick) and its .sha256.
#define MyAppName "Voice Without Borders"
#define MyAppExeName "VoiceWithoutBorders.exe"
; Read from the exe (the csproj's <Version>, as x.y.z.0), so the installer can never be labelled with a version
; other than the one it installs.
#define ExeVersion GetVersionNumbersString(SourcePath + "..\publish\win-x64\" + MyAppExeName)
#if ExeVersion == ""
  #error Publish the exe to publish\win-x64 first.
#endif
#define MyAppVersion Copy(ExeVersion, 1, RPos(".", ExeVersion) - 1)
#define MyAppURL "https://github.com/Lunat1q/VoiceWithoutBorders"

[Setup]
; Never change: it is how upgrades find the installed copy.
AppId={{5B8E2C4D-9A31-4F6E-B7D2-3C8A1E0F4B96}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=Lunat1q
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppCopyright=Copyright (C) 2026 Lunat1q. Licensed under GPL-3.0-or-later.
DefaultDirName={autopf}\VoiceWithoutBorders
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
LicenseFile=..\LICENSE
OutputDir=..\publish
OutputBaseFilename=VoiceWithoutBorders-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\VoiceWithoutBorders\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; A running copy (often in the tray) is closed for the update; the app also closes itself before an in-app update.
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; GPL: the license text travels with the binary.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; "Start with Windows" is written by the app (RunKeyStartupRegistration); only removed here, on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "VoiceWithoutBorders"; Flags: uninsdeletevalue

[Run]
; No skipifsilent: after a silent in-app update the app reopens by itself. launchargs carries --resume when it was
; streaming (InnoSetupUpdateInstaller), so it streams again.
Filename: "{app}\{#MyAppExeName}"; Parameters: "{param:launchargs}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall
