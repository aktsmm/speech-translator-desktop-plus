param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\SpeechTranslatorDesktopPlus'),
    [switch]$SkipDotNetInstall
)

$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'

if (-not $SkipDotNetInstall -and -not (Test-Path -LiteralPath $dotnet)) {
    New-Item -ItemType Directory -Force -Path $dotnetDir | Out-Null
    $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel '10.0' -InstallDir $dotnetDir
}

& (Join-Path $PSScriptRoot 'publish.ps1') -InstallPath $InstallPath -CreateDesktopShortcut
Write-Host "Setup completed. Launch from the desktop shortcut: Speech Translator Desktop Plus"
