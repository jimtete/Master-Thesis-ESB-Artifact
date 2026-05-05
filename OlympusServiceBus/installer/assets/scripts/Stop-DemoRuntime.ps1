[CmdletBinding()]
param(
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptsRoot = $PSScriptRoot
$installRoot = Split-Path -Parent $scriptsRoot
$localAppDataRoot = Join-Path $env:LOCALAPPDATA "OlympusServiceBus"
$logsRoot = Join-Path $localAppDataRoot "Logs"
$pidFilePath = Join-Path $logsRoot "runtime-processes.json"

function Stop-ProcessByIdIfRunning {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        Stop-Process -Id $ProcessId -Force
    }
}

function Stop-KnownInstallProcesses {
    $knownNames = @(
        "MockEndpoints.exe",
        "OlympusServiceBus.WebHost.exe",
        "OlympusServiceBus.Engine.exe"
    )

    $processes = Get-CimInstance Win32_Process | Where-Object {
        $knownNames -contains $_.Name -and
        -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
        $_.ExecutablePath.StartsWith($installRoot, [System.StringComparison]::OrdinalIgnoreCase)
    }

    foreach ($process in $processes) {
        try {
            Stop-ProcessByIdIfRunning -ProcessId $process.ProcessId
        }
        catch {
            if (-not $Quiet) {
                Write-Warning "Failed to stop PID $($process.ProcessId): $($_.Exception.Message)"
            }
        }
    }
}

if (-not (Test-Path $pidFilePath)) {
    Stop-KnownInstallProcesses

    return
}

$processEntries = Get-Content $pidFilePath -Raw | ConvertFrom-Json

foreach ($entry in @($processEntries)) {
    try {
        Stop-ProcessByIdIfRunning -ProcessId $entry.ProcessId
    }
    catch {
        if (-not $Quiet) {
            Write-Warning "Failed to stop PID $($entry.ProcessId): $($_.Exception.Message)"
        }
    }
}

Stop-KnownInstallProcesses
if (Test-Path $pidFilePath) {
    Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
}

if (-not $Quiet) {
    Write-Host "OlympusServiceBus demo runtime stopped."
}
