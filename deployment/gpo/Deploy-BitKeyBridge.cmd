@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem BitKeyBridge controlled unattended GPO/Intune install (native CMD only).
rem Usage: Deploy-BitKeyBridge.cmd "<approved BitKeyBridge.exe>" "<pinned SHA256>" "<allowlist.txt>" "0.30.5" [/verify-only]
rem Script does not enable unsigned update consent, register a SYSTEM updater,
rem modify protected audit or fetch arbitrary public releases.

set "BKB_SOURCE=%~1"
set "BKB_PIN=%~2"
set "BKB_ALLOW=%~3"
set "BKB_VERSION=%~4"
set "BKB_MODE=%~5"
set "BKB_RESULT=0"

if /I "%~1"=="/test-version-order" goto test_version_order

if not defined BKB_SOURCE goto usage
if not defined BKB_PIN goto usage
if not defined BKB_ALLOW goto usage
if not defined BKB_VERSION goto usage
if defined BKB_MODE if /I not "%BKB_MODE%"=="/verify-only" goto usage
if not "%BKB_PIN:~64,1%"=="" goto usage
if "%BKB_PIN:~63,1%"=="" goto usage
echo(%BKB_PIN%| findstr /R /I /X /C:"[0-9a-f][0-9a-f]*" >nul 2>&1
if errorlevel 1 goto usage
if not exist "%BKB_ALLOW%" (
  echo ERROR: Approved device allowlist unavailable.
  exit /b 2
)
rem Never trust wildcard inclusion or a global "ALL" opt-in.
findstr /I /L /X /C:"%COMPUTERNAME%" "%BKB_ALLOW%" >nul 2>&1
if errorlevel 1 (
  echo SKIP: This machine is outside the approved deployment ring.
  exit /b 0
)
if not exist "%BKB_SOURCE%" (
  echo ERROR: Approved local/UNC executable is unavailable.
  exit /b 2
)
call :VerifyHash "%BKB_SOURCE%"
if errorlevel 1 (
  echo ERROR: Source executable SHA256 does not match pinned release digest.
  exit /b 2
)

if /I "%BKB_MODE%"=="/verify-only" (
  echo PASS: Exact device allowlist and pinned SHA256 verified. No installation performed.
  exit /b 0
)

rem SYSTEM/administrator required to install under Program Files; never UAC
rem elevate, run an interactive prompt or silently accept unsigned updates.
fltmc >nul 2>&1
if errorlevel 1 (
  echo ERROR: GPO/Intune deployment requires administrator or SYSTEM.
  exit /b 5
)
set "BKB_TARGETDIR=%ProgramFiles%\BitKeyBridge"
set "BKB_TARGET=%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe"
set "BKB_STAGE=%ProgramFiles%\BitKeyBridge\BitKeyBridge.deploy-stage.exe"
set "BKB_STATE=%ProgramData%\BitKeyBridgeDeployment"
set "BKB_LOCK=%ProgramData%\BitKeyBridgeDeployment\_install-lock"
set "BKB_BACKUP=%ProgramData%\BitKeyBridgeDeployment\BitKeyBridge.previous.exe"
set "BKB_VERSIONFILE=%ProgramData%\BitKeyBridgeDeployment\stage-version.txt"
set "BKB_CURRENTFILE=%ProgramData%\BitKeyBridgeDeployment\installed-version.txt"
set "BKB_LOG=%SystemRoot%\Logs\BitKeyBridge-Deploy.log"

if not exist "%BKB_STATE%\." mkdir "%BKB_STATE%" >nul 2>&1
if not exist "%BKB_STATE%\." (
  echo ERROR: Cannot create deployment state directory.
  exit /b 5
)
mkdir "%BKB_LOCK%" >nul 2>&1
if errorlevel 1 (
  call :Log "SKIP: Installer lock already held; next scheduled run will retry."
  exit /b 12
)

if not exist "%BKB_TARGETDIR%\." mkdir "%BKB_TARGETDIR%" >nul 2>&1
if not exist "%BKB_TARGETDIR%\." goto failure
if exist "%BKB_TARGET%" (
  call :VerifyHash "%BKB_TARGET%"
  if not errorlevel 1 goto already_installed
)

if exist "%BKB_STAGE%" del /f /q "%BKB_STAGE%" >nul 2>&1
if exist "%BKB_STAGE%" goto failure
copy /b /y "%BKB_SOURCE%" "%BKB_STAGE%" >nul 2>&1
if errorlevel 1 goto failure
call :VerifyHash "%BKB_STAGE%"
if errorlevel 1 goto failure

rem Verify the exact expected semantic version before replacing a live binary.
"%BKB_STAGE%" --version > "%BKB_VERSIONFILE%" 2>nul
if errorlevel 1 goto bad_version
findstr /I /L /X /C:"%BKB_VERSION%" "%BKB_VERSIONFILE%" >nul 2>&1
if errorlevel 1 goto bad_version

rem Avoid accidental downgrade/same-version overwrite if another deployment
rem mechanism has already installed a different build or a newer release.
if exist "%BKB_TARGET%" (
  "%BKB_TARGET%" --version > "%BKB_CURRENTFILE%" 2>nul
  if errorlevel 1 goto version_conflict
  set "BKB_CURRENT_VERSION="
  for /f "usebackq delims=" %%V in ("%BKB_CURRENTFILE%") do if not defined BKB_CURRENT_VERSION set "BKB_CURRENT_VERSION=%%V"
  call :IsCurrentOlder "%BKB_CURRENT_VERSION%" "%BKB_VERSION%"
  if errorlevel 1 goto version_conflict
)

rem A backup is written before touching the installed executable. A locked
rem executable must fail without terminating operators' active recovery work.
if exist "%BKB_TARGET%" (
  copy /b /y "%BKB_TARGET%" "%BKB_BACKUP%" >nul 2>&1
  if errorlevel 1 goto failure
)
move /y "%BKB_STAGE%" "%BKB_TARGET%" >nul 2>&1
if errorlevel 1 goto replace_failed

call :VerifyHash "%BKB_TARGET%"
if errorlevel 1 goto restore_previous
"%BKB_TARGET%" --version > "%BKB_VERSIONFILE%" 2>nul
if errorlevel 1 goto restore_previous
findstr /I /L /X /C:"%BKB_VERSION%" "%BKB_VERSIONFILE%" >nul 2>&1
if errorlevel 1 goto restore_previous

call :Log "SUCCESS: Approved BitKeyBridge version installed; SHA256 and version checked."
set "BKB_RESULT=0"
goto cleanup

:already_installed
call :Log "SKIP: Installed executable already matches approved SHA256."
set "BKB_RESULT=0"
goto cleanup

:version_conflict
call :Log "SKIP: Installed version is newer/equal or unrecognized; manual review required."
set "BKB_RESULT=13"
goto cleanup

:bad_version
call :Log "ERROR: Staged executable does not match the approved version."
set "BKB_RESULT=11"
goto cleanup

:replace_failed
call :Log "ERROR: Could not replace executable (possibly in use); no process killed."
if not exist "%BKB_TARGET%" if exist "%BKB_BACKUP%" (
  copy /b /y "%BKB_BACKUP%" "%BKB_TARGET%" >nul 2>&1
)
set "BKB_RESULT=10"
goto cleanup

:restore_previous
call :Log "ERROR: Post-install check failed; trying previous executable."
set "BKB_RESULT=11"
if exist "%BKB_BACKUP%" (
  copy /b /y "%BKB_BACKUP%" "%BKB_STAGE%" >nul 2>&1
  if not errorlevel 1 (
    move /y "%BKB_STAGE%" "%BKB_TARGET%" >nul 2>&1
  )
)
goto cleanup

:failure
call :Log "ERROR: Deployment staging/copy operation failed."
set "BKB_RESULT=10"

:cleanup
if exist "%BKB_STAGE%" del /f /q "%BKB_STAGE%" >nul 2>&1
if exist "%BKB_VERSIONFILE%" del /f /q "%BKB_VERSIONFILE%" >nul 2>&1
if exist "%BKB_CURRENTFILE%" del /f /q "%BKB_CURRENTFILE%" >nul 2>&1
rmdir "%BKB_LOCK%" >nul 2>&1
endlocal & exit /b %BKB_RESULT%

:test_version_order
call :IsCurrentOlder "%~2" "%~3"
exit /b %ERRORLEVEL%

:IsCurrentOlder
rem Return success ONLY when the installed numeric SemVer is strictly older.
rem Unknown/invalid/same/newer versions never overwrite an installed build.
setlocal EnableExtensions DisableDelayedExpansion
set "VER_OLD_M="
set "VER_OLD_N="
set "VER_OLD_P="
set "VER_NEW_M="
set "VER_NEW_N="
set "VER_NEW_P="
for /f "tokens=1-3 delims=.-" %%A in ("%~1") do (
  set "VER_OLD_M=%%A"
  set "VER_OLD_N=%%B"
  set "VER_OLD_P=%%C"
)
for /f "tokens=1-3 delims=.-" %%A in ("%~2") do (
  set "VER_NEW_M=%%A"
  set "VER_NEW_N=%%B"
  set "VER_NEW_P=%%C"
)
for %%C in (VER_OLD_M VER_OLD_N VER_OLD_P VER_NEW_M VER_NEW_N VER_NEW_P) do (
  call :ValidateVersionComponent %%C
  if errorlevel 1 (endlocal & exit /b 1)
)
if %VER_OLD_M% GTR %VER_NEW_M% (endlocal & exit /b 1)
if %VER_OLD_M% LSS %VER_NEW_M% (endlocal & exit /b 0)
if %VER_OLD_N% GTR %VER_NEW_N% (endlocal & exit /b 1)
if %VER_OLD_N% LSS %VER_NEW_N% (endlocal & exit /b 0)
if %VER_OLD_P% GEQ %VER_NEW_P% (endlocal & exit /b 1)
endlocal & exit /b 0

:ValidateVersionComponent
call set "CHECK_VER=%%%~1%%"
if not defined CHECK_VER exit /b 1
echo(%CHECK_VER%| findstr /R /X /C:"[0-9][0-9]*" >nul 2>&1
exit /b %ERRORLEVEL%

:VerifyHash
rem Verify fixed digest from separately approved GPO/Intune deployment.
rem Only the full, exact hash output line may match.
certutil.exe -hashfile "%~1" SHA256 2>nul | findstr /I /L /X /C:"%BKB_PIN%" >nul 2>&1
exit /b %ERRORLEVEL%

:Log
>>"%BKB_LOG%" echo [%DATE% %TIME%] %~1
exit /b 0

:usage
echo Usage: Deploy-BitKeyBridge.cmd ^<EXE path^> ^<64-hex SHA256 pin^> ^<device allowlist^> ^<version^> [/verify-only]
exit /b 2
