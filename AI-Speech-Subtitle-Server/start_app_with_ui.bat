@echo off
setlocal enabledelayedexpansion
title [FlowMy Studio] AI Speech-to-Subtitle and Translation Studio
color 0B

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
echo     [*] KHOI DONG AI SPEECH-TO-SUBTITLE STUDIO (WEB UI ^& REST API)
echo     [*] Giao dien Web Studio: http://127.0.0.1:8765
echo     [*] REST API Endpoint:    http://127.0.0.1:8765/api/transcribe
echo ===============================================================================
echo.
echo Dang mo trinh duyet...

start "" http://127.0.0.1:8765

python -m uvicorn backend.server:app --host 127.0.0.1 --port 8765 --workers 1

pause
