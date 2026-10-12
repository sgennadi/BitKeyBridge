@echo off
setlocal EnableExtensions DisableDelayedExpansion
REM BitKeyBridge unattended Win32 deployment; run elevated / as SYSTEM.
set "SOURCE=%~dp0BitKeyBridge.exe"
set "TARGET=%ProgramFiles%\BitKeyBridge"
if not exist "%SOURCE%" (
    echo ERROR: BitKeyBridge.exe is not in the package root.
    exit /b 2
)
fltmc >nul 2>&1
if errorlevel 1 (
    echo ERROR: Administrator or SYSTEM rights are required.
    exit /b 5
)
if not exist "%TARGET%" mkdir "%TARGET%"
if errorlevel 1 exit /b 5
if exist "%TARGET%\BitKeyBridge.exe" (
    copy /y "%TARGET%\BitKeyBridge.exe" "%TARGET%\BitKeyBridge.exe.bak" >nul
    if errorlevel 1 exit /b 10
)
copy /y "%SOURCE%" "%TARGET%\BitKeyBridge.exe" >nul
if errorlevel 1 (
    if exist "%TARGET%\BitKeyBridge.exe.bak" (
        copy /y "%TARGET%\BitKeyBridge.exe.bak" "%TARGET%\BitKeyBridge.exe" >nul
    )
    echo ERROR: Could not replace BitKeyBridge.exe. Check running processes.
    exit /b 10
)
"%TARGET%\BitKeyBridge.exe" --version >nul 2>&1
if errorlevel 1 (
    if exist "%TARGET%\BitKeyBridge.exe.bak" (
        copy /y "%TARGET%\BitKeyBridge.exe.bak" "%TARGET%\BitKeyBridge.exe" >nul
    )
    echo ERROR: Executable version smoke test failed.
    exit /b 11
)
echo BitKeyBridge executable deployed successfully.
exit /b 0
