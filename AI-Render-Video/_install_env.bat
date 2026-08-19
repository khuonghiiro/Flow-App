@echo off
title AI 3D Animation Studio - Cai Dat Moi Truong
color 0B
cls

echo ==============================================================================
echo                 AI 3D ANIMATION STUDIO - CAI DAT MOI TRUONG
echo ==============================================================================
echo.

cd /d "%~dp0"

echo [1/3] Kiem tra Node.js va npm...
where node >nul 2>nul
if %errorlevel% neq 0 (
    color 0C
    echo [LOI] Khong tim thay Node.js tren may tinh!
    echo Vui long cai dat Node.js LTS tu https://nodejs.org/ truoc.
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('node -v') do set NODE_VER=%%i
for /f "tokens=*" %%i in ('npm -v') do set NPM_VER=%%i
echo - Node.js phien ban: %NODE_VER%
echo - npm phien ban: %NPM_VER%
echo.

echo [2/3] Dang cai dat thu vien npm...
call npm.cmd install
if %errorlevel% neq 0 (
    color 0C
    echo [LOI] Qua trinh npm install gap loi! Vui long kiem tra mang.
    pause
    exit /b 1
)
echo - Da cai dat thanh cong tat ca thu vien!
echo.

echo [3/3] Kiem tra bien dich TypeScript...
call npm.cmd run build
if %errorlevel% neq 0 (
    color 0E
    echo [CANH BAO] Kiem tra build co canh bao.
) else (
    echo - Bien dich TypeScript thanh cong hoan toan!
)

echo.
echo ==============================================================================
echo       CAI DAT HOAN TAT! BAN CO THE CHAY FILE _start_studio.bat DE BAT DAU.
echo ==============================================================================
echo.
pause
