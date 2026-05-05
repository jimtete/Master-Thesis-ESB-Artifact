[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SelfContained = $true,

    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$installerRoot = Split-Path -Parent $PSCommandPath
$solutionRoot = Split-Path -Parent $installerRoot
$stageRoot = Join-Path $installerRoot "artifacts\stage\$Configuration\$Runtime"

if ($Clean -and (Test-Path $stageRoot)) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

function Publish-Project {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRelativePath,

        [Parameter(Mandatory = $true)]
        [string]$OutputFolderName,

        [string[]]$AdditionalArguments = @()
    )

    $projectPath = Join-Path $solutionRoot $ProjectRelativePath
    $outputPath = Join-Path $stageRoot $OutputFolderName

    if (Test-Path $outputPath) {
        Remove-Item -LiteralPath $outputPath -Recurse -Force
    }

    $publishArguments = @(
        "publish",
        $projectPath,
        "--configuration", $Configuration,
        "--runtime", $Runtime,
        "--output", $outputPath,
        "/p:SelfContained=$($SelfContained.IsPresent.ToString().ToLowerInvariant())",
        "/p:PublishSingleFile=false",
        "/p:PublishTrimmed=false"
    )

    if ($AdditionalArguments.Count -gt 0) {
        $publishArguments += $AdditionalArguments
    }

    Write-Host "Publishing $ProjectRelativePath -> $outputPath"
    & dotnet @publishArguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$ProjectRelativePath'."
    }

    $projectDirectory = Split-Path -Parent $projectPath
    Get-ChildItem -Path $projectDirectory -Filter "appsettings*.json" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $outputPath $_.Name) -Force
    }
}

function Copy-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRelativePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationFolderName
    )

    $sourcePath = Join-Path $solutionRoot $SourceRelativePath
    $destinationPath = Join-Path $stageRoot $DestinationFolderName

    if (Test-Path $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    Copy-Item -Path (Join-Path $sourcePath '*') -Destination $destinationPath -Recurse -Force
}

Publish-Project -ProjectRelativePath "OlympusServiceBus.Application\OlympusServiceBus.Application.csproj" -OutputFolderName "Application"
Publish-Project -ProjectRelativePath "OlympusServiceBus.Engine\OlympusServiceBus.Engine.csproj" -OutputFolderName "Engine"
Publish-Project -ProjectRelativePath "OlympusServiceBus.WebHost\OlympusServiceBus.WebHost.csproj" -OutputFolderName "WebHost" -AdditionalArguments @("/p:ErrorOnDuplicatePublishOutputFiles=false")
Publish-Project -ProjectRelativePath "MockEndpoints\MockEndpoints.csproj" -OutputFolderName "MockEndpoints"

Copy-Directory -SourceRelativePath "Examples" -DestinationFolderName "Examples"
Copy-Directory -SourceRelativePath "installer\assets\scripts" -DestinationFolderName "Scripts"
Copy-Directory -SourceRelativePath "installer\assets\seed" -DestinationFolderName "Seed"

Write-Host ""
Write-Host "Installer staging payload created at:"
Write-Host $stageRoot
