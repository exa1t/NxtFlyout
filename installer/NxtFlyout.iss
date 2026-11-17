; NxtFlyout installer script for Inno Setup 6.3 or later (https://jrsoftware.org/isinfo.php).
;
; Build it with installer\build-installer.ps1, which publishes the app first and passes the
; version in. To compile by hand: publish to artifacts\publish, then run
;   ISCC.exe /DAppVersion=1.0.0 installer\NxtFlyout.iss
;
; The installer is per-user: it needs no administrator rights and installs to
; %LocalAppData%\Programs\NxtFlyout.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "NxtFlyout"
#define AppExe "NxtFlyout.exe"

[Setup]
; AppId identifies the app to Windows for upgrades and uninstall. Never change it.
AppId={{B6A1F2C4-7D3E-4A59-9C1B-2E8F5D6A7B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=NxtFlyout
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=NxtFlyout-Setup-{#AppVersion}
SetupIconFile=..\src\NxtFlyout\Assets\NxtFlyout.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; The name of the app's single-instance mutex (see AppInfo.InstanceId and SingleInstance.cs).
; Lets setup and uninstall ask for a running copy to be closed first.
AppMutex=NxtFlyout-6f0e2c1a-9b7d-4c35-8e21-3d5a7b9c0f42.Mutex
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[Registry]
; The app registers itself to start with Windows from its own settings. This entry writes
; nothing at install time; it only makes uninstall remove that registration.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: dontcreatekey uninsdeletevalue

; Settings and logs in %AppData%\NxtFlyout are deliberately left in place on uninstall.
