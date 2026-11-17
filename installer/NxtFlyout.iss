; NxtFlyout installer script for Inno Setup 6.3 or later (https://jrsoftware.org/isinfo.php).
;
; Normally built by installer\build-installer.ps1 (locally) or by the Release workflow on
; GitHub, both of which publish the app first and pass the version in. To compile by hand:
; publish to artifacts\publish, then run
;   ISCC.exe /DAppVersion=1.1.0 installer\NxtFlyout.iss
;
; The installer is per-user: it needs no administrator rights and installs to
; %LocalAppData%\Programs\NxtFlyout. The app is self-contained, so the PC needs nothing else.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "NxtFlyout"
#define AppExe "NxtFlyout.exe"
#define AppUrl "https://github.com/exa1t/NxtFlyout"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
; AppId identifies the app to Windows for upgrades and uninstall. Never change it.
AppId={{B6A1F2C4-7D3E-4A59-9C1B-2E8F5D6A7B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=NxtFlyout
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=NxtFlyout-Setup-{#AppVersion}
SetupIconFile=..\src\NxtFlyout\Assets\NxtFlyout.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; A short wizard: choose options, install, finish. A per-user app needs no folder or group choice.
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
; The name of the app's single-instance mutex (see AppInfo.InstanceId and SingleInstance.cs).
; Lets setup and uninstall ask for a running copy to be closed first, so upgrades just work.
AppMutex=NxtFlyout-6f0e2c1a-9b7d-4c35-8e21-3d5a7b9c0f42.Mutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Checked by default: a tray utility is only useful while it is running. Inno Setup remembers
; the choice, so an upgrade keeps whatever the user picked last time.
Name: "autostart"; Description: "Start {#AppName} automatically when I sign in to Windows"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Start with Windows. The value matches what the app writes itself (AppInfo.GetAutostartCommand),
; so the app's own "Start with Windows" switch shows it as on and can turn it off again.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --autostart"; Flags: uninsdeletevalue; Tasks: autostart
; Whether or not the task was chosen, uninstall removes a registration the app may have made itself.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: none; ValueName: "{#AppName}"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent
; A silent upgrade (for example from a script) restarts the app the user already had running.
Filename: "{app}\{#AppExe}"; Parameters: "--autostart"; Flags: nowait; Check: WizardSilent

; Settings and logs in %AppData%\NxtFlyout are deliberately left in place on uninstall, so a
; reinstall picks up where the user left off.
