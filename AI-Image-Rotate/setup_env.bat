@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   AI Image Rotate - Environment Setup (RTX 3060 12GB)
echo =======================================================

cd /d "%~dp0"

:: Check if Python 3.10 or 3.11 is available
where py >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo [1/4] Found Python Launcher (py). Checking versions...
    py -3.11 --version >nul 2>nul
    if !ERRORLEVEL! equ 0 (
        set "PY_CMD=py -3.11"
        echo Using Python 3.11
    ) else (
        py -3.10 --version >nul 2>nul
        if !ERRORLEVEL! equ 0 (
            set "PY_CMD=py -3.10"
            echo Using Python 3.10
        ) else (
            set "PY_CMD=python"
            echo Using default python
        )
    )
) else (
    set "PY_CMD=python"
    echo Using default python
)

:: Create Virtual Environment if not exists
if not exist "venv\Scripts\activate.bat" (
    echo [2/4] Creating virtual environment in .\venv ...
    %PY_CMD% -m venv venv
    if %ERRORLEVEL% neq 0 (
        echo [ERROR] Failed to create virtual environment!
        pause
        exit /b %ERRORLEVEL%
    )
    echo [OK] Virtual environment created.
) else (
    echo [2/4] Virtual environment .\venv already exists.
)

:: Activate Virtual Environment
call venv\Scripts\activate.bat

:: Upgrade pip
echo [3/4] Upgrading pip...
python -m pip install --upgrade pip

:: Install PyTorch with CUDA support
echo [4/4] Installing PyTorch with CUDA support and dependencies...
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install -r requirements.txt

echo =======================================================
echo   Setup completed successfully!
echo   Run "run_server.bat" to start the server on port 3978.
echo =======================================================
pause
