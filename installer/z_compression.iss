#define MyAppName "z_compression"
#define MyAppVersion "1.2.1"
#define MyAppPublisher "z_compression contributors"
#define MyAppExeName "z_compression.exe"

[Setup]
AppId={{965813FC-D8E6-46B1-9361-54C98A75B723}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\z_compression
DefaultGroupName=z_compression
OutputDir=..\artifacts\installer
OutputBaseFilename=z_compression-v{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\ZCompression.App\Assets\z_compression.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로가기 만들기"; GroupDescription: "추가 바로가기:"
Name: "defaultapps"; Description: "설치 후 z_compression을 기본 앱으로 선택"; GroupDescription: "파일 연결:"; Flags: checkedonce

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\z_compression"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\z_compression"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\ZCompression.Zip"; ValueType: string; ValueData: "z_compression ZIP archive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ZCompression.Zip\DefaultIcon"; ValueType: string; ValueData: """{app}\Assets\FileTypes\zip.ico"",0"
Root: HKCU; Subkey: "Software\Classes\ZCompression.Zip\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "ZCompression.Zip"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\ZCompression.SevenZip"; ValueType: string; ValueData: "z_compression 7Z archive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ZCompression.SevenZip\DefaultIcon"; ValueType: string; ValueData: """{app}\Assets\FileTypes\7z.ico"",0"
Root: HKCU; Subkey: "Software\Classes\ZCompression.SevenZip\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "ZCompression.SevenZip"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\ZCompression.Rar"; ValueType: string; ValueData: "z_compression RAR archive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ZCompression.Rar\DefaultIcon"; ValueType: string; ValueData: """{app}\Assets\FileTypes\rar.ico"",0"
Root: HKCU; Subkey: "Software\Classes\ZCompression.Rar\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "ZCompression.Rar"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\ZCompression.Archive"; ValueType: string; ValueData: "z_compression archive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ZCompression.Archive\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\ZCompression.Archive\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".zip"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".7z"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".rar"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tar"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".gz"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tgz"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".bz2"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".xz"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".zst"; ValueData: ""
Root: HKCU; Subkey: "Software\ZCompression\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\ZCompression\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "빠르고 안전한 Windows 압축 및 압축 해제 프로그램"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zip"; ValueData: "ZCompression.Zip"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".7z"; ValueData: "ZCompression.SevenZip"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rar"; ValueData: "ZCompression.Rar"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tar"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gz"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tgz"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bz2"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".xz"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\ZCompression\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zst"; ValueData: "ZCompression.Archive"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "z_compression"; ValueData: "Software\ZCompression\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\*\shell\ZCompression.Compress"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 바로 압축"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\ZCompression.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\*\shell\ZCompression.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\*\shell\ZCompression.Compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ZCompression.Compress"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 바로 압축"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ZCompression.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ZCompression.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ZCompression.Compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress-here ""%1"""

Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zst\shell\ZCompression.Extract"; ValueType: string; ValueName: "MUIVerb"; ValueData: "z_compression으로 폴더에 풀기"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zst\shell\ZCompression.Extract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zst\shell\ZCompression.Extract"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zst\shell\ZCompression.Extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-shell"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "Launch z_compression"; Flags: nowait postinstall skipifsilent
Filename: "ms-settings:defaultapps?registeredAppUser=z_compression"; Description: "z_compression 기본 앱 선택"; Flags: shellexec postinstall skipifsilent; Tasks: defaultapps
