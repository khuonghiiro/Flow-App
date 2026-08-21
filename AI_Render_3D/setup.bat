@echo off
REM Setup script for image-to-rig-pipeline on Windows
REM Optimized for NVIDIA RTX 3060 12GB (Ampere CUDA 12.1+)

echo ======================================================================
echo   Image-to-Rig Pipeline Environment Setup (RTX 3060 12GB)
echo ======================================================================

REM Check Python availability
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python not found in PATH. Please install Python 3.10 or 3.11.
    pause
    exit /b 1
)

REM Create virtual environment if not exists
if not exist "venv" (
    echo [INFO] Creating Python virtual environment 'venv'...
    python -m venv venv
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
echo To start Gradio Web UI: run 'python app.py'
echo To start FastAPI REST Server: run 'python server.py'
echo ======================================================================
pause
