; Inno Setup script for the Windows installer.
; Built by .github/workflows/release.yml:  ISCC /DAppVersion=0.1.0 installer\DatacenterArchitect.iss
; Installs per user (no admin prompt), so the in-game updater can run it silently.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{8F3C2A51-6E1B-4D7A-9C55-3B1E2D4F7A90}
AppName=Datacenter Architect
AppVersion={#AppVersion}
AppVerName=Datacenter Architect {#AppVersion}
AppPublisher=Datacenter Architect
AppPublisherURL=https://github.com/rhisijmuiden/datacenter-architect
DefaultDirName={autopf}\Datacenter Architect
DefaultGroupName=Datacenter Architect
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=DatacenterArchitect-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\DatacenterArchitect.exe
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; remove the previous .NET data folder so old assemblies never linger after an update
Type: filesandordirs; Name: "{app}\data_DatacenterArchitect_windows_x86_64"

[Files]
Source: "..\build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Datacenter Architect"; Filename: "{app}\DatacenterArchitect.exe"
Name: "{autodesktop}\Datacenter Architect"; Filename: "{app}\DatacenterArchitect.exe"; Tasks: desktopicon

[Run]
; normal install: offer to start the game
Filename: "{app}\DatacenterArchitect.exe"; Description: "{cm:LaunchProgram,Datacenter Architect}"; Flags: nowait postinstall skipifsilent
; silent update started from the game: start the game again afterwards
Filename: "{app}\DatacenterArchitect.exe"; Flags: nowait; Check: WizardSilent
