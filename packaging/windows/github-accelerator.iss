#define MyAppName "GitHub Accelerator"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Kaixing"
#define MyAppExeName "github-accelerator.exe"

[Setup]
AppId={{B665A8EA-5F2B-4D6D-A10E-0F3AA7A8A100}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\GitHubAccelerator
DefaultGroupName=GitHub Accelerator
DisableProgramGroupPage=yes
OutputDir=..\..\dist
OutputBaseFilename=GitHubAccelerator-Setup-win-x64
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\..\dist\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\GitHub Accelerator"; Filename: "{app}\{#MyAppExeName}"; Parameters: "gui --listen 127.0.0.1:8999 --gui-listen 127.0.0.1:19010 --github-only true"
Name: "{autodesktop}\GitHub Accelerator"; Filename: "{app}\{#MyAppExeName}"; Parameters: "gui --listen 127.0.0.1:8999 --gui-listen 127.0.0.1:19010 --github-only true"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "gui --listen 127.0.0.1:8999 --gui-listen 127.0.0.1:19010 --github-only true"; Description: "启动 GitHub Accelerator"; Flags: nowait postinstall skipifsilent
