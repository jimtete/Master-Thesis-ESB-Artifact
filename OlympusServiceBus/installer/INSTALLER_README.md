# OlympusServiceBus Installer

## Prerequisites

To rebuild the installer from source, install:

- .NET 10 SDK
- Inno Setup 6 (`ISCC.exe`)
- Windows PowerShell 5.1 or PowerShell 7

The published installer is self-contained for `win-x64`, so the target machine does not need a separate .NET runtime.

## Publish The App Payload

From `OlympusServiceBus/` run:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Publish-OlympusServiceBus.ps1 -Configuration Release -Runtime win-x64
```

This publishes the required executable projects and stages the installer payload under:

```text
installer\artifacts\stage\Release\win-x64
```

## Build The Installer

From `OlympusServiceBus/` run:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Build-OlympusServiceBusInstaller.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0
```

If `ISCC.exe` is not on `PATH`, provide it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Build-OlympusServiceBusInstaller.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0 -InnoSetupCompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

The generated installer is written to:

```text
installer\artifacts\installer\Release\win-x64
```

## What Gets Installed

The installer packages:

- `OlympusServiceBus.Application`
- `OlympusServiceBus.Engine`
- `OlympusServiceBus.WebHost`
- `MockEndpoints`
- helper scripts for start/stop/reset
- repo example content from `Examples`
- demo seed contracts and demo data for local testing

Installed binaries go under the selected application folder, normally:

```text
C:\Program Files\OlympusServiceBus
```

User-writable runtime data is created under:

```text
%APPDATA%\OlympusServiceBus
%LOCALAPPDATA%\OlympusServiceBus
```

## Local Test Flow After Installation

1. Run the installer.
2. Launch `Start Demo Runtime` from the Start Menu.
3. Launch `OlympusServiceBus Configurator` from the Start Menu.
4. Confirm the contracts workspace points to `%APPDATA%\OlympusServiceBus\Contracts`.
5. Check that recurring demo output appears under `%APPDATA%\OlympusServiceBus\DemoData`.
6. Use `Reset Demo Data` if you want to restore the seeded demo workspace.

The seeded `FileToApi` and `FileToFile` contracts are installed in a disabled state so their sample input files are not consumed automatically on first startup.

For a quick WebHost test after starting the runtime:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5099/demo/guest-registration-file -ContentType "application/json" -Body (@{
    fullName = "Ada Lovelace"
    email = "ada@example.test"
    meetingDateTime = "2026-05-05T12:00:00Z"
    duration = 2
    registeredBy = "installer-demo"
} | ConvertTo-Json)
```
