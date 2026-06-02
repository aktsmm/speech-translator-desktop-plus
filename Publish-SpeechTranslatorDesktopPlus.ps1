param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\SpeechTranslatorDesktopPlus'),
    [switch]$CreateDesktopShortcut
)

$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'
$resolvedInstallPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallPath)

New-Item -ItemType Directory -Force -Path $resolvedInstallPath | Out-Null
$env:PATH = "$dotnetDir;$env:PATH"
& $dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $resolvedInstallPath

if ($CreateDesktopShortcut) {
    $shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Speech Translator Desktop Plus.lnk'
    $target = Join-Path $resolvedInstallPath 'SpeechTranslatorDesktopPlus.exe'
    $ws = New-Object -ComObject WScript.Shell
    $link = $ws.CreateShortcut($shortcut)
    $link.TargetPath = $target
    $link.WorkingDirectory = $resolvedInstallPath
    $link.Description = 'Speech Translator Desktop Plus'
    $link.Save()
}

Write-Host "Published Speech Translator Desktop Plus to: $resolvedInstallPath"
