@echo off
cd /d "%~dp0"
title [Studio 3D] Image-to-Rig Pipeline Launcher
color 0B

echo ======================================================================
echo   Starting Image-to-Rig Pipeline (Studio 3D Animation Engine)
echo   Hardware: NVIDIA RTX 3060 12GB VRAM
echo ======================================================================
echo.

if not exist "venv\Scripts\python.exe" (
    echo [ERROR] Python virtual environment 'venv' was not found!
    echo Please double-click 'setup.bat' to initialize the environment first.
    echo.
    pause
    exit /b 1
)

set "PY_EXE=%~dp0venv\Scripts\python.exe"

REM Clean up any previous stale processes on ports 7860 and 8000
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7860 " ^| findstr "LISTENING"') do (
    taskkill /f /pid %%a >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":8000 " ^| findstr "LISTENING"') do (
    taskkill /f /pid %%a >nul 2>&1
)

REM 1. Start FastAPI REST Server in background
echo [1/3] Starting FastAPI REST Server on http://localhost:8000 ...
start "Image-to-Rig API Server (Port 8000)" /min "%PY_EXE%" server.py

REM Give the REST server 2 seconds to initialize
echo [2/3] Waiting for API Server to bind socket...
timeout /t 2 /nobreak >nul

REM 2. Launch default web browser to Gradio UI
echo [3/3] Opening Web UI in default browser: http://localhost:7860 ...
start http://localhost:7860

REM 3. Run Gradio Web UI in foreground
echo.
echo ======================================================================
echo   Web UI is running on: http://localhost:7860
echo   API Docs available on: http://localhost:8000/docs
echo   Keep this window open while using the application.
echo   Press CTRL+C or close this window to stop.
echo ======================================================================
echo.

"%PY_EXE%" app.py

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application exited with error code %errorlevel%.
)

pause
