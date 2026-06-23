param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion = "1.0.0",
    [string]$AppPublisherUrl = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$publishRoot = Join-Path $repoRoot "publish\installer"
$appPublishDir = Join-Path $env:TEMP ("LidGuardInstallerApp-" + [guid]::NewGuid().ToString("N"))
$outputRoot = Join-Path $env:TEMP ("LidGuardInstallerBin-" + [guid]::NewGuid().ToString("N"))
$intermediateRoot = Join-Path $env:TEMP ("LidGuardInstallerObj-" + [guid]::NewGuid().ToString("N"))
$installerScript = Join-Path $scriptDir "LidGuard.iss"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appPublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $intermediateRoot | Out-Null

dotnet publish (Join-Path $repoRoot "src\LidGuard.Production\LidGuard.Production.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    "-p:BaseOutputPath=$outputRoot\\" `
    "-p:BaseIntermediateOutputPath=$intermediateRoot\\" `
    -o $appPublishDir

Get-ChildItem -Path $appPublishDir -Filter *.pdb -Recurse | Remove-Item -Force

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) }

$isccPath = $isccCandidates | Select-Object -First 1

if (-not $isccPath) {
    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 and rerun this script."
}

& $isccPath `
    "/DAppSourceDir=$appPublishDir" `
    "/DAppVersion=$AppVersion" `
    "/DAppPublisherUrl=$AppPublisherUrl" `
    "/O$publishRoot" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Remove-Item -LiteralPath $appPublishDir -Recurse -Force
Remove-Item -LiteralPath $outputRoot -Recurse -Force
Remove-Item -LiteralPath $intermediateRoot -Recurse -Force

Write-Host "Installer build completed:"
Write-Host (Join-Path $publishRoot "LidGuard-Setup.exe")
