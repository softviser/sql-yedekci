; SQL Yedekçi — Inno Setup 6 kurulum betiği
; Derleme: build.ps1 tarafından çağrılır.
;   ISCC /DAppVersion=3.0.1 /DPublishDir=..\artifacts\publish installer\SqlYedekci.iss

#ifndef AppVersion
  #define AppVersion "3.0.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

#define AppName "SQL Yedekçi"
#define AppPublisher "Softviser Yazılım"
#define AppURL "https://softviser.com.tr"
#define AppRepo "https://github.com/softviser/sql-yedekci"
#define AppExe "SqlYedekci.exe"
#define TaskName "SQL Yedekci"

[Setup]
AppId={{3F8A2C71-5B9E-4D16-A0E4-7C2B9D4E1F58}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppRepo}/issues
AppUpdatesURL={#AppRepo}/releases
AppCopyright=© 2026 {#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Kurulum
DefaultDirName={autopf}\Softviser\SQL Yedekci
DefaultGroupName=Softviser\{#AppName}
DisableProgramGroupPage=yes
; Zamanlanmış görev SYSTEM hesabıyla Program Files'taki exe'yi çalıştırır; tüm kullanıcılar için kurulur.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputBaseFilename=SqlYedekci-{#AppVersion}-setup-win-x64
SetupIconFile=..\src\SqlYedekci\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
#ifdef CertFile
Source: "{#CertFile}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\trust-cert.ps1"; DestDir: "{app}"; Flags: ignoreversion
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Softviser Yazılım web sitesi"; Filename: "{#AppURL}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Kaldırınca zamanlanmış görev de silinir. Yedekler ve %ProgramData%\Softviser\SqlYedekci ayarları korunur.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /F /TN ""{#TaskName}"""; Flags: runhidden; RunOnceId: "DeleteTask"
