@echo off
chcp 65001 >nul
title [FlowMy] Cai dat Moi truong AI Speech-to-Subtitle Server
color 0B

echo ===============================================================================
echo     🎙️  CAI DAT MOI TRUONG DUC LAP CHO AI SPEECH-TO-SUBTITLE SERVER
echo ===============================================================================
echo.

cd /d "%~dp0"

:: 1. Kiem tra Python
echo [1/5] Kiem tra moi truong Python...
python --version >nul 2>&1
if errorlevel 1 (
    color 0C
    echo [ERROR] Khong tim thay Python tren may cua ban!
    echo Vui long cai dat Python 3.10 tro len (tich vao 'Add Python to PATH') roi chay lai script nay.
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('python --version') do echo    -> %%i

:: 2. Tao virtual environment .venv de cach ly thu vien
echo.
echo [2/5] Khoi tao Virtual Environment (.venv) co lap...
if not exist ".venv" (
    python -m venv .venv
    echo    -> Da tao xong thu muc .venv
) else (
    echo    -> Da ton tai .venv, tiep tuc su dung.
)

call .venv\Scripts\activate.bat

:: 3. Kiem tra card do hoa NVIDIA GPU de cai Torch CUDA
echo.
echo [3/5] Kiem tra phan cung do hoa (NVIDIA GPU / CUDA)...
nvidia-smi >nul 2>&1
if errorlevel 1 (
    echo    -> Khong tim thay GPU NVIDIA hoac khong co driver CUDA.
    echo    -> He thong se cai dat PyTorch phien ban CPU (van chay tot cho SenseVoice va Faster-Whisper int8).
    pip install --upgrade pip
    pip install torch torchaudio --index-url https://download.pytorch.org/whl/cpu
) else (
    echo    -> Phat hien GPU NVIDIA CUDA!
    echo    -> Dang cai dat PyTorch phien ban CUDA 12.1 de toi uu toc do GPU toi da...
    pip install --upgrade pip
    pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu121
)

:: 4. Cai dat cac thu vien Backend Python
echo.
echo [4/5] Cai dat cac thu vien Backend AI tu backend\requirements.txt...
pip install -r backend\requirements.txt

:: 5. Cai dat frontend dependencies bang pnpm (neu co)
echo.
echo [5/5] Kiem tra pnpm de toi uu quan ly thu vien Web UI...
where pnpm >nul 2>&1
if errorlevel 1 (
    where npx >nul 2>&1
    if not errorlevel 1 (
        echo    -> Dang chay npx pnpm install...
        call npx pnpm install --ignore-scripts
    ) else (
        echo    -> pnpm chua cai san, su dung giao dien Web tinh san co.
    )
) else (
    echo    -> Phat hien pnpm! Dang chay pnpm install...
    call pnpm install --ignore-scripts
)

echo.
echo ===============================================================================
echo  ✅ CAI DAT HOAN TAT THANH CONG!
echo  - De chay Server kem giao dien Web UI: Chay file 'start_app_with_ui.bat'
echo  - De chi chay Server API ngam cho FlowMy: Chay file 'start_server_only.bat'
echo ===============================================================================
echo.
pause
