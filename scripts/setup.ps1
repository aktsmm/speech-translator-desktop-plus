param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\SpeechTranslatorDesktopPlus'),
    [switch]$SkipDotNetInstall
)

$ErrorActionPreference = 'Stop'

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or [Environment]::OSVersion.Version.Major -lt 10) {
    throw 'Speech Translator Desktop Plus requires Windows 10 or later.'
}

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'

if (-not $SkipDotNetInstall -and -not (Test-Path -LiteralPath $dotnet)) {
    New-Item -ItemType Directory -Force -Path $dotnetDir | Out-Null
    $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
    try {
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -TimeoutSec 60
        & $installer -Channel '10.0' -InstallDir $dotnetDir
    }
    catch {
        throw "Failed to install the local .NET 10 SDK. Check your internet connection, proxy settings, or install .NET 10 SDK manually, then rerun this script with -SkipDotNetInstall. Details: $($_.Exception.Message)"
    }
}

& (Join-Path $PSScriptRoot 'publish.ps1') -InstallPath $InstallPath -CreateDesktopShortcut
Write-Host "Setup completed. Launch from the desktop shortcut: Speech Translator Desktop Plus"
