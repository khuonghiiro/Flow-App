@echo off
echo ====================================
echo Bat dau chay cac file theo thu tu
echo ====================================

REM Chay ExtractVersion.bat truoc
echo.
echo [1/3] Dang chay ExtractVersion.bat...
call "%~dp0ExtractVersion.bat"
if errorlevel 1 (
    echo Loi khi chay ExtractVersion.bat!
    pause
    exit /b 1
)
echo ExtractVersion.bat hoan thanh!

REM Chay Build-Release.bat
echo.
echo [2/3] Dang chay Build-Release.bat...
call "%~dp0Build-Release.bat"
if errorlevel 1 (
    echo Loi khi chay Build-Release.bat!
    pause
    exit /b 1
)
echo Build-Release.bat hoan thanh!

REM Mo file SettingExe.iss bang Inno Setup
echo.
echo [3/3] Dang mo SettingExe.iss...
start "" "%~dp0SettingExe.iss"

echo.
echo ====================================
echo Hoan thanh tat ca!
echo ====================================
pause