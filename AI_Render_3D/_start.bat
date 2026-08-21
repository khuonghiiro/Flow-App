@echo off
REM ======================================================================
REM   One-Click Launcher for Image-to-Rig Pipeline (RTX 3060 12GB)
REM   1. Starts FastAPI Backend REST Server (Port 8000)
REM   2. Waits for Server Initialization
REM   3. Starts Gradio Web UI (Port 7860)
REM   4. Automatically opens Web UI in default Browser
REM ======================================================================

title [Studio 3D] Image-to-Rig Pipeline Launcher
color 0B

echo ======================================================================
echo   Starting Image-to-Rig Pipeline (Studio 3D Animation Engine)
echo   Hardware: NVIDIA RTX 3060 12GB VRAM
echo ======================================================================
echo.

REM Activate virtual environment if present
if exist "venv\Scripts\activate.bat" (
    echo [INFO] Activating virtual environment 'venv'...
    call venv\Scripts\activate.bat
) else (
    echo [INFO] Running using system Python environment...
)

REM 1. Start FastAPI REST Server in background
echo [1/3] Starting FastAPI REST Server on http://localhost:8000 ...
start "Image-to-Rig API Server (Port 8000)" /min cmd /c "python server.py"

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
echo   Close this window to stop the Gradio Web application.
echo ======================================================================
echo.

python app.py

pause
