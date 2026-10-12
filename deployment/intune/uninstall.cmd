@echo off
setlocal EnableExtensions DisableDelayedExpansion
REM Preserve ProgramData, user data, secrets, snapshots and audit history.
set "TARGET=%ProgramFiles%\BitKeyBridge"
fltmc >nul 2>&1
if errorlevel 1 exit /b 5
if not exist "%TARGET%\BitKeyBridge.exe" exit /b 0
del /f /q "%TARGET%\BitKeyBridge.exe" >nul 2>&1
if exist "%TARGET%\BitKeyBridge.exe" (
    echo ERROR: BitKeyBridge executable may be running or in use.
    exit /b 10
)
if exist "%TARGET%\BitKeyBridge.exe.bak" del /f /q "%TARGET%\BitKeyBridge.exe.bak" >nul 2>&1
rmdir "%TARGET%" >nul 2>&1
echo BitKeyBridge binary removed. ProgramData and user data preserved.
exit /b 0
