param(
    [string]$DotnetPath = "C:\Users\vainf\AppData\Local\Microsoft\dotnet\dotnet.exe",
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OutputDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "artifacts\ui-previews")
)

$projectPath = Join-Path $RepositoryRoot "scripts\UiPreviewHarness\UiPreviewHarness.csproj"
& $DotnetPath run --project $projectPath -- $RepositoryRoot $OutputDirectory
