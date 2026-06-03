@echo off
setlocal

set "SCRIPT_DIR=%~dp0"

pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%setup.ps1" %*
if %ERRORLEVEL% equ 0 exit /b 0

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%setup.ps1" %*
exit /b %ERRORLEVEL%
