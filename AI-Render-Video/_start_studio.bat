@echo off
title AI 3D Animation Studio - Khoi Chay
color 0A
cls

echo ==============================================================================
echo                 AI 3D ANIMATION STUDIO - KHOI CHAY HE THONG
echo ==============================================================================
echo.

cd /d "%~dp0"

echo [1/3] Kiem tra va giai phong Port 7122 neu dang bi chiem dung...
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 7122 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"
echo - Port 7122 da san sang.
echo.

echo [2/3] Mo trinh duyet Studio...
start "" "http://localhost:7122/"
echo - Da mo trinh duyet: http://localhost:7122/
echo.

echo [3/3] Dang khoi dong may chu Vite Dev Server...
echo ==============================================================================
echo    Studio dang chay tai: http://localhost:7122/ (Nhan Ctrl+C de dung)
echo ==============================================================================
echo.

call npm.cmd run dev

pause
