@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo   2D ASSET SCANNER - FlowMy 2D Studio
echo ============================================
echo.
echo   Scanning asset_2ds folder...
echo.

node "scripts\scan_asset_2ds.js"

echo.
echo   [OK] Scan completed! Updated:
echo     - asset_2ds\asset_2d_manifest.json (JSON cho code)
echo.
pause
