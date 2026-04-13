@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)
echo.
echo Build success.
endlocal
