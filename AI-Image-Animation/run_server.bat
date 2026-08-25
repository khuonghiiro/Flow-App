@echo off
setlocal
cd /d "%~dp0"

echo =======================================================
echo   Starting AI Image Animation Server (Port 3979)
echo   Optimized for NVIDIA RTX 3060 12GB VRAM
echo   Studio UI: http://localhost:3979
echo   API Docs:  http://localhost:3979/docs
echo =======================================================

set "PY_EXE=venv\Scripts\python.exe"

if exist "%PY_EXE%" (
    echo [OK] Using local virtual environment (.venv)...
    "%PY_EXE%" -m uvicorn app.main:app --host 0.0.0.0 --port 3979 --reload
) else (
    echo [WARNING] Local virtual environment 'venv' not found!
    echo Automatically running 'setup_env.bat' to initialize environment...
    echo.
    call setup_env.bat
    if exist "%PY_EXE%" (
        echo [OK] Environment initialized. Starting server...
        "%PY_EXE%" -m uvicorn app.main:app --host 0.0.0.0 --port 3979 --reload
    ) else (
        echo [ERROR] Failed to find or initialize virtual environment.
        pause
    )
)

pause
