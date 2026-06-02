@echo off
setlocal

set "DOTNET_DIR=%LOCALAPPDATA%\Microsoft\dotnet"
set "DOTNET_EXE=%DOTNET_DIR%\dotnet.exe"

if not exist "%DOTNET_EXE%" (
  echo .NET SDK/runtime was not found at "%DOTNET_EXE%".
  echo Install .NET 10 SDK first.
  exit /b 1
)

set "PATH=%DOTNET_DIR%;%PATH%"
cd /d "%~dp0.."
if exist "%DOTNET_EXE%" (
  "%DOTNET_EXE%" run --project "%CD%\src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj"
) else (
  dotnet run --project "%CD%\src\SpeechTranslatorDesktop\SpeechTranslatorDesktop.csproj"
)
