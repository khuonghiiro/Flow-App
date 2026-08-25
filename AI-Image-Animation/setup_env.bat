@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   AI Image Animation - Environment Setup (RTX 3060 12GB)
echo   Port: 3979
echo =======================================================

cd /d "%~dp0"

:: 1. Detect best Python version (3.10 or 3.11 preferred for AI/PyTorch)
set "PY_CMD="

if exist "%LOCALAPPDATA%\Programs\Python\Python310\python.exe" (
    set "PY_CMD="%LOCALAPPDATA%\Programs\Python\Python310\python.exe""
    echo [1/4] Found Python 3.10 in AppData.
) else (
    where py >nul 2>nul
    if !ERRORLEVEL! equ 0 (
        py -3.10 --version >nul 2>nul
        if !ERRORLEVEL! equ 0 (
            set "PY_CMD=py -3.10"
            echo [1/4] Using Python 3.10 via py launcher.
        ) else (
            py -3.11 --version >nul 2>nul
            if !ERRORLEVEL! equ 0 (
                set "PY_CMD=py -3.11"
                echo [1/4] Using Python 3.11 via py launcher.
            ) else (
                set "PY_CMD=python"
                echo [1/4] Using default python.
            )
        )
    ) else (
        set "PY_CMD=python"
        echo [1/4] Using default python.
    )
)

:: 2. Create Virtual Environment if not exists
if not exist "venv\Scripts\activate.bat" (
    echo [2/4] Creating virtual environment in .\venv ...
    %PY_CMD% -m venv venv
    if !ERRORLEVEL! neq 0 (
        echo [ERROR] Failed to create virtual environment!
        pause
        exit /b !ERRORLEVEL!
    )
    echo [OK] Virtual environment created successfully.
) else (
    echo [2/4] Virtual environment .\venv already exists.
)

:: 3. Upgrade pip
echo [3/4] Upgrading pip...
.\venv\Scripts\python.exe -m pip install --upgrade pip

:: 4. Install dependencies
echo [4/4] Installing dependencies...
.\venv\Scripts\pip.exe install -r requirements.txt

echo =======================================================
echo   Setup completed successfully!
echo   Run "run_server.bat" to start the server on Port 3979.
echo =======================================================
pause
