#define MyAppName "GLEM"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
[Setup]
AppId={{B7BFE7E7-2EA0-4AC6-9EF0-6A4D2B9F1001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\GLEM
DefaultGroupName=GLEM
OutputBaseFilename=GLEM-{#MyAppVersion}-win-x64-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
Uninstallable=yes
SetupIconFile=..\src\GLEM.App\Assets\GLEM.ico
LicenseFile=package\LICENSE
[Files]
Source: "package\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\GLEM"; Filename: "{app}\GLEM.exe"
Name: "{autodesktop}\GLEM"; Filename: "{app}\GLEM.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
[Registry]
Root: HKCR; Subkey: ".glem"; ValueType: string; ValueName: ""; ValueData: "GLEMFile"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "GLEMFile"; ValueType: string; ValueName: ""; ValueData: "GLEM file"; Flags: uninsdeletekey
Root: HKCR; Subkey: "GLEMFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\GLEM.exe,0"
Root: HKCR; Subkey: "GLEMFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\GLEM.exe"" ""%1"""
