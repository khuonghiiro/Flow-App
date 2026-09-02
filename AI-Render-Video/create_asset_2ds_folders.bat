@echo off
setlocal
cd /d "%~dp0"
echo ====================================================
echo   FLOWMY - KHOI TAO CAY THU MUC ASSET_2DS
echo ====================================================
echo.
echo   Dang tao cay thu muc 2D chuyen dung cho chi tiet nhan vat va map...
echo.

node "scripts\create_asset_2ds_folders.js"

echo.
echo   [OK] Hoan tat! Cay thu muc asset_2ds da san sang.
echo.
pause

