$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
    if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET 10 SDK was not found. Run .\scripts\setup.ps1 first or install .NET 10 SDK and add dotnet.exe to PATH.'
    }
}

$repoRoot = Split-Path $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'

$env:PATH = "$dotnetDir;$env:PATH"
& $dotnet run --project $projectPath
