[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$installRoot = Split-Path -Parent $PSScriptRoot
$seedRoot = Join-Path $installRoot "Seed"
$appDataRoot = Join-Path $env:APPDATA "OlympusServiceBus"
$localAppDataRoot = Join-Path $env:LOCALAPPDATA "OlympusServiceBus"
$contractsRoot = Join-Path $appDataRoot "Contracts"
$demoDataRoot = Join-Path $appDataRoot "DemoData"
$requestSamplesRoot = Join-Path $appDataRoot "RequestSamples"
$examplesCopyRoot = Join-Path $appDataRoot "Examples"
$logsRoot = Join-Path $localAppDataRoot "Logs"
$configRoot = Join-Path $localAppDataRoot "Configurator"
$configFilePath = Join-Path $configRoot "appsettings.json"
$runtimeStateDbPath = Join-Path $appDataRoot "runtime-state.db"
$templateRoot = Join-Path $seedRoot "Contracts"
$demoDataSeedRoot = Join-Path $seedRoot "DemoData"
$requestSamplesSeedRoot = Join-Path $seedRoot "RequestSamples"
$installedExamplesRoot = Join-Path $installRoot "Examples"

function Remove-IfPresent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-SeedTree {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path $Source)) {
        return
    }

    Ensure-Directory -Path $Destination
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

function Convert-ToContractPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ($Path -replace '\\', '/')
}

function Write-TemplateContracts {
    $replacements = @{
        "__MOCK_ENDPOINTS_BASE_URL__" = "http://localhost:5146"
        "__WEB_HOST_BASE_URL__" = "http://localhost:5099"
        "__API_TO_FILE_OUTPUT_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "ApiToFile\output"))
        "__PORT_TO_FILE_OUTPUT_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "PortToFile\output"))
        "__FILE_TO_API_INPUT_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToApi\input"))
        "__FILE_TO_API_PROCESSED_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToApi\processed"))
        "__FILE_TO_API_ERROR_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToApi\error"))
        "__FILE_TO_FILE_INPUT_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToFile\input"))
        "__FILE_TO_FILE_PROCESSED_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToFile\processed"))
        "__FILE_TO_FILE_ERROR_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToFile\error"))
        "__FILE_TO_FILE_OUTPUT_DIRECTORY__" = (Convert-ToContractPath (Join-Path $demoDataRoot "FileToFile\output"))
    }

    foreach ($template in Get-ChildItem -Path $templateRoot -Recurse -Filter *.json.template) {
        $relativePath = $template.FullName.Substring($templateRoot.Length + 1)
        $targetRelativePath = $relativePath.Substring(0, $relativePath.Length - ".template".Length)
        $destinationPath = Join-Path $contractsRoot $targetRelativePath

        Ensure-Directory -Path (Split-Path -Parent $destinationPath)

        if ((-not $Force) -and (Test-Path $destinationPath)) {
            continue
        }

        $content = Get-Content $template.FullName -Raw
        foreach ($key in $replacements.Keys) {
            $content = $content.Replace($key, $replacements[$key])
        }

        Set-Content -LiteralPath $destinationPath -Value $content -Encoding UTF8
    }
}

if ($Force) {
    Remove-IfPresent -Path $contractsRoot
    Remove-IfPresent -Path $demoDataRoot
    Remove-IfPresent -Path $requestSamplesRoot
    Remove-IfPresent -Path $examplesCopyRoot
    Remove-IfPresent -Path $configFilePath

    if (Test-Path $runtimeStateDbPath) {
        Remove-Item -LiteralPath $runtimeStateDbPath -Force
    }
}

Ensure-Directory -Path $appDataRoot
Ensure-Directory -Path $localAppDataRoot
Ensure-Directory -Path $contractsRoot
Ensure-Directory -Path $demoDataRoot
Ensure-Directory -Path $requestSamplesRoot
Ensure-Directory -Path $logsRoot
Ensure-Directory -Path $configRoot

Copy-SeedTree -Source $demoDataSeedRoot -Destination $demoDataRoot
Copy-SeedTree -Source $requestSamplesSeedRoot -Destination $requestSamplesRoot
Copy-SeedTree -Source $installedExamplesRoot -Destination $examplesCopyRoot

Write-TemplateContracts

if ($Force -or -not (Test-Path $configFilePath)) {
    $settings = @{
        ContractRootDirectory = $appDataRoot
    } | ConvertTo-Json

    Set-Content -LiteralPath $configFilePath -Value $settings -Encoding UTF8
}

Write-Host "Demo workspace ready."
Write-Host "Contracts: $contractsRoot"
Write-Host "Demo data: $demoDataRoot"
