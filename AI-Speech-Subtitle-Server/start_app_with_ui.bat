@echo off
setlocal enabledelayedexpansion
title [FlowMy Studio] AI Speech-to-Subtitle and Translation Studio
color 0B

cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    color 0C
    echo [ERROR] Chua khoi tao moi truong ao .venv!
    echo Vui long chay file 'install_env.bat' truoc de cai dat day du thu vien.
    pause
    exit /b 1
)

REM Tat canh bao Symlink va Tokenizer tren Windows
set HF_HUB_DISABLE_SYMLINKS_WARNING=1
set TOKENIZERS_PARALLELISM=false

REM Tu dong giai phong port 8765 neu co tien trinh cu dang chiem dung
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":8765" ^| findstr "LISTENING"') do taskkill /f /pid %%a >nul 2>&1

echo ===============================================================================
echo     [*] KHOI DONG AI SPEECH-TO-SUBTITLE STUDIO (WEB UI ^& REST API)
echo     [*] Giao dien Web Studio: http://127.0.0.1:8765
echo     [*] REST API Endpoint:    http://127.0.0.1:8765/api/transcribe
echo ===============================================================================
echo.
echo Dang mo trinh duyet...

start "" http://127.0.0.1:8765

.venv\Scripts\python.exe -m uvicorn backend.server:app --host 127.0.0.1 --port 8765 --workers 1

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Server bi dung hoac xay ra loi (Ma loi: %ERRORLEVEL%).
    pause
)

pause
