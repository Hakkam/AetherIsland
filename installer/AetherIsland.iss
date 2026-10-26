#define AppVersion "1.0.0"
#define PublishDir "..\release\win-x64"

[Setup]
AppId=AetherIsland.Desktop
AppName=Aether Island
AppVersion={#AppVersion}
AppMutex=AetherIsland.SingleInstance
SetupIconFile=..\src\AetherIsland.App\Assets\AetherIsland.ico
DefaultDirName={localappdata}\Programs\Aether Island
DefaultGroupName=Aether Island
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=..\release
OutputBaseFilename=AetherIsland-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\AetherIsland.exe
CloseApplications=yes
MinVersion=10.0.17763

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Aether Island"; Filename: "{app}\AetherIsland.exe"
Name: "{autodesktop}\Aether Island"; Filename: "{app}\AetherIsland.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\AetherIsland.exe"; Description: "Launch Aether Island"; Flags: nowait postinstall skipifsilent

; Remove the application's current-user autostart entry only if it points
; to this installation. Leave %LOCALAPPDATA%\AetherIsland user data intact.
[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RunValue: String;
  ExpectedValue: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AetherIsland', RunValue) then
  begin
    ExpectedValue := '"' + ExpandConstant('{app}\AetherIsland.exe') + '"';
    if CompareText(Trim(RunValue), ExpectedValue) = 0 then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AetherIsland');
  end;
end;
