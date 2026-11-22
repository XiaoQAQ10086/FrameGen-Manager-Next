#define AppName "FrameGen Manager-Next"
#define AppVersion "2.3.6"
#define AppPublisher "FrameGen"
#define AppExeName "FrameGen-Manager-Next.exe"

[Setup]
AppId={{B7E7F3B6-6C3E-4F7A-9E0B-6F6B2C6D0D61}
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
Source: "..\ui-electron\dist\FrameGen-Manager-Next\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "userdata\*,portable.flag"

; The C# 2.x build used a different executable name and .NET payload.
; Remove those obsolete files on upgrade; never touch data/ or userdata/.
[InstallDelete]
Type: files; Name: "{app}\portable.flag"
Type: files; Name: "{app}\FrameGenManager-Next.exe"
Type: files; Name: "{app}\FrameGenManager-Next.pdb"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.deps.json"
Type: files; Name: "{app}\*.runtimeconfig.json"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\WebAssets"
Type: filesandordirs; Name: "{app}\Assets"
Type: filesandordirs; Name: "{app}\runtimes"

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
; Silent updates must reopen the app after the installer finishes.
Filename: "{app}\{#AppExeName}"; Flags: nowait; Check: WizardSilent
