@echo off
title AI 3D Animation Studio - Khoi Chay
color 0A
cls

echo ==============================================================================
echo                 AI 3D ANIMATION STUDIO - KHOI CHAY HE THONG
echo ==============================================================================
echo.

cd /d "%~dp0"

echo [1/3] Kiem tra va giai phong Port 5173 neu dang bi chiem dung...
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5173 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"
echo - Port 5173 da san sang.
echo.

echo [2/3] Mo trinh duyet Studio...
start "" "http://localhost:5173/"
echo - Da mo trinh duyet.
echo.

echo [3/3] Dang khoi dong may chu Vite Dev Server...
echo ==============================================================================
echo    Studio dang chay tai: http://localhost:5173/ (Nhan Ctrl+C de dung)
echo ==============================================================================
echo.

call npm.cmd run dev

pause
