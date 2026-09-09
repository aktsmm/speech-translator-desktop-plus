param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot) 'artifacts'),
    [string]$PackageName = 'SpeechTranslatorDesktopPlus-win-x64.zip',
    [string]$AppId = 'SpeechTranslatorDesktopPlus'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot
$resolvedOutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$staging = Join-Path $resolvedOutputDirectory 'SpeechTranslatorDesktopPlus-win-x64'
$zipPath = Join-Path $resolvedOutputDirectory $PackageName
$velopackOutput = Join-Path $resolvedOutputDirectory 'velopack'
$toolsRoot = Join-Path $repoRoot 'artifacts\tools'

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null
& (Join-Path $PSScriptRoot 'publish.ps1') -InstallPath $staging
if (-not (Test-Path -LiteralPath (Join-Path $staging 'SpeechTranslatorDesktopPlus.exe'))) {
    throw "publish.ps1 did not produce SpeechTranslatorDesktopPlus.exe in '$staging'."
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
Write-Host "Created package: $zipPath"

if (Test-Path -LiteralPath $velopackOutput) {
    Remove-Item -LiteralPath $velopackOutput -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $velopackOutput | Out-Null

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnet = Join-Path $dotnetDir 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

$props = [xml](Get-Content (Join-Path $repoRoot 'Directory.Build.props'))
$version = $props.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'VersionPrefix is not defined in Directory.Build.props.'
}

$desktopProject = [xml](Get-Content (Join-Path $repoRoot 'src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj'))
$velopackPackageReference = $desktopProject.Project.ItemGroup.PackageReference |
    Where-Object { $_.Include -eq 'Velopack' } |
    Select-Object -First 1
$velopackVersion = $velopackPackageReference.GetAttribute('Version')
if ([string]::IsNullOrWhiteSpace($velopackVersion)) {
    throw 'Velopack PackageReference version is not defined in SpeechTranslatorDesktop.csproj.'
}

$vpkToolPath = Join-Path $toolsRoot "vpk-$velopackVersion"
$vpkExe = Join-Path $vpkToolPath 'vpk.exe'
if (-not (Test-Path -LiteralPath $vpkExe)) {
    New-Item -ItemType Directory -Force -Path $vpkToolPath | Out-Null
    & $dotnet tool install vpk --version $velopackVersion --tool-path $vpkToolPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install vpk $velopackVersion. Exit code: $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    & $vpkExe pack -u $AppId -v $version -o $velopackOutput -p $staging -f net10-x64-desktop
    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
Write-Host "Created Velopack package: $velopackOutput"

$checksumPath = Join-Path $resolvedOutputDirectory 'SHA256SUMS.txt'
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
"$($hash.Hash.ToLowerInvariant())  $PackageName" | Set-Content -LiteralPath $checksumPath -Encoding ascii
Write-Host "Created checksum: $checksumPath"
