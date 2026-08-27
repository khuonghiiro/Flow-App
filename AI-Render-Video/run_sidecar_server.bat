@echo off
title AI Studio - Antigravity Sidecar Server (:5050)
color 0E
cls

echo ==============================================================================
echo             AI STUDIO - ANTIGRAVITY SIDECAR SERVER (PORT 5050)
echo ==============================================================================
echo.

cd /d "%~dp0"

echo [1/2] Kiem tra va giai phong Port 5050...
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5050 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"
echo - Port 5050 da san sang.
echo.

echo [2/2] Dang khoi chay Sidecar Server...
echo ==============================================================================
echo    Sidecar API: http://127.0.0.1:5050/ (Nhan Ctrl+C de dung)
echo ==============================================================================
echo.

py -3.11 server/antigravity_sidecar_server.py
if %errorlevel% neq 0 (
    echo [CANH BAO] py -3.11 khong kha dung, thu khoi chay voi python mac dinh...
    python server/antigravity_sidecar_server.py
)

pause
