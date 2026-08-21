@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion
REM Setup script for image-to-rig-pipeline on Windows
REM Optimized for NVIDIA RTX 3060 12GB (Ampere CUDA 12.1+)

echo ======================================================================
echo   Image-to-Rig Pipeline Environment Setup (RTX 3060 12GB)
echo ======================================================================

REM Check Python availability (prioritize Python 3.11 / 3.10 for PyTorch CUDA compatibility)
set "PYTHON_CMD="

py -3.11 --version >nul 2>&1
if %errorlevel% equ 0 (
    set "PYTHON_CMD=py -3.11"
    echo [INFO] Found Python 3.11 via py launcher.
) else (
    py -3.10 --version >nul 2>&1
    if %errorlevel% equ 0 (
        set "PYTHON_CMD=py -3.10"
        echo [INFO] Found Python 3.10 via py launcher.
    ) else (
        python --version >nul 2>&1
        if %errorlevel% equ 0 (
            set "PYTHON_CMD=python"
            echo [INFO] Using default system python.
        )
    )
)

if "%PYTHON_CMD%"=="" (
    echo [ERROR] No suitable Python installation found. Please install Python 3.11 or 3.10.
    pause
    exit /b 1
)

REM Create virtual environment if not exists
if not exist "venv\Scripts\python.exe" (
    echo [INFO] Creating Python virtual environment 'venv' using %PYTHON_CMD%...
    %PYTHON_CMD% -m venv venv
    if %errorlevel% neq 0 (
        echo [ERROR] Failed to create virtual environment.
        pause
        exit /b 1
    )
) else (
    echo [INFO] Virtual environment 'venv' already exists.
)

REM Activate virtual environment
call venv\Scripts\activate.bat

echo [INFO] Upgrading pip, setuptools, and wheel...
python -m pip install --upgrade pip setuptools wheel

echo [INFO] Installing PyTorch with CUDA 12.1 support...
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

echo [INFO] Installing dependencies from requirements.txt...
pip install -r requirements.txt

echo ======================================================================
echo [SUCCESS] Environment setup complete!
echo To download all AI models into 'models/': run 'python download_models.py'
echo To start Gradio Web UI: run 'python app.py' (or double-click _start.bat)
echo To start FastAPI REST Server: run 'python server.py'
echo ======================================================================
pause

