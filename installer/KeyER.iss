#define AppVersion "0.2.0"
#define AppName "KeyER"

[Setup]
AppId={{3D91559A-9B6B-4FE8-8568-258A81580A9E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=KeyER
DefaultDirName={localappdata}\Programs\KeyER
DefaultGroupName=KeyER
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=KeyER-v{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\KeyER.exe
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки"; Flags: unchecked

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KeyER"; Filename: "{app}\KeyER.exe"
Name: "{autodesktop}\KeyER"; Filename: "{app}\KeyER.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\KeyER.exe"; Description: "Запустить KeyER"; Flags: nowait postinstall skipifsilent
