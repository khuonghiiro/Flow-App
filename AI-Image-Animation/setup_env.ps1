# AI Image Animation - PowerShell Setup Script
$Host.UI.RawUI.WindowTitle = "AI Image Animation - Setup (Port 3979)"

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  AI Image Animation - Environment Setup (RTX 3060 12GB)" -ForegroundColor Cyan
Write-Host "  Port: 3979" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

Set-Location -Path $PSScriptRoot

$pythonCmd = "python"
if (Get-Command "py" -ErrorAction SilentlyContinue) {
    if (& py -3.11 --version 2>$null) {
        $pythonCmd = "py -3.11"
        Write-Host "[1/4] Using Python 3.11" -ForegroundColor Green
    } elseif (& py -3.10 --version 2>$null) {
        $pythonCmd = "py -3.10"
        Write-Host "[1/4] Using Python 3.10" -ForegroundColor Green
    }
}

if (-not (Test-Path "venv\Scripts\Activate.ps1")) {
    Write-Host "[2/4] Creating virtual environment in .\venv ..." -ForegroundColor Yellow
    Invoke-Expression "$pythonCmd -m venv venv"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Failed to create virtual environment!" -ForegroundColor Red
        Exit $LASTEXITCODE
    }
    Write-Host "[OK] Virtual environment created." -ForegroundColor Green
} else {
    Write-Host "[2/4] Virtual environment already exists." -ForegroundColor Green
}

Write-Host "[3/4] Activating environment and upgrading pip..." -ForegroundColor Yellow
& ".\venv\Scripts\python.exe" -m pip install --upgrade pip

Write-Host "[4/4] Installing PyTorch with CUDA 12.1 and dependencies..." -ForegroundColor Yellow
& ".\venv\Scripts\pip.exe" install torch torchvision --index-url https://download.pytorch.org/whl/cu121
& ".\venv\Scripts\pip.exe" install -r requirements.txt

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  Setup completed! Run .\run_server.ps1 to start server." -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Cyan
