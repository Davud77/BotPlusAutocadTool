#ifndef BundleDir
  #error BundleDir must point to a complete BotPlusAutocadTool.bundle
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\artifacts\release"
#endif
#define ProjectDir ".."
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

[Setup]
AppId={{98BED5F2-3CAB-47DD-8344-E681FEA048DB}
AppName=BotPlusAutocadTool
AppVersion={#AppVersion}
AppPublisher=OOXGN
AppPublisherURL=https://github.com/Davud77/BotPlusAutocadTool
AppSupportURL=https://github.com/Davud77/BotPlusAutocadTool/issues
AppUpdatesURL=https://github.com/Davud77/BotPlusAutocadTool/releases
DefaultDirName={code:GetInstallDirectory}
DisableDirPage=yes
DisableWelcomePage=no
DisableProgramGroupPage=yes
UsePreviousAppDir=no
UninstallDisplayName=BotPlusAutocadTool (AutoCAD 2025–2027)
UninstallDisplayIcon={uninstallexe}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
LicenseFile={#ProjectDir}\LICENSE
InfoBeforeFile={#ProjectDir}\installer\BeforeInstall.txt
OutputDir={#ReleaseDir}
OutputBaseFilename=BotPlusAutocadTool-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
Type: files; Name: "{app}\Contents\Windows\2025-2026\*.dll"
Type: files; Name: "{app}\Contents\Windows\2025-2026\*.deps.json"
Type: files; Name: "{app}\Contents\Windows\2027\*.dll"
Type: files; Name: "{app}\Contents\Windows\2027\*.deps.json"

[Files]
Source: "{#BundleDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectDir}\README.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#ProjectDir}\LICENSE"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#ProjectDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#ProjectDir}\docs\VALIDATION.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion

[Code]
function GetInstallDirectory(Param: String): String;
begin
  if IsAdminInstallMode then
    Result := ExpandConstant('{commonpf}\Autodesk\ApplicationPlugins\BotPlusAutocadTool.bundle')
  else
    Result := ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\BotPlusAutocadTool.bundle');
end;

function AutoCADIsRunning(): Boolean;
var Locator, Service, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="acad.exe" OR Name="accoreconsole.exe"');
    Result := Processes.Count > 0;
  except
    RaiseException('Не удалось проверить, закрыт ли AutoCAD. / Cannot check whether AutoCAD is closed.');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if AutoCADIsRunning() then
    Result := 'Закройте AutoCAD и Core Console перед установкой или обновлением. / Close AutoCAD and Core Console before installing.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := not AutoCADIsRunning();
  if not Result then
    MsgBox('Закройте AutoCAD и Core Console перед удалением. / Close AutoCAD and Core Console before uninstalling.', mbError, MB_OK);
end;
