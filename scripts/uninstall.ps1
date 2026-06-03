param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\SpeechTranslatorDesktopPlus'),
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'

$resolvedInstallPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallPath)
$targetExe = Join-Path $resolvedInstallPath 'SpeechTranslatorDesktopPlus.exe'

$runningApp = Get-CimInstance Win32_Process -Filter "Name = 'SpeechTranslatorDesktopPlus.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -eq $targetExe } |
    Select-Object -First 1
if ($runningApp) {
    throw "Speech Translator Desktop Plus is running from '$resolvedInstallPath'. Close the app before uninstalling."
}

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Speech Translator Desktop Plus.lnk'
if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force
}

if (Test-Path -LiteralPath $resolvedInstallPath) {
    Remove-Item -LiteralPath $resolvedInstallPath -Recurse -Force
}

if ($RemoveSettings) {
    $settingsPath = Join-Path $env:LOCALAPPDATA 'SpeechTranslatorDesktop'
    if (Test-Path -LiteralPath $settingsPath) {
        Remove-Item -LiteralPath $settingsPath -Recurse -Force
    }
}

Write-Host "Uninstalled Speech Translator Desktop Plus from: $resolvedInstallPath"
if (-not $RemoveSettings) {
    Write-Host "Local settings were kept. Use -RemoveSettings to remove them."
}
