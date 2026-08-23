@echo off
chcp 65001 >nul
title AI Matting Server - BiRefNet (RTX 3060 Local Env)
cd /d "%~dp0"

echo ===================================================================
echo   KHOI DONG SERVER AI TACH NEN BIREFNET [RTX 3060 - LOCAL ENV]
echo ===================================================================
echo.

set PYTHONUNBUFFERED=1
set PYTHONIOENCODING=utf-8
set U2NET_HOME=%~dp0models\ai_matting
if not exist "models\ai_matting" mkdir "models\ai_matting"

if exist ".venv\Scripts\python.exe" goto START_SERVER

echo [1/2] Dang tao moi truong ao .venv trong thu muc source...
python -m venv .venv
echo.
echo [2/2] Dang cai dat rembg, onnxruntime-directml, pillow vao .venv...
.venv\Scripts\python.exe -m pip install --upgrade pip
.venv\Scripts\python.exe -m pip install rembg[gpu] onnxruntime-directml pillow
echo.

:START_SERVER
echo [*] Dang khoi dong Server AI tren cong 5000...
echo.
.venv\Scripts\python.exe -u server_ai_matting.py 5000

echo.
echo ===================================================================
echo [!] Server da dung lai.
echo ===================================================================
pause
cmd /k
