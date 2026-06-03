param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\SpeechTranslatorDesktopPlus'),
    [switch]$CreateDesktopShortcut,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

$repoRoot = Split-Path $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'
$resolvedInstallPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallPath)
$targetExe = Join-Path $resolvedInstallPath 'SpeechTranslatorDesktopPlus.exe'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file was not found: $projectPath"
}

$runningApp = Get-CimInstance Win32_Process -Filter "Name = 'SpeechTranslatorDesktopPlus.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -eq $targetExe } |
    Select-Object -First 1
if ($runningApp) {
    throw "Speech Translator Desktop Plus is running from '$resolvedInstallPath'. Close the app before publishing to this install path."
}

New-Item -ItemType Directory -Force -Path $resolvedInstallPath | Out-Null
$env:PATH = "$dotnetDir;$env:PATH"
$selfContained = -not $FrameworkDependent
& $dotnet publish $projectPath -c Release -r win-x64 --self-contained:$selfContained -o $resolvedInstallPath

if ($CreateDesktopShortcut) {
    $shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Speech Translator Desktop Plus.lnk'
    $ws = New-Object -ComObject WScript.Shell
    $link = $ws.CreateShortcut($shortcut)
    $link.TargetPath = $targetExe
    $link.WorkingDirectory = $resolvedInstallPath
    $link.Description = 'Speech Translator Desktop Plus'
    $link.Save()
}

Write-Host "Published Speech Translator Desktop Plus to: $resolvedInstallPath"
