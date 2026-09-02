@echo off
setlocal
cd /d "%~dp0"
echo ====================================================
echo   FLOWMY - XUAT DANH SACH THU MUC ASSETS
echo ====================================================
echo.

node "scripts\export_asset_folders.js"

echo.
pause
