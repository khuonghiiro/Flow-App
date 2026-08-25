# AI Image Animation - PowerShell Run Script
$Host.UI.RawUI.WindowTitle = "AI Image Animation Server (Port 3979)"

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  Starting AI Image Animation Server (Port 3979)" -ForegroundColor Cyan
Write-Host "  Optimized for NVIDIA RTX 3060 12GB VRAM" -ForegroundColor Cyan
Write-Host "  Studio UI: http://localhost:3979" -ForegroundColor Green
Write-Host "  API Docs:  http://localhost:3979/docs" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Cyan

Set-Location -Path $PSScriptRoot

$pythonExe = "python"
if (Test-Path "venv\Scripts\python.exe") {
    $pythonExe = ".\venv\Scripts\python.exe"
} else {
    Write-Host "[WARNING] Virtual environment 'venv' not found! Using system python." -ForegroundColor Yellow
}

& $pythonExe -m uvicorn app.main:app --host 0.0.0.0 --port 3979 --reload
