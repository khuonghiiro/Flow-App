@echo off
REM ExtractVersion.bat
REM Script để gọi PowerShell extract version từ .csproj

echo Calling PowerShell to extract version...

REM Gọi PowerShell script
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ExtractVersion.ps1"

if %errorlevel% neq 0 (
    echo ERROR: Failed to extract version
    pause
    exit /b 1
)

echo Done!
pause
exit /b 0