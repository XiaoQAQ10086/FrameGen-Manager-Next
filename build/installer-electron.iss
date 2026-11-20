#define AppName "FrameGen Manager-Next"
#define AppVersion "2.3.4"
#define AppPublisher "FrameGen"
#define AppExeName "FrameGen-Manager-Next.exe"

[Setup]
AppId={{5F2E4A97-6C1B-4C4E-9B7A-2F7D1C3A8E51}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer-electron\releases
OutputBaseFilename=FrameGenManagerNext-{#AppVersion}-win-Setup
SetupIconFile=..\ui-electron\renderer\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UseSetupLdr=x64
CloseApplications=yes
AppMutex=Local\FrameGenManagerNext.App
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Files]
Source: "..\ui-electron\dist\FrameGen-Manager-Next\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "userdata\*"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\userdata"
Type: filesandordirs; Name: "{app}\logs"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
