@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Secret-free local acceptance report for a managed test workstation.
rem This script NEVER installs a service, modifies ACLs, reads passwords,
rem elevates, or sends the report to any network destination.
rem Usage: BitKeyBridge-DomainPreflight.cmd "C:\Path\BitKeyBridge.exe" [/probe]

set "BKB_EXE=%~1"
if not defined BKB_EXE set "BKB_EXE=%~dp0BitKeyBridge.exe"

if not exist "%BKB_EXE%" (
    echo ERROR: BitKeyBridge executable not found: "%BKB_EXE%"
    echo Usage: %~nx0 "C:\Path\BitKeyBridge.exe" [/probe]
    exit /b 2
)

set "BKB_PROBE="
if /I "%~2"=="/probe" set "BKB_PROBE=--probe"

set "BKB_REPORT_DIR=%LOCALAPPDATA%\BitKeyBridge\Acceptance"
if not exist "%BKB_REPORT_DIR%" mkdir "%BKB_REPORT_DIR%" >nul 2>&1
if not exist "%BKB_REPORT_DIR%" (
    echo ERROR: Could not create private per-user report directory.
    exit /b 2
)

set "BKB_REPORT=%BKB_REPORT_DIR%\Broker-Preflight-%RANDOM%-%RANDOM%.json"
set "BKB_STARTUP_REPORT=%BKB_REPORT_DIR%\Startup-Preflight-%RANDOM%-%RANDOM%.json"
echo BitKeyBridge standard-user acceptance preflight
echo EXE: "%BKB_EXE%"
echo.
echo Checking startup permissions with the current Windows token...
"%BKB_EXE%" --startup-diagnostics --no-elevation > "%BKB_STARTUP_REPORT%"
set "BKB_STARTUP_EXIT=%ERRORLEVEL%"
if not "%BKB_STARTUP_EXIT%"=="0" (
    echo ERROR: Startup diagnostics failed with exit %BKB_STARTUP_EXIT%.
    echo Read the report if present: "%BKB_STARTUP_REPORT%"
    exit /b 2
)
if not exist "%BKB_STARTUP_REPORT%" (
    echo ERROR: No startup diagnostic report was written.
    exit /b 2
)
for %%F in ("%BKB_STARTUP_REPORT%") do (
    if %%~zF EQU 0 (
        echo ERROR: Startup diagnostics returned empty JSON.
        exit /b 2
    )
)
echo Startup diagnostics report: "%BKB_STARTUP_REPORT%"
echo.
echo Checking protected audit broker configuration...
if defined BKB_PROBE (
    echo Explicit synthetic protected audit probe: ENABLED
) else (
    echo Synthetic protected audit probe: OFF, read-only
)

"%BKB_EXE%" --audit-broker-preflight %BKB_PROBE% --no-elevation > "%BKB_REPORT%"
set "BKB_EXIT=%ERRORLEVEL%"

if not exist "%BKB_REPORT%" (
    echo ERROR: No JSON report was written.
    exit /b 2
)
for %%F in ("%BKB_REPORT%") do (
    if %%~zF EQU 0 (
        echo ERROR: The JSON report is empty.
        exit /b 2
    )
)

echo.
echo Broker preflight exit code: %BKB_EXIT%
echo Startup report: "%BKB_STARTUP_REPORT%"
echo Broker report: "%BKB_REPORT%"
echo The report contains machine/identity metadata but no recovery secrets.
echo Do not publish this report or paste it into public GitHub issues.
echo.
echo Exit 0: local checks passed, production backend checks still pending.
echo Exit 3: at least one local check failed; see the JSON report.
exit /b %BKB_EXIT%
