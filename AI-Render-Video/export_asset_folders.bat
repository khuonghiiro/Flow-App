@echo off
chcp 65001 >nul 2>&1
echo ====================================================
echo  📋 FLOWMY - XUẤT DANH SÁCH TOÀN BỘ THƯ MỤC ASSETS
echo ====================================================
echo.

node "%~dp0scripts\export_asset_folders.js"

echo.
pause
