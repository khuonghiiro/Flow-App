@echo off
chcp 65001 >nul 2>&1
echo ============================================
echo  🔍 ASSET SCANNER - AI 3D Animation Studio
echo ============================================
echo.
echo  Dang quet thu muc assets/ ...
echo.

node "%~dp0scripts\scan_assets.js"

echo.
echo  ✅ Hoan tat! Da cap nhat:
echo     - assets\ASSET_CATALOG.md    (English for AI agents)
echo     - assets\ASSET_CATALOG_VI.md (Tieng Viet cho User)
echo     - assets\asset_manifest.json (JSON cho code)
echo.
pause
