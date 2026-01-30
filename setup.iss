; ============================================
; Segment Application - Inno Setup Script
; ============================================
; This script creates a production-ready installer
; for the Segment WPF application.
;
; Prerequisites:
;   1. Run build_release.bat first
;   2. Install Inno Setup 6.x from https://jrsoftware.org/isinfo.php
;   3. Compile this script with Inno Setup Compiler
; ============================================

#define MyAppName "Segment"
#define MyAppVersion "1.0"
#define MyAppPublisher "Segment Team"
#define MyAppExeName "Segment.exe"
#define MyAppDescription "Smart Translation and Glossary Management Tool"

[Setup]
; Application Information
AppId={{A7B9C4D5-E6F7-48A9-B0C1-D2E3F4A5B6C7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/yourusername/segment
AppSupportURL=https://github.com/yourusername/segment/issues
AppUpdatesURL=https://github.com/yourusername/segment/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=

; Output Configuration
OutputDir=.
OutputBaseFilename=SegmentSetup_v{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Icons and Graphics
; SetupIconFile=segment.ico
; NOTE: Uncomment above line when you have an .ico file
; You can create one from your procedural icon or use an icon editor

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Versioning
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; Uninstall Display
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start {#MyAppName} with Windows (runs in system tray)"; GroupDescription: "Startup Options:"; Flags: checkedonce

[Files]
; Main executable (single-file self-contained)
Source: "Publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Include any additional configuration files if they exist
Source: "Publish\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Publish\*.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; NOTE: If you have runtime-specific plugins or native libraries that
; weren't bundled into the single file, include them here:
; Source: "Publish\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start with Windows (runs in system tray)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Option to launch the application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the application if it's running before uninstall
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillSegment"

[Code]
// Check if the application is running before installation
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  AppRunning: Boolean;
begin
  Result := True;
  
  // Check if Segment.exe is running
  if Exec('tasklist', '/FI "IMAGENAME eq {#MyAppExeName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      AppRunning := True;
      if MsgBox('Segment is currently running. Setup will close it before continuing.' + #13#10 + #13#10 + 'Continue with installation?', 
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        Exec('taskkill', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        Sleep(1000); // Wait for process to fully terminate
        Result := True;
      end
      else
        Result := False;
    end;
  end;
end;

// Ensure application is not running before uninstall
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  if MsgBox('This will uninstall Segment from your computer.' + #13#10 + #13#10 + 'Continue?', 
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    // Try to close the application gracefully
    Exec('taskkill', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
    Result := True;
  end
  else
    Result := False;
end;
