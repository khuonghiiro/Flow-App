#!/usr/bin/env bash
# Setup script for image-to-rig-pipeline on Linux/WSL2
# Optimized for NVIDIA RTX 3060 12GB (Ampere CUDA 12.1+)

set -e

echo "======================================================================"
echo "  Image-to-Rig Pipeline Environment Setup (Linux/WSL2)"
echo "======================================================================"

if ! command -v python3 &> /dev/null; then
    echo "[ERROR] python3 could not be found. Please install Python 3.10 or 3.11."
    exit 1
fi

if [ ! -d "venv" ]; then
    echo "[INFO] Creating virtual environment 'venv'..."
    python3 -m venv venv
else
    echo "[INFO] Virtual environment 'venv' exists."
fi

source venv/bin/activate

echo "[INFO] Upgrading pip..."
pip install --upgrade pip setuptools wheel

echo "[INFO] Installing PyTorch with CUDA 12.1 support..."
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

echo "[INFO] Installing project dependencies..."
pip install -r requirements.txt

echo "======================================================================"
echo "[SUCCESS] Environment setup complete!"
echo "To start Gradio Web UI: run 'python app.py'"
echo "To start FastAPI REST Server: run 'python server.py'"
echo "======================================================================"
