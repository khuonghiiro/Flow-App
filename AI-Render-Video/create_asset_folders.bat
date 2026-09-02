@echo off
setlocal
cd /d "%~dp0"
echo ====================================================
echo   FLOWMY - KHOI TAO CAY THU MUC ASSETS CHUAN
echo ====================================================
echo.
echo   Dang tao thu muc chuan cho cac danh muc...
echo.

if "%~1"=="" (
    node "scripts\create_asset_folders.js" vi --clean
) else (
    node "scripts\create_asset_folders.js" %*
)

echo.
echo   [OK] Hoan tat! Cay thu muc chuan da san sang.
echo.
pause
