@echo off
setlocal enabledelayedexpansion
title [FlowMy API] AI Speech-to-Subtitle REST Server (Port 8765)
color 0A

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
echo     [*] KHOI DONG AI SPEECH-TO-SUBTITLE SERVER (HEADLESS API MODE)
echo     [*] REST API Endpoint: http://127.0.0.1:8765/api/transcribe
echo     [*] API Docs:          http://127.0.0.1:8765/docs
echo ===============================================================================
echo.

.venv\Scripts\python.exe -m uvicorn backend.server:app --host 127.0.0.1 --port 8765 --workers 1

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Server bi dung hoac xay ra loi (Ma loi: %ERRORLEVEL%).
    pause
)

pause
