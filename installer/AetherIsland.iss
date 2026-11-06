; AetherIsland — per-user Windows installer (Inno Setup 6)
; Builds release/AetherIsland-Setup-win-x64.exe from the self-contained
; publish output. No admin rights, no runtime prerequisites.

#define AppName "Aether Island"
#define AppExe "AetherIsland.exe"
#define AppVersion GetEnv("AETHER_VERSION")

[Setup]
AppId={{8A2E14D2-6F3B-4E7A-9C51-B71F0A2E4A01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=AetherIsland
DefaultDirName={localappdata}\Programs\AetherIsland
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=AetherIsland-Setup-win-x64
SetupIconFile=..\src\AetherIsland.Portable\Assets\AetherIsland.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\release\portable-v2\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Runtime leftovers only; the Data folder is handled by the uninstall code so
; user data can be preserved unless explicitly confirmed.
Type: files; Name: "{app}\*.log"

[Code]
const
  AppMutexName = 'AetherIsland.Portable.';

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Close a running instance before installing/upgrading so files can be replaced.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  WaitCount: Integer;
  AskClose: Integer;
begin
  Result := '';
  if CheckForMutexes(AppMutexName + '*') then
  begin
    AskClose := MsgBox('Aether Island is running. Close it now to continue the installation?', mbConfirmation, MB_OKCANCEL);
    if AskClose = IDCANCEL then
    begin
      Result := 'Installation cancelled while Aether Island was running.';
      Exit;
    end;
    for WaitCount := 0 to 20 do
    begin
      if not CheckForMutexes(AppMutexName + '*') then Break;
      Sleep(500);
    end;
  end;
end;

// Offer explicit, clearly described removal of user data on uninstall.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  Msg: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\Data');
    if DirExists(DataDir) then
    begin
      Msg := 'Delete your Aether Island data as well (notes, tasks, settings, screen-time history, credentials)?' #13#10 'Choose No to keep the Data folder inside the installation directory.';
      if MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
    // The app owns this optional startup entry; remove it with the program.
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'AetherIsland');
  end;
end;
