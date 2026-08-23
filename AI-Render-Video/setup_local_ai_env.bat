@echo off
chcp 65001 >nul
title Cai Dat Moi Truong AI Cuc Bo [RTX 3060]
cd /d "%~dp0"

echo ===================================================================
echo   CAI DAT MOI TRUONG AI PYTHON CUC BO CHO DU AN [.VENV + RTX 3060]
echo ===================================================================
echo.
echo Du an se tao thu muc .venv rieng biet va thu muc models\ai_matting
echo khong anh huong den moi truong he thong hoac cac du an khac.
echo.

if exist ".venv\Scripts\python.exe" goto INSTALL_PACKAGES

echo [*] Dang tao moi truong ao .venv...
python -m venv .venv

:INSTALL_PACKAGES
echo [*] Dang cai dat rembg[gpu], onnxruntime-gpu, pillow vao .venv...
.venv\Scripts\python.exe -m pip install --upgrade pip
.venv\Scripts\python.exe -m pip install rembg[gpu] onnxruntime-gpu pillow

if not exist "models\ai_matting" mkdir "models\ai_matting"

echo.
echo ===================================================================
echo   HOAN TAT! MOI TRUONG AI DA DUOC DONG GOI SAN SANG TRONG SOURCE!
echo   Model tai ve se nam tai: .\models\ai_matting\
echo ===================================================================
echo.
pause
