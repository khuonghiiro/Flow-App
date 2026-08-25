@echo off
title AI Image Animation Server (Port 3979)
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo =======================================================
echo   Starting AI Image Animation Server (Port 3979)
echo   Optimized for NVIDIA RTX 3060 12GB VRAM
echo   Studio UI: http://localhost:3979
echo   API Docs:  http://localhost:3979/docs
echo =======================================================

set "PY_EXE=%~dp0venv\Scripts\python.exe"

if exist "%PY_EXE%" goto :START_SERVER

echo [WARNING] Local virtual environment 'venv' not found!
echo Automatically running setup_env.bat to initialize environment...
echo.
call "%~dp0setup_env.bat"

if not exist "%PY_EXE%" (
    echo [ERROR] Failed to find or initialize virtual environment.
    goto :SERVER_END
)

:START_SERVER
echo [OK] Starting server using local venv on http://localhost:3979 ...
"%PY_EXE%" -m uvicorn app.main:app --host 0.0.0.0 --port 3979 --reload
if !ERRORLEVEL! neq 0 (
    echo.
    echo [ERROR] Server exited with code !ERRORLEVEL!.
)

:SERVER_END
echo.
echo =======================================================
echo   Server stopped. Press any key to close this window.
echo =======================================================
pause


