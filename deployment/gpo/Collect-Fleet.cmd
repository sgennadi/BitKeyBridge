@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Machine-side GPO startup or Scheduled Task wrapper. Pass an approved
rem existing UNC share root; this script does NOT create a share or credentials.
if "%~1"=="" (
  echo Usage: Collect-Fleet.cmd "\\server\secure-inventory\BitKeyBridge" 1>&2
  exit /b 2
)
set "BKB_SHARE=%~1"
if not exist "%BKB_SHARE%\" (
  echo Fleet collection directory is not accessible. 1>&2
  exit /b 3
)
set "BKB_EXE=%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe"
if not exist "%BKB_EXE%" (
  echo BitKeyBridge machine installation is missing. 1>&2
  exit /b 4
)
"%BKB_EXE%" --fleet-export "%BKB_SHARE%\%COMPUTERNAME%.json" --no-elevation
set "BKB_EXIT=%ERRORLEVEL%"
endlocal & exit /b %BKB_EXIT%
