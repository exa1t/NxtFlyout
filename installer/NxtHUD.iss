; NxtHUD installer script for Inno Setup 6.3 or later (https://jrsoftware.org/isinfo.php).
;
; Normally built by installer\build-installer.ps1 (locally) or by the Release workflow on
; GitHub, both of which publish the app first and pass the version in. To compile by hand:
; publish to artifacts\publish, then run
;   ISCC.exe /DAppVersion=1.3.0 installer\NxtHUD.iss
;
; The installer is per-user: it needs no administrator rights and installs to
; %LocalAppData%\Programs\NxtHUD. The app is self-contained, so the PC needs nothing else.
;
; The app was called NxtFlyout until 1.3.0. This script upgrades an NxtFlyout install in place:
; same AppId, same install folder, old program files and shortcuts removed, start-with-Windows
; moved to the new name. The app copies the settings across on its first start.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "NxtHUD"
#define AppExe "NxtHUD.exe"
#define AppUrl "https://github.com/exa1t/NxtHUD"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define ApprovedKey "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"

; The app's name before 1.3.0: its program files, shortcuts and Run value are cleaned up on upgrade.
#define LegacyName "NxtFlyout"

[Setup]
; AppId identifies the app to Windows for upgrades and uninstall. Never change it: it is what
; makes this installer upgrade an existing NxtFlyout install instead of adding a second app.
AppId={{B6A1F2C4-7D3E-4A59-9C1B-2E8F5D6A7B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=NxtHUD
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
; New installs go to ...\Programs\NxtHUD. An upgrade keeps the folder the user already has,
; including an NxtFlyout install's ...\Programs\NxtFlyout, so nothing is left behind.
DefaultDirName={autopf}\{#AppName}
UsePreviousAppDir=yes
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=NxtHUD-Setup-{#AppVersion}
SetupIconFile=..\src\NxtHUD\Assets\NxtHUD.ico
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
; LEGACY INTERNAL ID: it still says "NxtFlyout" and must never change, because it is also what
; lets this installer close a running copy of the old NxtFlyout before upgrading it.
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

[InstallDelete]
; Upgrading from NxtFlyout: remove exactly its own program files and shortcuts, which the new
; files do not overwrite because their names changed. Never a wildcard over {app}.
Type: files; Name: "{app}\{#LegacyName}.exe"
Type: files; Name: "{app}\{#LegacyName}.dll"
Type: files; Name: "{app}\{#LegacyName}.Core.dll"
Type: files; Name: "{app}\{#LegacyName}.pdb"
Type: files; Name: "{app}\{#LegacyName}.Core.pdb"
Type: files; Name: "{app}\{#LegacyName}.deps.json"
Type: files; Name: "{app}\{#LegacyName}.runtimeconfig.json"
Type: files; Name: "{autoprograms}\{#LegacyName}.lnk"
Type: files; Name: "{autodesktop}\{#LegacyName}.lnk"

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
; Upgrading from NxtFlyout: its Run value goes. [Code] below re-registers under the new name
; if it was there, so a user who had start-with-Windows keeps it.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: none; ValueName: "{#LegacyName}"; Flags: dontcreatekey deletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent
; A silent upgrade (for example from a script) restarts the app the user already had running.
Filename: "{app}\{#AppExe}"; Parameters: "--autostart"; Flags: nowait; Check: WizardSilent

; Settings and logs in %AppData%\NxtHUD (and %AppData%\NxtFlyout, from before the rename) are
; deliberately left in place on uninstall, so a reinstall picks up where the user left off.

[Code]
var
  LegacyAutostart: Boolean;
  HasLegacyApproval: Boolean;
  LegacyApproval: AnsiString;

{ Moves an NxtFlyout start-with-Windows entry to the new name. [Registry] deletes the old Run
  value during installation, so it is read first, at ssInstall, and acted on at ssPostInstall:
  - it existed: register NxtHUD too, even if the remembered "autostart" task is unticked, since
    the user may have switched it on inside the app after installing;
  - it was switched off in Task Manager: copy that StartupApproved flag to the new name, so it
    stays off. }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    LegacyAutostart := RegValueExists(HKCU, '{#RunKey}', '{#LegacyName}');
    HasLegacyApproval := RegQueryBinaryValue(HKCU, '{#ApprovedKey}', '{#LegacyName}', LegacyApproval);
  end
  else if CurStep = ssPostInstall then
  begin
    if LegacyAutostart then
    begin
      if not RegValueExists(HKCU, '{#RunKey}', '{#AppName}') then
        RegWriteStringValue(HKCU, '{#RunKey}', '{#AppName}', '"' + ExpandConstant('{app}\{#AppExe}') + '" --autostart');

      if HasLegacyApproval then
        RegWriteBinaryValue(HKCU, '{#ApprovedKey}', '{#AppName}', LegacyApproval);
    end;

    RegDeleteValue(HKCU, '{#ApprovedKey}', '{#LegacyName}');
  end;
end;
