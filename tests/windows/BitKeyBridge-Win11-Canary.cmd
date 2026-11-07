@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem BitKeyBridge 0.36.0-preview.2 Windows 11 manual acceptance.
rem Read-only preflight; manual prompts record statuses ONLY.
rem NEVER reads or prints BitLocker/LAPS values, token, identity or device name.
rem NEVER uploads data or modifies AD, Intune, RBAC, services or GPO.
if /I "%~1"=="/?" goto :help
if /I "%~1"=="-h" goto :help
if /I "%~1"=="--help" goto :help

if "%~1"=="" goto :help_error
for %%F in ("%~1") do set "BKB_EXE=%%~fF"
for %%F in ("%BKB_EXE%") do set "BKB_NAME=%%~nxF"
if /I not "%BKB_NAME%"=="BitKeyBridge.exe" (
    echo ERROR: Supply the extracted BitKeyBridge.exe, not a script or other program.
    exit /b 2
)
if not exist "%BKB_EXE%" (
    echo ERROR: BitKeyBridge.exe was not found. Extract the verified QA ZIP first.
    exit /b 2
)

set "BKB_MODE=%~2"
if not defined BKB_MODE set "BKB_MODE=/guided"
if /I not "%BKB_MODE%"=="/guided" if /I not "%BKB_MODE%"=="/preflight" goto :help_error

rem ProductType=WinNT means workstation; LanmanNT is a DC, ServerNT a server.
set "BKB_PRODUCT="
for /f "tokens=3" %%V in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\ProductOptions" /v ProductType 2^>nul ^| findstr /I /C:"ProductType"') do set "BKB_PRODUCT=%%V"
if /I not "%BKB_PRODUCT%"=="WinNT" (
    echo ERROR: Canary helper is restricted to Windows client workstations.
    echo Do not test a domain controller or server with this script.
    exit /b 2
)

set "BKB_BUILD="
for /f "tokens=3" %%V in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v CurrentBuildNumber 2^>nul ^| findstr /I /C:"CurrentBuildNumber"') do set "BKB_BUILD=%%V"
if not defined BKB_BUILD (
    echo ERROR: Unable to verify Windows 11 version.
    exit /b 2
)
set /a "BKB_BUILD_NUMBER=%BKB_BUILD%" >nul 2>&1
if %BKB_BUILD_NUMBER% LSS 22000 (
    echo ERROR: This canary procedure is for Windows 11 build 22000 or later.
    exit /b 2
)

rem Full administrator/elevated tokens are not permitted in the Helpdesk test.
fltmc >nul 2>&1
if not errorlevel 1 (
    echo ERROR: Run as a non-elevated standard Helpdesk test account.
    echo Do not use Run as administrator.
    exit /b 2
)

if not defined LOCALAPPDATA (
    echo ERROR: Current user has no LOCALAPPDATA directory.
    exit /b 2
)
set "BKB_ROOT=%LOCALAPPDATA%\BitKeyBridge\Acceptance"
set "BKB_PRIVATE=%BKB_ROOT%\Preview2-%RANDOM%-%RANDOM%"
mkdir "%BKB_PRIVATE%" >nul 2>&1
if not exist "%BKB_PRIVATE%" (
    echo ERROR: Unable to create the private local acceptance folder.
    exit /b 2
)
set "BKB_SAFE=%BKB_PRIVATE%\SAFE-RESULTS.txt"
set "BKB_STARTUP=%BKB_PRIVATE%\startup-PRIVATE.json"
set "BKB_BROKER=%BKB_PRIVATE%\broker-PRIVATE.json"
set "BKB_VERSION=%BKB_PRIVATE%\version-PRIVATE.txt"
set "BKB_FAILED=0"

> "%BKB_SAFE%" (
    echo BitKeyBridgeCanary=0.36.0-preview.2
    echo Windows11Client=PASS
    echo NonElevatedToken=PASS
    echo NetworkUpload=DISABLED
    echo ActualSecretRead=MANUAL_ONLY
)
echo.
echo BitKeyBridge 0.36.0-preview.2 - local Windows 11 canary
echo No data is transmitted. Do not share PRIVATE diagnostic JSON.
echo.
"%BKB_EXE%" --version > "%BKB_VERSION%" 2>&1
if errorlevel 1 (
    >> "%BKB_SAFE%" echo AppVersion=FAIL
    echo ERROR: --version command failed.
    exit /b 2
)
findstr /I /C:"0.36.0-preview.2" "%BKB_VERSION%" >nul
if errorlevel 1 (
    >> "%BKB_SAFE%" echo AppVersion=FAIL
    echo ERROR: Wrong app version. Use exactly 0.36.0-preview.2.
    exit /b 2
)
>> "%BKB_SAFE%" echo AppVersion=PASS

