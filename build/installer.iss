#define AppName "FrameGen Manager-Next"
#define AppExeName "FrameGenManager-Next.exe"

[Setup]
AppId={{B7E7F3B6-6C3E-4F7A-9E0B-6F6B2C6D0D61}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=FrameGen Manager-Next
AppPublisherURL=https://github.com/XiaoQAQ10086/FrameGen-Manager-Next
AppSupportURL=https://github.com/XiaoQAQ10086/FrameGen-Manager-Next
AppUpdatesURL=https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/releases
DefaultDirName={localappdata}\Programs\FrameGen Manager-Next
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=FrameGenManagerNext-{#AppVersion}-win-Setup
SetupIconFile=..\src\FrameGenManagerNext.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#ifdef LEGACY_INNO
; Inno 6.4 has no 64-bit loader or RedirectionGuard directive.
#else
UseSetupLdr=x64
RedirectionGuard=no
#endif
SetupLogging=yes
AppMutex=Local\FrameGenManagerNext.App
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; The app requires administrator rights, but this installer is per-user
; (PrivilegesRequired=lowest). ShellExecuteEx is needed to raise the UAC
; prompt; CreateProcess fails with error 740.
Filename: "{app}\{#AppExeName}"; Flags: nowait shellexec

[UninstallRun]
Filename: "{app}\{#AppExeName}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated shellexec; RunOnceId: "CleanupUserData"

[UninstallDelete]
Type: files; Name: "{userdesktop}\FrameGen Manager-Next.lnk"
Type: files; Name: "{userprograms}\FrameGen Manager-Next.lnk"
Type: filesandordirs; Name: "{localappdata}\FrameGen-Manager-Next"
Type: filesandordirs; Name: "{localappdata}\FrameGenNext"
Type: filesandordirs; Name: "{tmp}\FrameGen-Manager-Next"

[Code]
procedure CleanupPreviousFiles;
var
  ManifestPath: String;
begin
  ManifestPath := AddBackslash(WizardDirValue) + 'install-manifest.txt';
  if FileExists(ManifestPath) then
    DelTree(WizardDirValue, True, True, True);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  OldDir: String;
begin
  if CurStep = ssInstall then
  begin
    OldDir := ExpandConstant('{localappdata}\FrameGenManagerNext');
    if FileExists(AddBackslash(OldDir) + 'Update.exe') then
    begin
      DelTree(OldDir, True, True, True);
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\FrameGenManagerNext');
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Start\TileProperties\W~velopack.FrameGenManagerNext');
      DeleteFile(ExpandConstant('{userdesktop}\FrameGen Manager-Next.lnk'));
      DeleteFile(ExpandConstant('{userprograms}\FrameGen Manager-Next.lnk'));
    end;
    CleanupPreviousFiles;
  end;
end;

[CustomMessages]
english.CreateDesktopShortcut=Create a desktop shortcut
chinesesimplified.CreateDesktopShortcut=创建桌面快捷方式
english.AdditionalShortcuts=Additional shortcuts:
chinesesimplified.AdditionalShortcuts=附加快捷方式:
