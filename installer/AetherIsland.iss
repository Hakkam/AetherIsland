#define AppVersion "1.0.0"
#define PublishDir "..\release\win-x64"

[Setup]
AppId=AetherIsland.Desktop
AppName=Aether Island
AppVersion={#AppVersion}
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
