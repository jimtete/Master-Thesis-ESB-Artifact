[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptsRoot = $PSScriptRoot
$installRoot = Split-Path -Parent $scriptsRoot
$localAppDataRoot = Join-Path $env:LOCALAPPDATA "OlympusServiceBus"
$logsRoot = Join-Path $localAppDataRoot "Logs"
$pidFilePath = Join-Path $logsRoot "runtime-processes.json"
$initializeScriptPath = Join-Path $scriptsRoot "Initialize-DemoWorkspace.ps1"
$stopScriptPath = Join-Path $scriptsRoot "Stop-DemoRuntime.ps1"

function Wait-ForTcpPort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$HostName,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "Process '$($Process.ProcessName)' exited before port $Port became available."
        }

        $client = $null
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $connectTask = $client.ConnectAsync($HostName, $Port)

            if ($connectTask.Wait(1000) -and $client.Connected) {
                return
            }
        }
        catch {
        }
        finally {
            if ($null -ne $client) {
                $client.Dispose()
            }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for port $Port."
}

function Start-HostedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [string[]]$ArgumentList = @(),

        [int]$ReadyPort = 0
    )

    $stdoutPath = Join-Path $logsRoot "$Name.stdout.log"
    $stderrPath = Join-Path $logsRoot "$Name.stderr.log"

    if (Test-Path $stdoutPath) {
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $stderrPath) {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }

    $startInfo = @{
        FilePath = $ExecutablePath
        WorkingDirectory = $WorkingDirectory
        PassThru = $true
        WindowStyle = "Hidden"
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
    }

    if ($ArgumentList.Count -gt 0) {
        $startInfo.ArgumentList = $ArgumentList
    }

    $process = Start-Process @startInfo

    if ($ReadyPort -gt 0) {
        Wait-ForTcpPort -HostName "127.0.0.1" -Port $ReadyPort -Process $process -TimeoutSeconds $StartupTimeoutSeconds
    }
    else {
        Start-Sleep -Seconds 2
        if ($process.HasExited) {
            throw "Process '$Name' exited during startup."
        }
    }

    return [PSCustomObject]@{
        Name = $Name
        ProcessId = $process.Id
        ExecutablePath = $ExecutablePath
        WorkingDirectory = $WorkingDirectory
        StdOut = $stdoutPath
        StdErr = $stderrPath
    }
}

New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null

& $stopScriptPath -Quiet
Start-Sleep -Seconds 1
& $initializeScriptPath

$startedProcesses = @()

try {
    $startedProcesses += Start-HostedProcess `
        -Name "MockEndpoints" `
        -ExecutablePath (Join-Path $installRoot "MockEndpoints\MockEndpoints.exe") `
        -WorkingDirectory (Join-Path $installRoot "MockEndpoints") `
        -ArgumentList @("--urls", "http://localhost:5146") `
        -ReadyPort 5146

    $startedProcesses += Start-HostedProcess `
        -Name "OlympusServiceBus.WebHost" `
        -ExecutablePath (Join-Path $installRoot "WebHost\OlympusServiceBus.WebHost.exe") `
        -WorkingDirectory (Join-Path $installRoot "WebHost") `
        -ArgumentList @("--urls", "http://localhost:5099") `
        -ReadyPort 5099

    $startedProcesses += Start-HostedProcess `
        -Name "OlympusServiceBus.Engine" `
        -ExecutablePath (Join-Path $installRoot "Engine\OlympusServiceBus.Engine.exe") `
        -WorkingDirectory (Join-Path $installRoot "Engine")

    $startedProcesses | ConvertTo-Json | Set-Content -LiteralPath $pidFilePath -Encoding UTF8

    Write-Host "OlympusServiceBus demo runtime started."
    $startedProcesses | ForEach-Object {
        Write-Host (" - {0} (PID {1})" -f $_.Name, $_.ProcessId)
    }
}
catch {
    & $stopScriptPath -Quiet
    throw
}
