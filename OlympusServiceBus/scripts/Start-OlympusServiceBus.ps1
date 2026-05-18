[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$StartupTimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$startupLogsRoot = Join-Path $repoRoot ".startup-logs"
$env:OLYMPUS_EVALUATION_VERBOSE = "true"

function Get-StartupLogTail {
    param(
        [string]$Path,
        [int]$LineCount = 40
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $lines = Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue | Select-Object -Last $LineCount
    if ($null -eq $lines -or $lines.Count -eq 0) {
        return $null
    }

    return ($lines -join [Environment]::NewLine)
}

function New-StartupFailureMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [System.Diagnostics.Process]$Process,
        [string]$ReadyUrl,
        [string]$StdOutPath,
        [string]$StdErrPath
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add("Process '$Name' exited before '$ReadyUrl' became available.")

    if ($null -ne $Process) {
        $parts.Add("ExitCode: $($Process.ExitCode)")
    }

    $stdoutTail = Get-StartupLogTail -Path $StdOutPath
    if (-not [string]::IsNullOrWhiteSpace($stdoutTail)) {
        $parts.Add("StdOut tail:")
        $parts.Add($stdoutTail)
    }

    $stderrTail = Get-StartupLogTail -Path $StdErrPath
    if (-not [string]::IsNullOrWhiteSpace($stderrTail)) {
        $parts.Add("StdErr tail:")
        $parts.Add($stderrTail)
    }

    return ($parts -join [Environment]::NewLine)
}

function Get-LaunchProfileApplicationUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRelativePath,

        [Parameter(Mandatory = $true)]
        [string]$ProfileName
    )

    $launchSettingsPath = Join-Path $repoRoot $ProjectRelativePath
    $launchSettingsPath = Join-Path $launchSettingsPath "Properties\launchSettings.json"

    $launchSettings = Get-Content $launchSettingsPath -Raw | ConvertFrom-Json
    $profile = $launchSettings.profiles.$ProfileName

    if ($null -eq $profile) {
        throw "Launch profile '$ProfileName' was not found in '$launchSettingsPath'."
    }

    $applicationUrl = [string]$profile.applicationUrl
    if ([string]::IsNullOrWhiteSpace($applicationUrl)) {
        throw "Launch profile '$ProfileName' in '$launchSettingsPath' does not define applicationUrl."
    }

    return ($applicationUrl -split ';' | Select-Object -First 1).Trim()
}

function Wait-ForTcpPort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,

        [string]$StdOutPath,
        [string]$StdErrPath
    )

    $uri = [System.Uri]$Url
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw (New-StartupFailureMessage `
                -Name $Name `
                -Process $Process `
                -ReadyUrl $Url `
                -StdOutPath $StdOutPath `
                -StdErrPath $StdErrPath)
        }

        $client = $null
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $connectTask = $client.ConnectAsync($uri.Host, $uri.Port)

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

    $timeoutMessage = "Timed out waiting for '$Name' on '$Url' after $TimeoutSeconds seconds."
    $stdoutTail = Get-StartupLogTail -Path $StdOutPath
    $stderrTail = Get-StartupLogTail -Path $StdErrPath

    if (-not [string]::IsNullOrWhiteSpace($stdoutTail)) {
        $timeoutMessage += [Environment]::NewLine + "StdOut tail:" + [Environment]::NewLine + $stdoutTail
    }

    if (-not [string]::IsNullOrWhiteSpace($stderrTail)) {
        $timeoutMessage += [Environment]::NewLine + "StdErr tail:" + [Environment]::NewLine + $stderrTail
    }

    throw $timeoutMessage
}

function Stop-ExistingDotNetProjectProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRelativePath
    )

    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ProjectRelativePath))
    $processName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectRelativePath)
    $processIdsToStop = @()

    $projectProcesses = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
    foreach ($process in $projectProcesses) {
        $processIdsToStop += $process.Id
    }

    $dotnetHosts = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.IndexOf($projectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        })

    foreach ($dotnetHost in $dotnetHosts) {
        $processIdsToStop += [int]$dotnetHost.ProcessId
    }

    $processIdsToStop = @($processIdsToStop | Sort-Object -Unique)
    if ($processIdsToStop.Count -eq 0) {
        return
    }

    Write-Host ("Stopping existing {0} process(es): {1}" -f $Name, ($processIdsToStop -join ", "))
    Stop-Process -Id $processIdsToStop -Force -ErrorAction Stop
    Start-Sleep -Seconds 1
}

function Start-DotNetProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRelativePath,

        [string]$LaunchProfile,
        [string]$ReadyUrl,

        [switch]$StopExisting,
        [switch]$HideWindow
    )

    $projectPath = Join-Path $repoRoot $ProjectRelativePath
    $argumentList = @(
        "run",
        "--project", $projectPath,
        "--configuration", $Configuration
    )

    if (-not [string]::IsNullOrWhiteSpace($LaunchProfile)) {
        $argumentList += @("--launch-profile", $LaunchProfile)
    }

    if ($StopExisting) {
        Stop-ExistingDotNetProjectProcesses -Name $Name -ProjectRelativePath $ProjectRelativePath
    }

    if (-not (Test-Path -LiteralPath $startupLogsRoot)) {
        New-Item -ItemType Directory -Path $startupLogsRoot | Out-Null
    }

    $safeName = ($Name -replace '[^A-Za-z0-9_.-]', '_')
    $logStamp = Get-Date -Format "yyyyMMdd-HHmmssfff"
    $stdoutPath = Join-Path $startupLogsRoot "$safeName-$logStamp.stdout.log"
    $stderrPath = Join-Path $startupLogsRoot "$safeName-$logStamp.stderr.log"

    Write-Host "Starting $Name..."
    $startInfo = @{
        FilePath = "dotnet"
        ArgumentList = $argumentList
        WorkingDirectory = $repoRoot
        PassThru = $true
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
    }

    if ($HideWindow) {
        $startInfo.WindowStyle = "Hidden"
    }

    $process = Start-Process @startInfo

    if (-not [string]::IsNullOrWhiteSpace($ReadyUrl)) {
        Write-Host "Waiting for $Name on $ReadyUrl..."
        Wait-ForTcpPort `
            -Name $Name `
            -Url $ReadyUrl `
            -Process $process `
            -TimeoutSeconds $StartupTimeoutSeconds `
            -StdOutPath $stdoutPath `
            -StdErrPath $stderrPath
    }
    else {
        Start-Sleep -Seconds 2

        if ($process.HasExited) {
            throw (New-StartupFailureMessage `
                -Name $Name `
                -Process $process `
                -ReadyUrl "<startup>" `
                -StdOutPath $stdoutPath `
                -StdErrPath $stderrPath)
        }
    }

    return [PSCustomObject]@{
        Name = $Name
        Process = $process
        StdOutPath = $stdoutPath
        StdErrPath = $stderrPath
    }
}

