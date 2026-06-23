# LidGuard

LidGuard helps laptop users keep working safely with the lid closed while an external monitor is connected, and recover cleanly when the external display disappears.

This repository is public so anyone can inspect the source, review the installer behavior, and build the app themselves. The Windows "Unknown Publisher" warning can still appear because the binaries are not code signed.

## What this repo contains

- `LidGuard/`: shared core logic for lid events, display detection, logging, and recovery behavior.
- `src/LidGuard.Production/`: the end-user Windows app.
- `src/LidGuard.Dev/`: a development host for testing and iteration.
- `installer/`: the Inno Setup installer and installer build script.

## Downloads

For non-technical users, the recommended download is the Inno Setup installer from **GitHub Releases**.

- Installer for normal users: `LidGuard-Setup.exe`
- Direct app build for technical users: `LidGuard.exe`

Do not use a GitHub folder page as the main download link. GitHub Releases is the right place for installer downloads because it gives users a stable release page and attached binaries.

## Why there are two app formats

- `LidGuard.exe` is for users who already know how to configure or troubleshoot Windows behavior themselves.
- `LidGuard-Setup.exe` is for regular users. It runs the required setup steps, including administrator-only actions such as install-time power policy changes and optional startup registration.

## Safety and transparency

The installer performs administrative setup because LidGuard needs to change lid-close behavior during installation. That behavior is implemented in the source code in this repository and the installer script is included under `installer/LidGuard.iss`.

Open source helps users inspect what the app does. It does **not** remove Microsoft's publisher warning by itself. That warning normally requires code signing.

## Build the app

Requirements:

- .NET 8 SDK
- Windows

Build the solution:

```powershell
dotnet build .\LidGuard.sln
```

Build the production app only:

```powershell
dotnet build .\src\LidGuard.Production\LidGuard.Production.csproj -c Release
```

## Build the installer

Requirements:

- .NET 8 SDK
- Inno Setup 6

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 -Configuration Release -Runtime win-x64 -AppVersion 1.0.0 -AppPublisherUrl https://github.com/your-account/your-repo
```

The generated installer is written to:

```text
publish\installer\LidGuard-Setup.exe
```

## Recommended GitHub release flow

1. Push the source code to GitHub.
2. Build `LidGuard-Setup.exe`.
3. Create a GitHub Release.
4. Upload `LidGuard-Setup.exe` as the main download asset.
5. Link users to the Release page instead of a repository folder.

## Project status

This project is intended to be shared freely for inspection and use. If code signing becomes available later, the same source repository can continue to be used while signed releases are published separately.
