@echo off
setlocal

set "DOTNET_DIR=%LOCALAPPDATA%\Microsoft\dotnet"
set "DOTNET_EXE=%DOTNET_DIR%\dotnet.exe"

if not exist "%DOTNET_EXE%" (
  echo .NET SDK/runtime was not found at "%DOTNET_EXE%".
  echo Run .\scripts\setup.ps1 first, or install .NET 10 SDK and add dotnet.exe to PATH.
  exit /b 1
)

set "PATH=%DOTNET_DIR%;%PATH%"
cd /d "%~dp0.."
"%DOTNET_EXE%" run --project "%CD%\src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj"