$mockEndpointsUrl = Get-LaunchProfileApplicationUrl -ProjectRelativePath "MockEndpoints" -ProfileName "http"
$webHostUrl = Get-LaunchProfileApplicationUrl -ProjectRelativePath "OlympusServiceBus.WebHost" -ProfileName "http"

$startedProcesses = @()

try {
    Stop-ExistingDotNetProjectProcesses `
        -Name "OlympusServiceBus.Engine" `
        -ProjectRelativePath "OlympusServiceBus.Engine\OlympusServiceBus.Engine.csproj"

    Stop-ExistingDotNetProjectProcesses `
        -Name "OlympusServiceBus.WebHost" `
        -ProjectRelativePath "OlympusServiceBus.WebHost\OlympusServiceBus.WebHost.csproj"

    Stop-ExistingDotNetProjectProcesses `
        -Name "MockEndpoints" `
        -ProjectRelativePath "MockEndpoints\MockEndpoints.csproj"

    $startedProcesses += Start-DotNetProject `
        -Name "MockEndpoints" `
        -ProjectRelativePath "MockEndpoints\MockEndpoints.csproj" `
        -LaunchProfile "http" `
        -ReadyUrl $mockEndpointsUrl `
        -StopExisting `
        -HideWindow

    $startedProcesses += Start-DotNetProject `
        -Name "OlympusServiceBus.WebHost" `
        -ProjectRelativePath "OlympusServiceBus.WebHost\OlympusServiceBus.WebHost.csproj" `
        -LaunchProfile "http" `
        -ReadyUrl $webHostUrl `
        -StopExisting `
        -HideWindow

    $startedProcesses += Start-DotNetProject `
        -Name "OlympusServiceBus.Engine" `
        -ProjectRelativePath "OlympusServiceBus.Engine\OlympusServiceBus.Engine.csproj" `
        -LaunchProfile "OlympusServiceBus.Engine" `
        -StopExisting `
        -HideWindow

    $startedProcesses += Start-DotNetProject `
        -Name "OlympusServiceBus.Application" `
        -ProjectRelativePath "OlympusServiceBus.Application\OlympusServiceBus.Application.csproj"

    Write-Host ""
    Write-Host "OlympusServiceBus stack started."
    $startedProcesses | ForEach-Object {
        Write-Host (" - {0} (PID {1})" -f $_.Name, $_.Process.Id)
    }
}
catch {
    Write-Error $_
    throw
}
