@echo off
chcp 65001 >nul
title [FlowMy API] AI Speech-to-Subtitle REST Server (Port 8765)
color 0A

cd /d "%~dp0"

if not exist ".venv\Scripts\activate.bat" (
    color 0C
    echo [ERROR] Chua khoi tao moi truong ao .venv!
    echo Vui long chay file 'install_env.bat' truoc de cai dat day du thu vien.
    pause
    exit /b 1
)

call .venv\Scripts\activate.bat

echo ===============================================================================
echo     🚀 KHOI DONG AI SPEECH-TO-SUBTITLE SERVER (HEADLESS API MODE)
echo     📡 REST API Endpoint: http://127.0.0.1:8765/api/transcribe
echo     📖 API Docs:          http://127.0.0.1:8765/docs
echo ===============================================================================
echo.

python -m uvicorn backend.server:app --host 127.0.0.1 --port 8765 --workers 1

pause
