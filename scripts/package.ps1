param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot) 'artifacts'),
    [string]$PackageName = 'SpeechTranslatorDesktopPlus-win-x64.zip'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot
$staging = Join-Path $OutputDirectory 'SpeechTranslatorDesktopPlus-win-x64'
$zipPath = Join-Path $OutputDirectory $PackageName

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& (Join-Path $PSScriptRoot 'publish.ps1') -InstallPath $staging

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
Write-Host "Created package: $zipPath"
