@echo off
cd /d "%~dp0"

echo =======================================================
echo   Starting AI Image Rotate Server (Port 3978)
echo =======================================================

if exist "venv\Scripts\activate.bat" (
    call venv\Scripts\activate.bat
) else (
    echo [WARNING] Virtual environment 'venv' not found!
    echo Please run 'setup_env.bat' first.
    echo Attempting to run with system python...
)

python -m uvicorn app.main:app --host 0.0.0.0 --port 3978 --reload

pause
