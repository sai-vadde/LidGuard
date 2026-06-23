#define MyAppName "LidGuard"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppPublisherUrl
  #define AppPublisherUrl ""
#endif
#ifndef AppSourceDir
  #error AppSourceDir must be provided to the compiler.
#endif

[Setup]
AppId={{B56D7D5D-1C9E-4B8D-9AFB-B8A9E130AA41}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppName}
AppPublisherURL={#AppPublisherUrl}
AppSupportURL={#AppPublisherUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=LidGuard-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\LidGuard.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start LidGuard when I sign in"; Flags: checkedonce

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSourceDir}\LidGuard.exe"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: RunInstallCommandsAfterLidGuardInstall

[Icons]
Name: "{group}\LidGuard"; Filename: "{app}\LidGuard.exe"

[Run]
Filename: "{app}\LidGuard.exe"; Description: "Launch LidGuard"; Flags: nowait postinstall skipifsilent; Check: ShouldLaunchApplication

[Code]
var
  InstallCommandsSucceeded: Boolean;
  InstallCommandsRan: Boolean;

function ShouldLaunchApplication(): Boolean;
begin
  Result := InstallCommandsSucceeded;
end;

procedure FailRequiredSetup(const FriendlyMessage: string);
begin
  InstallCommandsSucceeded := False;
  RaiseException(FriendlyMessage);
end;

procedure ExecChecked(const FileName: string; const Parameters: string);
var
  ExitCode: Integer;
begin
  if not Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    RaiseException('Windows could not start ' + FileName + '.');
  end;

  if ExitCode <> 0 then
  begin
    RaiseException(FileName + ' failed with exit code ' + IntToStr(ExitCode) + '.');
  end;
end;

procedure ExecRequiredSetupStep(
  const FileName: string;
  const Parameters: string;
  const DefaultFriendlyMessage: string
);
var
  ExitCode: Integer;
  FriendlyMessage: string;
  ReportContents: AnsiString;
  ReportPath: string;
begin
  ReportPath := ExpandConstant('{tmp}\LidGuard-setup-report.txt');

  if FileExists(ReportPath) then
  begin
    DeleteFile(ReportPath);
  end;

  if not Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    FailRequiredSetup(DefaultFriendlyMessage);
  end;

  if ExitCode <> 0 then
  begin
    FriendlyMessage := DefaultFriendlyMessage;

    if LoadStringFromFile(ReportPath, ReportContents) and (Trim(ReportContents) <> '') then
    begin
      FriendlyMessage := Trim(ReportContents);
    end;

    FailRequiredSetup(FriendlyMessage);
  end;
end;

function GetInstallScopeArgument(): string;
begin
  if IsAdminInstallMode then
  begin
    Result := 'all-users';
  end
  else
  begin
    Result := 'current-user';
  end;
end;

procedure RunInstallCommands();
var
  ScopeArgument: string;
begin
  InstallCommandsSucceeded := False;
  ScopeArgument := GetInstallScopeArgument();

  ExecRequiredSetupStep(
    ExpandConstant('{app}\LidGuard.exe'),
    '--apply-install-power-policy ' + ScopeArgument + ' ' + AddQuotes(ExpandConstant('{tmp}\LidGuard-setup-report.txt')),
    'LidGuard could not change the Windows lid-close power settings to "Do nothing". Setup requires administrator permission for this mandatory step, so the installation has been stopped.'
  );

  if WizardIsTaskSelected('startup') then
  begin
    ExecRequiredSetupStep(
      ExpandConstant('{app}\LidGuard.exe'),
      '--register-startup ' + ScopeArgument + ' ' + AddQuotes(ExpandConstant('{tmp}\LidGuard-setup-report.txt')),
      'LidGuard could not register itself to start when you sign in. The installation has been stopped so the app is not left in a partial setup state.'
    );
  end;

  InstallCommandsSucceeded := True;
end;

procedure RunInstallCommandsAfterLidGuardInstall();
begin
  if InstallCommandsRan then
  begin
    exit;
  end;

  InstallCommandsRan := True;
  RunInstallCommands();
end;

procedure RunUninstallCommands();
var
  ExitCode: Integer;
  ScopeArgument: string;
begin
  ScopeArgument := GetInstallScopeArgument();

  Exec(
    ExpandConstant('{cmd}'),
    '/C taskkill /IM LidGuard.exe /F /T',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ExitCode
  );

  if FileExists(ExpandConstant('{app}\LidGuard.exe')) then
  begin
    ExecChecked(
      ExpandConstant('{app}\LidGuard.exe'),
      '--unregister-startup ' + ScopeArgument
    );
    ExecChecked(
      ExpandConstant('{app}\LidGuard.exe'),
      '--restore-install-power-policy ' + ScopeArgument
    );
  end;
end;

procedure InitializeWizard();
begin
  InstallCommandsSucceeded := False;
  InstallCommandsRan := False;
  WizardForm.SelectTasksLabel.Caption :=
    'LidGuard must change Windows lid-close behavior to "Do nothing" on this computer.' + #13#10 +
    'Administrator permission is required for this mandatory setup step.' + #13#10 + #13#10 +
    'Choose whether LidGuard should also start automatically when you sign in.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RunUninstallCommands();
  end;
end;
