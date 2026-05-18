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
$projectRelativePath = "OlympusServiceBus.WebHost\OlympusServiceBus.WebHost.csproj"
$projectPath = Join-Path $repoRoot $projectRelativePath
$projectDirectory = Split-Path -Parent $projectPath
$launchSettingsPath = Join-Path $repoRoot "OlympusServiceBus.WebHost\Properties\launchSettings.json"

if (-not (Test-Path -LiteralPath $startupLogsRoot)) {
    New-Item -ItemType Directory -Path $startupLogsRoot | Out-Null
}

$logStamp = Get-Date -Format "yyyyMMdd-HHmmssfff"
$helperLogPath = Join-Path $startupLogsRoot "OlympusServiceBus.WebHost-Restart-$logStamp.helper.log"

function Write-HelperLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $timestamp = Get-Date -Format "o"
    Add-Content -LiteralPath $helperLogPath -Value "[$timestamp] $Message"
}

function Get-LaunchProfileApplicationUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ProfileName
    )

    $launchSettings = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $profile = $launchSettings.profiles.$ProfileName

    if ($null -eq $profile) {
        throw "Launch profile '$ProfileName' was not found in '$Path'."
    }

    $applicationUrl = [string]$profile.applicationUrl
    if ([string]::IsNullOrWhiteSpace($applicationUrl)) {
        throw "Launch profile '$ProfileName' in '$Path' does not define applicationUrl."
    }

    return ($applicationUrl -split ';' | Select-Object -First 1).Trim()
}

function Resolve-WebHostLaunchCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$ReadyUrl
    )

    $outputDirectory = Join-Path $ProjectDirectory ("bin\{0}\net10.0" -f $Configuration)
    $appHostPath = Join-Path $outputDirectory "OlympusServiceBus.WebHost.exe"
    if (Test-Path -LiteralPath $appHostPath) {
        return [PSCustomObject]@{
            FilePath = $appHostPath
            ArgumentList = @("--urls", $ReadyUrl)
            WorkingDirectory = $outputDirectory
        }
    }

    $assemblyPath = Join-Path $outputDirectory "OlympusServiceBus.WebHost.dll"
    if (Test-Path -LiteralPath $assemblyPath) {
        return [PSCustomObject]@{
            FilePath = "dotnet"
            ArgumentList = @($assemblyPath, "--urls", $ReadyUrl)
            WorkingDirectory = $outputDirectory
        }
    }

    throw "Could not find a built WebHost executable or assembly under '$outputDirectory'."
}

function Stop-ExistingDotNetProjectProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $processName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    $processIdsToStop = @()

    $projectProcesses = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
    foreach ($process in $projectProcesses) {
        $processIdsToStop += $process.Id
    }

    $dotnetHosts = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.IndexOf($ProjectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        })

    foreach ($dotnetHost in $dotnetHosts) {
        $processIdsToStop += [int]$dotnetHost.ProcessId
    }

    $processIdsToStop = @($processIdsToStop | Sort-Object -Unique)
    if ($processIdsToStop.Count -eq 0) {
        return
    }

    Write-HelperLog ("Stopping existing WebHost process(es): {0}" -f ($processIdsToStop -join ", "))
    Stop-Process -Id $processIdsToStop -Force -ErrorAction Stop
}

function Wait-ForTcpPort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $uri = [System.Uri]$Url
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "WebHost exited before '$Url' became available. ExitCode: $($Process.ExitCode)"
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

    throw "Timed out waiting for WebHost on '$Url' after $TimeoutSeconds seconds."
}

try {
    $readyUrl = Get-LaunchProfileApplicationUrl -Path $launchSettingsPath -ProfileName "http"
    $stdoutPath = Join-Path $startupLogsRoot "OlympusServiceBus.WebHost-$logStamp.stdout.log"
    $stderrPath = Join-Path $startupLogsRoot "OlympusServiceBus.WebHost-$logStamp.stderr.log"
    $launchCommand = Resolve-WebHostLaunchCommand -ProjectDirectory $projectDirectory -Configuration $Configuration -ReadyUrl $readyUrl

    Write-HelperLog "Restart requested. Waiting for the current WebHost to exit."
    Start-Sleep -Seconds 2

    Stop-ExistingDotNetProjectProcesses -ProjectPath $projectPath

    Write-HelperLog ("Starting WebHost with arguments: {0} {1}" -f $launchCommand.FilePath, ($launchCommand.ArgumentList -join " "))
    $process = Start-Process `
        -FilePath $launchCommand.FilePath `
        -ArgumentList $launchCommand.ArgumentList `
        -WorkingDirectory $launchCommand.WorkingDirectory `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden

    Wait-ForTcpPort -Url $readyUrl -Process $process -TimeoutSeconds $StartupTimeoutSeconds
    Write-HelperLog ("WebHost restarted successfully. PID: {0}" -f $process.Id)
}
catch {
    Write-HelperLog ("Restart failed: {0}" -f $_)
    throw
}
