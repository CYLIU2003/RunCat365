#define MyAppName     "RunCat 365"
#define MyAppVersion  "3.6.0"
#define MyAppExeName  "RunCat 365.exe"
#define MyAppID       "B7E4C891-3A12-4F9D-B2C8-5D91E4A7F630"
#define SrcDir        "..\RunCat365\bin\portable-win-x64"
#define IconFile      "..\RunCat365\resources\app_icon.ico"

[Setup]
AppId={{{#MyAppID}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Studio Kyome (Custom Build)
AppPublisherURL=https://github.com/Kyome22/RunCat365
DefaultDirName={localappdata}\Programs\RunCat365
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=RunCat365-{#MyAppVersion}-setup
SetupIconFile={#IconFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoDescription={#MyAppName} インストーラー

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "startup"; Description: "Windows 起動時に自動的に起動する"; GroupDescription: "追加タスク:"; Flags: unchecked

[Files]
Source: "{#SrcDir}\RunCat 365.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\RunCat 365.dll.config"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppName}"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{#MyAppName} を起動する"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM ""{#MyAppExeName}"""; \
  Flags: runhidden; RunOnceId: "KillBeforeUninstall"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec(ExpandConstant('{sys}\taskkill.exe'),
         '/F /IM "RunCat 365.exe"',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
