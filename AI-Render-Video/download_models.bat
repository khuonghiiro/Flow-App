@echo off
title AI 3D Studio - Download Sample 3D Models
color 0A
cls

echo ==============================================================================
echo                 AI 3D STUDIO - TAI TAI NGUYEN MODEL 3D MAU
echo ==============================================================================
echo.

cd /d "%~dp0"

echo Dang tai cac model 3D mau (VRM Characters, Props, Weapons)...
echo.

node download_sample_models.cjs

echo.
echo ==============================================================================
echo   HOAN TAT! CAC FILE 3D DA DUOC LUU VAO THU MUC assets/
echo ==============================================================================
echo.
pause
