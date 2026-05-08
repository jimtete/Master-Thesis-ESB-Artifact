[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [string]$Version = "0.2.2",

    [string]$InnoSetupCompilerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$installerRoot = Split-Path -Parent $PSCommandPath
$publishScriptPath = Join-Path $installerRoot "Publish-OlympusServiceBus.ps1"
$issPath = Join-Path $installerRoot "OlympusServiceBus.iss"
$stageRoot = Join-Path $installerRoot "artifacts\stage\$Configuration\$Runtime"
$outputRoot = Join-Path $installerRoot "artifacts\installer\$Configuration\$Runtime"

function Resolve-InnoSetupCompiler {
    param(
        [string]$ExplicitPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "The supplied Inno Setup compiler path does not exist: $ExplicitPath"
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "ISCC.exe was not found. Install Inno Setup 6 and rerun this script, or pass -InnoSetupCompilerPath."
}

& $publishScriptPath -Configuration $Configuration -Runtime $Runtime -SelfContained -Clean

if ($LASTEXITCODE -ne 0) {
    throw "Publishing the installer payload failed."
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$isccPath = Resolve-InnoSetupCompiler -ExplicitPath $InnoSetupCompilerPath

$compilerArguments = @(
    "/DStageDir=$stageRoot",
    "/DAppVersion=$Version",
    "/DInstallerOutputDir=$outputRoot",
    $issPath
)

Write-Host "Compiling installer with $isccPath"
& $isccPath @compilerArguments

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed."
}

Write-Host ""
Write-Host "Installer output directory:"
Write-Host $outputRoot
