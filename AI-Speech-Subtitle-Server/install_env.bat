@echo off
setlocal enabledelayedexpansion
title [FlowMy] Cai dat Moi truong AI Speech-to-Subtitle Server
color 0B

echo ===============================================================================
echo      CAI DAT MOI TRUONG DOC LAP CHO AI SPEECH-TO-SUBTITLE SERVER
echo ===============================================================================
echo.

cd /d "%~dp0"

:: 1. Tim phien ban Python toi uu nhat (Uu tien Python 3.11 hoac 3.12)
echo [1/5] Tim kiem phien ban Python tuong thich (Uu tien Python 3.11 / 3.12)...

set "PYTHON_CMD="

:: Kiem tra qua Python Launcher (py)
py -3.11 --version >nul 2>&1
if not errorlevel 1 (
    set "PYTHON_CMD=py -3.11"
    echo    [+] Tim thay Python 3.11 qua py launcher!
    goto :PYTHON_FOUND
)

py -3.12 --version >nul 2>&1
if not errorlevel 1 (
    set "PYTHON_CMD=py -3.12"
    echo    [+] Tim thay Python 3.12 qua py launcher!
    goto :PYTHON_FOUND
)

py -3.10 --version >nul 2>&1
if not errorlevel 1 (
    set "PYTHON_CMD=py -3.10"
    echo    [+] Tim thay Python 3.10 qua py launcher!
    goto :PYTHON_FOUND
)

:: Kiem tra duong dan truc tiep
if exist "%LocalAppData%\Programs\Python\Python311\python.exe" (
    set "PYTHON_CMD=%LocalAppData%\Programs\Python\Python311\python.exe"
    echo    [+] Tim thay Python 3.11 tai AppData!
    goto :PYTHON_FOUND
)

if exist "C:\Python312\python.exe" (
    set "PYTHON_CMD=C:\Python312\python.exe"
    echo    [+] Tim thay Python 3.12 tai C:\Python312!
    goto :PYTHON_FOUND
)

if exist "%LocalAppData%\Programs\Python\Python312\python.exe" (
    set "PYTHON_CMD=%LocalAppData%\Programs\Python\Python312\python.exe"
    echo    [+] Tim thay Python 3.12 tai AppData!
    goto :PYTHON_FOUND
)

:: Fallback kiem tra lenh python mac dinh
python --version >nul 2>&1
if not errorlevel 1 (
    set "PYTHON_CMD=python"
    echo    [+] Su dung lenh python mac dinh trong PATH.
    goto :PYTHON_FOUND
)

color 0C
echo [ERROR] Khong tim thay Python 3.10 / 3.11 / 3.12 tren may!
echo Vui long cai dat Python 3.11 hoac 3.12 de dam bao tuong thich AI.
pause
exit /b 1

:PYTHON_FOUND
echo    [+] Trinh thuc thi duoc chon: %PYTHON_CMD%
%PYTHON_CMD% --version

:: 2. Tao virtual environment .venv
echo.
echo [2/5] Khoi tao Virtual Environment (.venv) co lap...
if not exist ".venv\Scripts\python.exe" (
    if exist ".venv" (
        echo    [+] Dang don dep thu muc .venv cu...
        rmdir /s /q .venv >nul 2>&1
    )
    %PYTHON_CMD% -m venv .venv
    if errorlevel 1 (
        color 0C
        echo [ERROR] Khong the tao virtual environment bang %PYTHON_CMD%!
        pause
        exit /b 1
    )
    echo    [+] Da tao xong .venv moi bang %PYTHON_CMD%
) else (
    echo    [+] Da ton tai .venv hop le, tiep tuc su dung.
)

call .venv\Scripts\activate.bat

:: Cau hinh Index Mirror va Trusted-Host de tranh loi 403 Forbidden va SSL Cert
.venv\Scripts\python.exe -m pip config set global.index-url "https://mirrors.aliyun.com/pypi/simple/" >nul 2>&1
.venv\Scripts\python.exe -m pip config set global.trusted-host "mirrors.aliyun.com download.pytorch.org pypi.org files.pythonhosted.org huggingface.co" >nul 2>&1

:: 3. Kiem tra card do hoa NVIDIA GPU de cai Torch CUDA
echo.
echo [3/5] Kiem tra phan cung do hoa (NVIDIA GPU / CUDA)...
nvidia-smi >nul 2>&1
if errorlevel 1 (
    echo    [+] Khong tim thay GPU NVIDIA hoac chua co driver CUDA.
    echo    [+] He thong se cai dat PyTorch phien ban CPU...
    .venv\Scripts\python.exe -m pip install "torch>=2.2.0,<=2.4.1" "torchaudio>=2.2.0,<=2.4.1" --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://mirrors.aliyun.com/pypi/simple/ --trusted-host download.pytorch.org --trusted-host mirrors.aliyun.com
) else (
    echo    [+] Phat hien GPU NVIDIA CUDA!
    echo    [+] Dang cai dat PyTorch phien ban CUDA 12.1 de toi uu toc do GPU toi da...
    .venv\Scripts\python.exe -m pip install "torch>=2.2.0,<=2.4.1" "torchaudio>=2.2.0,<=2.4.1" --index-url https://download.pytorch.org/whl/cu121 --extra-index-url https://mirrors.aliyun.com/pypi/simple/ --trusted-host download.pytorch.org --trusted-host mirrors.aliyun.com
)

:: 4. Cai dat cac thu vien Backend Python
echo.
echo [4/5] Cai dat cac thu vien Backend AI tu backend\requirements.txt...
.venv\Scripts\python.exe -m pip install -r backend\requirements.txt -i https://mirrors.aliyun.com/pypi/simple/ --trusted-host mirrors.aliyun.com

:: 5. Cai dat frontend dependencies bang pnpm (neu co)
echo.
echo [5/5] Kiem tra pnpm de toi uu quan ly thu vien Web UI...
where pnpm >nul 2>&1
if errorlevel 1 (
    where npx >nul 2>&1
    if not errorlevel 1 (
        echo    [+] Dang chay npx pnpm install...
        call npx pnpm install --ignore-scripts
    ) else (
        echo    [+] pnpm chua cai san, su dung giao dien Web tinh san co.
    )
) else (
    echo    [+] Phat hien pnpm! Dang chay pnpm install...
    call pnpm install --ignore-scripts
)

echo.
echo ===============================================================================
echo   [*] CAI DAT HOAN TAT THANH CONG!
echo   - De chay Server kem giao dien Web UI: Chay file 'start_app_with_ui.bat'
echo   - De chi chay Server API ngam cho FlowMy: Chay file 'start_server_only.bat'
echo ===============================================================================
echo.
