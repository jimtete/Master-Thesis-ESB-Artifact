[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$StartupTimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

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
            throw "Process '$($Process.ProcessName)' exited before '$Url' became available."
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

    throw "Timed out waiting for '$Url' after $TimeoutSeconds seconds."
}

function Start-DotNetProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRelativePath,

        [string]$LaunchProfile,

        [string]$ReadyUrl
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

    Write-Host "Starting $Name..."
    $process = Start-Process -FilePath "dotnet" -ArgumentList $argumentList -WorkingDirectory $repoRoot -PassThru

    if (-not [string]::IsNullOrWhiteSpace($ReadyUrl)) {
        Write-Host "Waiting for $Name on $ReadyUrl..."
        Wait-ForTcpPort -Url $ReadyUrl -Process $process -TimeoutSeconds $StartupTimeoutSeconds
    }
    else {
        Start-Sleep -Seconds 2

        if ($process.HasExited) {
            throw "Process '$Name' exited during startup."
        }
    }

    return [PSCustomObject]@{
        Name = $Name
        Process = $process
    }
}

$mockEndpointsUrl = Get-LaunchProfileApplicationUrl -ProjectRelativePath "MockEndpoints" -ProfileName "http"
$webHostUrl = Get-LaunchProfileApplicationUrl -ProjectRelativePath "OlympusServiceBus.WebHost" -ProfileName "http"

$startedProcesses = @()

try {
    $startedProcesses += Start-DotNetProject `
        -Name "MockEndpoints" `
        -ProjectRelativePath "MockEndpoints\MockEndpoints.csproj" `
        -LaunchProfile "http" `
        -ReadyUrl $mockEndpointsUrl

    $startedProcesses += Start-DotNetProject `
        -Name "OlympusServiceBus.WebHost" `
        -ProjectRelativePath "OlympusServiceBus.WebHost\OlympusServiceBus.WebHost.csproj" `
        -LaunchProfile "http" `
        -ReadyUrl $webHostUrl

    $startedProcesses += Start-DotNetProject `
        -Name "OlympusServiceBus.Engine" `
        -ProjectRelativePath "OlympusServiceBus.Engine\OlympusServiceBus.Engine.csproj" `
        -LaunchProfile "OlympusServiceBus.Engine"

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
