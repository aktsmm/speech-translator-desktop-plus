$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

$repoRoot = Split-Path $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'

$env:PATH = "$dotnetDir;$env:PATH"
& $dotnet run --project $projectPath
