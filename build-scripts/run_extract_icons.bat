@echo off
echo ================================================
echo   Icon Extraction Script Runner
echo ================================================
echo.

REM Get the directory where this batch file is located (removes trailing backslash)
set "BATCH_DIR=%~dp0"
if "%BATCH_DIR:~-1%"=="\" set "BATCH_DIR=%BATCH_DIR:~0,-1%"

REM The PowerShell script is in the same directory as this batch file
set "PS_SCRIPT=%BATCH_DIR%\ExtractIcons.ps1"

REM Check if script exists
if not exist "%PS_SCRIPT%" (
    echo ERROR: PowerShell script not found at:
    echo %PS_SCRIPT%
    echo.
    echo Current directory: %CD%
    echo Batch directory: %BATCH_DIR%
    echo.
    pause
    exit /b 1
)

echo Running script: %PS_SCRIPT%
echo.

REM Run the PowerShell script and keep window open
powershell.exe -NoExit -ExecutionPolicy Bypass -File "%PS_SCRIPT%"

pause