#ifndef AppVersion
#define AppVersion "1.1.0"
#endif
[Setup]
AppId={{271467E2-D833-4AC7-A174-16DF879A728A}
AppName=Mochi Desktop Companion
AppVersion={#AppVersion}
AppPublisher=Kwan-desu
AppPublisherURL=https://github.com/Kwan-desu/mochi-desktop-companion
DefaultDirName={localappdata}\Programs\Mochi Desktop Companion
DefaultGroupName=Mochi Desktop Companion
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\release
OutputBaseFilename=Mochi-Desktop-Companion-{#AppVersion}-win-x64-Setup
SetupIconFile=..\assets\icon\MochiDuo.ico
UninstallDisplayIcon={app}\MochiDuo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,test-results.txt,live-test.txt,preview-*.png"

[Icons]
Name: "{group}\Mochi Desktop Companion"; Filename: "{app}\MochiDuo.exe"
Name: "{group}\Mochi Settings"; Filename: "{app}\MochiDuo.exe"; Parameters: "--settings"
Name: "{autodesktop}\Mochi Desktop Companion"; Filename: "{app}\MochiDuo.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\MochiDuo.exe"; Description: "Launch Mochi Desktop Companion"; Flags: nowait postinstall skipifsilent
