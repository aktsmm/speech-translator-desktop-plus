$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw ".NET SDK/runtime was not found at $dotnet. Install .NET 10 SDK first."
}

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'

$env:PATH = "$dotnetDir;$env:PATH"
& $dotnet run --project $projectPath
