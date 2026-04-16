; ============================================
;  Open Browser Inno Setup Script
; ============================================

#include "version.iss"

#define RootPath ".."
#define MyAppName "Auto Click"
#define MyAppExeName "FlowMy.exe"
#define MyAppCompany "Phạm Khương"
#define MyAppURL "https://www.example.com"

#define MainAppBuild RootPath + "\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{F8Y4E3D1-4C5C-4O0F-C4A2-8C1F1E2D1Q5A}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppCompany}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir={#RootPath}\Setup
OutputBaseFilename={#MyAppName}_v{#MyAppVersion}_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=*.exe
DiskSpanning=no
UsePreviousAppDir=yes
DisableWelcomePage=no
AllowNoIcons=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppCompany}
VersionInfoDescription={#MyAppName} Setup
VersionInfoTextVersion={#MyAppVersion}
UninstallDisplayIcon={app}{#MyAppExeName}
SetupIconFile={#RootPath}\Assets\Images\Auto_Click.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.WelcomeLabel1=Welcome to the %1 Setup Wizard
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%nIt is recommended that you close all other applications before continuing.
english.WizardReady=Ready to Install
english.ReadyLabel1=Setup is now ready to begin installing [name] on your computer.
english.ReadyLabel2a=Click Install to continue with the installation, or click Back if you want to review or change any settings.
english.FinishedHeadingLabel=%1 Setup Complete
english.FinishedLabelNoIcons=Setup has finished installing [name].
english.FinishedLabel=Setup has finished installing [name]. The application may be launched by selecting the installed icons.
english.ClickFinish=Click Finish to exit Setup.
english.BeveledLabel=Auto Click Application - v{#MyAppVersion}

[Files]
Source: "{#MainAppBuild}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Permissions: users-full

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconIndex: 0; Comment: "Launch Auto Click"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

; Desktop and Startup shortcuts
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconIndex: 0; Comment: "Launch Auto Click"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Flags: createonlyiffileexists

[Run]
; Hiển thị checkbox "Launch app" cho cả cài mới VÀ cập nhật
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\*.log"
Type: dirifempty; Name: "{app}\Resources"
Type: dirifempty; Name: "{app}"

[Messages]
BeveledLabel=Phần mềm tạo video veo3 v{#MyAppVersion}

[Code]
var
  IsUpgradeInstallation: Boolean;

// Kiểm tra xem đây có phải là upgrade hay không
function IsUpgrade: Boolean;
begin
  Result := IsUpgradeInstallation;
end;

procedure InitializeWizard;
begin
  WizardForm.PageNameLabel.Caption := 'Auto Click - v' + '{#MyAppVersion}';
  WizardForm.PageDescriptionLabel.Caption := 'Công cụ tạo video Veo3';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    WizardForm.FinishedHeadingLabel.Caption := 'Cài đặt hoàn tất!';
    
    if IsUpgradeInstallation then
    begin
      WizardForm.FinishedLabel.Caption := 'Ứng dụng Auto Click đã được cập nhật lên v{#MyAppVersion} thành công!' + #13#13 +
                                          'Chọn "Launch Auto Click" bên dưới để khởi chạy ứng dụng.' + #13#13 +
                                          'Nhấp Hoàn tất để đóng trình cài đặt.';
    end
    else
    begin
      WizardForm.FinishedLabel.Caption := 'Ứng dụng Auto Click v{#MyAppVersion} đã được cài đặt thành công.' + #13#13 +
                                          'Shortcut đã được tạo trên Desktop và Startup folder của bạn.' + #13#13 +
                                          'Nhấp Hoàn tất để đóng trình cài đặt.';
    end;
  end;
end;

// Kiểm tra xem app đã được cài đặt trước đó chưa
function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  Result := True;
  IsUpgradeInstallation := False;
  
  // Kiểm tra trong registry xem app đã được cài đặt chưa
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{F8Y4E3D1-4C5C-4O0F-C4A2-8C1F1E2D1Q5A}_is1', 'DisplayVersion', OldVersion) or
     RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{F8Y4E3D1-4C5C-4O0F-C4A2-8C1F1E2D1Q5A}_is1', 'DisplayVersion', OldVersion) then
  begin
    IsUpgradeInstallation := True;
    Log('Detected upgrade installation. Old version: ' + OldVersion + ', New version: {#MyAppVersion}');
  end
  else
  begin
    Log('Fresh installation detected');
  end;
end;

// Đảm bảo đóng ứng dụng cũ trước khi cài đặt
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Counter: Integer;
begin
  Result := '';
  
  // Thử đóng app đang chạy một cách nhẹ nhàng
  if IsUpgradeInstallation then
  begin
    Log('Attempting to close running application...');
    
    // Thử đóng bằng taskkill nhiều lần
    for Counter := 1 to 3 do
    begin
      Exec('taskkill', '/IM "' + '{#MyAppExeName}' + '" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(500);
    end;
    
    Sleep(1000); // Đợi thêm 1 giây để app đóng hoàn toàn
    Log('Application closed, ready to install');
  end;
end;