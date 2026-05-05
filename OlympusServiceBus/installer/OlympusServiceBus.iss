#define MyAppName "OlympusServiceBus"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef StageDir
  #error StageDir must be supplied to the Inno Setup compiler.
#endif

#ifndef InstallerOutputDir
  #error InstallerOutputDir must be supplied to the Inno Setup compiler.
#endif

[Setup]
AppId={{F76FE0CB-4D77-4B57-BE6D-F02F1988B2CB}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=jimtete
DefaultDirName={autopf}\OlympusServiceBus
DefaultGroupName=OlympusServiceBus
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma
SolidCompression=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=OlympusServiceBus-Setup-{#AppVersion}-win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Application\OlympusServiceBus.Application.exe

[Files]
Source: "{#StageDir}\Application\*"; DestDir: "{app}\Application"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\Engine\*"; DestDir: "{app}\Engine"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\WebHost\*"; DestDir: "{app}\WebHost"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\MockEndpoints\*"; DestDir: "{app}\MockEndpoints"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\Examples\*"; DestDir: "{app}\Examples"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\Scripts\*"; DestDir: "{app}\Scripts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\Seed\*"; DestDir: "{app}\Seed"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OlympusServiceBus\OlympusServiceBus Configurator"; Filename: "{app}\Application\OlympusServiceBus.Application.exe"
Name: "{autoprograms}\OlympusServiceBus\Start Demo Runtime"; Filename: "{app}\Scripts\Start-DemoRuntime.cmd"
Name: "{autoprograms}\OlympusServiceBus\Stop Demo Runtime"; Filename: "{app}\Scripts\Stop-DemoRuntime.cmd"
Name: "{autoprograms}\OlympusServiceBus\Reset Demo Data"; Filename: "{app}\Scripts\Reset-DemoData.cmd"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Scripts\Initialize-DemoWorkspace.ps1"""; Flags: runhidden waituntilterminated
Filename: "{app}\Application\OlympusServiceBus.Application.exe"; Description: "Launch OlympusServiceBus Configurator"; Flags: nowait postinstall skipifsilent unchecked
