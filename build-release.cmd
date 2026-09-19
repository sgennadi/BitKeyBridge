@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

where dotnet.exe >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET 10 SDK was not found in PATH.
    exit /b 2
)

if exist artifacts rmdir /s /q artifacts
mkdir artifacts

dotnet restore src\BitKeyBridge\BitKeyBridge.csproj
if errorlevel 1 exit /b %errorlevel%

dotnet build src\BitKeyBridge\BitKeyBridge.csproj -c Release --no-restore
if errorlevel 1 exit /b %errorlevel%

for %%R in (win-x64 win-x86 win-arm64) do (
    echo.
    echo ============================================================
    echo Publishing %%R
    echo ============================================================
    dotnet publish src\BitKeyBridge\BitKeyBridge.csproj -c Release -r %%R --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o artifacts\%%R
    if errorlevel 1 exit /b !errorlevel!

    if /I "%%R"=="win-x64" (
        start /wait "" artifacts\%%R\BitKeyBridge.exe --self-test
        if errorlevel 1 exit /b !errorlevel!
    )
)

echo.
echo Builds completed:
echo %CD%\artifacts\win-x64\BitKeyBridge.exe
echo %CD%\artifacts\win-x86\BitKeyBridge.exe
echo %CD%\artifacts\win-arm64\BitKeyBridge.exe
exit /b 0
