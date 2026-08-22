@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo   ASSET SCANNER - AI 3D Animation Studio
echo ============================================
echo.
echo   Scanning assets folder...
echo.

node "scripts\scan_assets.js"

echo.
echo   [OK] Scan completed! Updated:
echo     - assets\ASSET_CATALOG.md    (English for AI agents)
echo     - assets\ASSET_CATALOG_VI.md (Tieng Viet cho User)
echo     - assets\asset_manifest.json (JSON cho code)
echo.
pause
