@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Managed-device GPO Task/Startup wrapper. No PowerShell or credentials.
rem The approved destination already exists; grant each machine account
rem permission only to create/replace its own named report.
set "BKB_LOG=%WINDIR%\Temp\BitKeyBridgeFleet.log"
if "%~1"=="" (
  >"%BKB_LOG%" echo ERROR: Expected an approved fleet inventory share.
  exit /b 2
)
set "BKB_SHARE=%~1"
if not exist "%BKB_SHARE%\." (
  >"%BKB_LOG%" echo ERROR: Fleet inventory directory not accessible.
  exit /b 3
)
set "BKB_EXE=%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe"
if not exist "%BKB_EXE%" (
  >"%BKB_LOG%" echo ERROR: Machine BitKeyBridge installation not found.
  exit /b 4
)
rem The EXE enforces the exact MACHINE.json leaf and atomically replaces it.
"%BKB_EXE%" --fleet-export "%BKB_SHARE%\%COMPUTERNAME%.json" --no-elevation >"%BKB_LOG%" 2>&1
set "BKB_EXIT=%ERRORLEVEL%"
endlocal & exit /b %BKB_EXIT%
