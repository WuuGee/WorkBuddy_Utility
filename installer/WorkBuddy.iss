; WorkBuddy installer (Inno Setup 6). Build with installer\Build-Installer.ps1.
; Per-user install: no administrator rights, nothing outside the user's profile and HKCU.

#ifndef AppVersion
  #define AppVersion "0.1.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "WorkBuddy"
#define AppPublisher "Qian Tong Low"
#define AppExe "WorkBuddy.exe"
#define AppUrl "https://github.com/WuuGee/WorkBuddy_Utility"

[Setup]
; Never change AppId: it is how Windows and future installers recognise this product (upgrades, Apps & Features).
AppId={{B7C4F0A2-5D1E-4C8B-9A63-2E7F1D4B8C90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Qian Tong Low
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=WorkBuddy-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
SetupLogging=yes
; Windows 10 1809 or later (Windows 11 is the primary target).
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Startup:"; Check: ShowStartupTask

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; The data folder is created at install time so it exists before first launch. It is deliberately NOT listed for
; removal: uninstall asks the user first (see [Code]).
Name: "{code:DataDir}"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Written for both a fresh install (task ticked by default) and an upgrade where the user kept startup on.
; The value is exactly what the app itself writes (StartupManager), so the app sees it as Registered, not mismatched.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
    ValueData: """{app}\{#AppExe}"" --autostart"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ask a running instance to quit so its files are not locked. Exit code is 0 whether or not one was running.
Filename: "{app}\{#AppExe}"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "QuitWorkBuddy"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  ApprovedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';

{ /DATADIR=<path> exists so verification can run against a scratch folder; users never need it. }
function DataDir(Param: String): String;
var
  I: Integer;
  S: String;
begin
  Result := ExpandConstant('{localappdata}\{#AppName}');
  for I := 1 to ParamCount do
  begin
    S := ParamStr(I);
    if CompareText(Copy(S, 1, 9), '/DATADIR=') = 0 then
      Result := RemoveQuotes(Copy(S, 10, Length(S)));
  end;
end;

{ /LEGACYDIR=<path> exists so verification can use a scratch folder for the pre-rename FocusCatAI data. }
function LegacyDir(): String;
var
  I: Integer;
  S: String;
begin
  Result := ExpandConstant('{localappdata}\FocusCatAI');
  for I := 1 to ParamCount do
  begin
    S := ParamStr(I);
    if CompareText(Copy(S, 1, 11), '/LEGACYDIR=') = 0 then
      Result := RemoveQuotes(Copy(S, 12, Length(S)));
  end;
end;

function DeleteLegacyRequested(): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/DELETELEGACY') = 0 then Result := True;
end;

{ Silent uninstalls keep data unless /DELETEDATA is given. }
function DeleteDataRequested(): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/DELETEDATA') = 0 then Result := True;
end;

function SettingsPath(): String;
begin
  Result := DataDir('') + '\settings.json';
end;

{ Reads General.StartWithWindows from an existing settings.json. Returns True when the file exists and says false. }
function UserTurnedStartupOff(): Boolean;
var
  Text: AnsiString;
  Json, Tail: String;
  P: Integer;
begin
  Result := False;
  if not FileExists(SettingsPath()) then Exit;
  if not LoadStringFromFile(SettingsPath(), Text) then Exit;
  Json := String(Text);
  P := Pos('"StartWithWindows"', Json);
  if P = 0 then Exit;
  Tail := Copy(Json, P + Length('"StartWithWindows"'), 40);
  P := Pos('false', Lowercase(Tail));
  Result := (P > 0) and (P < Pos('true', Lowercase(Tail) + 'true'));
end;

{ The startup task is only offered when it would not override a recorded choice. }
function ShowStartupTask(): Boolean;
begin
  Result := not UserTurnedStartupOff();
end;

procedure StopRunningInstance();
var
  ResultCode: Integer;
  Exe: String;
begin
  Exe := ExpandConstant('{app}\{#AppExe}');
  if FileExists(Exe) then
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Give the old process a moment to release its files. }
  Sleep(1500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningInstance();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { If the user had startup off, make sure an old Run value from a previous version does not survive. }
    if UserTurnedStartupOff() then
      RegDeleteValue(HKEY_CURRENT_USER, RunKey, '{#AppName}');
    { A fresh install of an entry must not be hidden by a stale "disabled in Windows" flag from an older install. }
    if WizardIsTaskSelected('startup') then
      RegDeleteValue(HKEY_CURRENT_USER, ApprovedKey, '{#AppName}');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Data := DataDir('');
    if DirExists(Data) then
    begin
      { Default button is Yes (keep). Silent uninstalls keep data unless /DELETEDATA is given. }
      if (UninstallSilent() and not DeleteDataRequested()) or
         (not UninstallSilent() and (MsgBox('Keep your WorkBuddy settings and preferences?' + #13#10 + #13#10 +
                 'Choose Yes to keep them (recommended, so a reinstall picks up where you left off).' + #13#10 +
                 'Choose No to delete them: ' + Data,
                 mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES)) then
        { keep }
      else
        DelTree(Data, True, True, True);
    end;

    { Pre-rename data left behind by migration. Never removed unless the user explicitly says so. }
    Data := LegacyDir();
    if DirExists(Data) then
    begin
      if (UninstallSilent() and not DeleteLegacyRequested()) or
         (not UninstallSilent() and (MsgBox('Old FocusCatAI data was found:' + #13#10 + Data + #13#10 + #13#10 +
                 'It was copied into WorkBuddy when you upgraded. Keep this old copy?' + #13#10 + #13#10 +
                 'Choose Yes to keep it (recommended). Choose No to delete it.',
                 mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES)) then
        { keep }
      else
        DelTree(Data, True, True, True);
    end;
  end;
end;