"%BKB_EXE%" --startup-diagnostics --no-elevation > "%BKB_STARTUP%" 2>&1
set "BKB_STARTUP_EXIT=%ERRORLEVEL%"
if not "%BKB_STARTUP_EXIT%"=="0" (
    >> "%BKB_SAFE%" echo StartupDiagnostics=FAIL
    echo ERROR: Startup diagnostics failed; review PRIVATE local report only.
    exit /b 2
)
for %%F in ("%BKB_STARTUP%") do if %%~zF EQU 0 (
    >> "%BKB_SAFE%" echo StartupDiagnostics=FAIL
    echo ERROR: Startup diagnostic output was empty.
    exit /b 2
)
>> "%BKB_SAFE%" echo StartupDiagnostics=PASS

rem This is read-only. Do NOT use --probe here; it writes an audit event.
"%BKB_EXE%" --audit-broker-preflight --no-elevation > "%BKB_BROKER%" 2>&1
set "BKB_BROKER_EXIT=%ERRORLEVEL%"
if "%BKB_BROKER_EXIT%"=="0" (
    >> "%BKB_SAFE%" echo ProtectedAuditPreflight=PASS
) else (
    >> "%BKB_SAFE%" echo ProtectedAuditPreflight=NOT_READY
    echo NOTE: Protected Audit Broker not verified. Do not read secrets until approved.
)

echo Local preflight completed.
echo Private JSON is stored in your user profile. Do NOT upload it.
if /I "%BKB_MODE%"=="/preflight" goto :finish

echo.
echo Guided acceptance: open BitKeyBridge in your normal user session.
echo Perform only approved actions on a disposable, rotatable test device.
echo Mark each result P=Pass, F=Fail, or S=Skip. No free-text is collected.
echo Run this separately as authorized and denied standard test accounts.
echo Never type a password or recovery key into this script.
call :mark "StartToDevices" "Start exact device -> Devices: same verified hardware metadata, no second search."
call :mark "GraphOneSession" "Connect Microsoft 365 once; Start/Devices/Device 360 switch without extra MFA."
call :mark "DeviceIdentityMismatch" "Missing or ambiguous ID: old Device/Recovery ID are cleared, not guessed by name."
call :mark "RecoveryIdSelection" "One verified Recovery ID selectable; multiple IDs require manual exact choice."
call :mark "AuthorizedBitLocker" "Approved test reader: BitLocker Reveal/Copy obey ticket, MFA and audit; no leaked logs."
call :mark "DeniedBitLocker" "Denied test reader: BitLocker Reveal/Copy refused with NO key displayed/copied."
call :mark "AuthorizedLaps" "Approved test reader: LAPS Reveal/Copy obey ticket, MFA and audit; no leaked logs."
call :mark "DeniedLaps" "Denied test reader: LAPS Reveal/Copy refused with NO password displayed/copied."
call :mark "WmiAllowed" "Read-only WMI authorized: verified BIOS/CPU/RAM/TPM and consistent Health."
call :mark "WmiDenied" "WMI denied/offline/mismatch: typed reason; no stale or false-green hardware."
call :mark "ExpiredGraphSession" "Expired/changed Graph session: no late recovery value; explicit reconnect only."
call :mark "DpiRtlRdp" "EN/RU/HE, RTL and RDP at 100/125/150/175/200%% DPI: no clipped actions."
call :mark "ProtectedAuditNoSecrets" "Authorized test: pre-read durable ACK; journal has NO secret bytes."
goto :finish

:mark
echo.
echo TEST: %~2
choice /C PFS /N /M "Result [P]ass / [F]ail / [S]kip: "
if errorlevel 3 (
    set "BKB_MARK=SKIP"
) else if errorlevel 2 (
    set "BKB_MARK=FAIL"
    set "BKB_FAILED=1"
) else (
    set "BKB_MARK=PASS"
)
>> "%BKB_SAFE%" echo %~1=%BKB_MARK%
exit /b 0

:finish
echo.
echo Safe status-only report: "%BKB_SAFE%"
echo Private diagnostics stay beside it; NEVER upload those PRIVATE files.
echo Review SAFE-RESULTS.txt manually before sharing even these statuses.
if "%BKB_FAILED%"=="1" (
    echo One or more manual acceptance checks FAILED. Do not approve Stable.
    exit /b 3
)
echo Not tested or skipped items are NOT acceptance approval.
exit /b 0

:help_error
echo ERROR: Invalid arguments.
:help
echo BitKeyBridge Windows 11 canary - SAFE local acceptance
echo Usage:
echo   BitKeyBridge-Win11-Canary.cmd "C:\QA\BitKeyBridge.exe" /preflight
echo   BitKeyBridge-Win11-Canary.cmd "C:\QA\BitKeyBridge.exe" /guided
echo   BitKeyBridge-Win11-Canary.cmd /?
echo Only Windows 11 standard user. No AD/Graph automation or secret export.
exit /b 2
