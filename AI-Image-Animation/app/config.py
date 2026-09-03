import os
from pathlib import Path
from typing import List

# Base Paths
BASE_DIR = Path(__file__).resolve().parent.parent
APP_DIR = BASE_DIR / "app"
STATIC_DIR = APP_DIR / "static"
OUTPUTS_DIR = BASE_DIR / "outputs"
TEMP_DIR = BASE_DIR / "temp"
CACHE_DIR = BASE_DIR / "cache"

# Ensure runtime directories exist
for directory in [OUTPUTS_DIR, TEMP_DIR, CACHE_DIR]:
    directory.mkdir(parents=True, exist_ok=True)

# Server Configuration
SERVER_HOST = os.getenv("SERVER_HOST", "0.0.0.0")
SERVER_PORT = int(os.getenv("SERVER_PORT", "3979"))
SERVER_TITLE = "AI Image Animation Studio API"
SERVER_DESCRIPTION = "API & Web Studio for generating fluid hair, cloth, and ambient wind animations from static images."
SERVER_VERSION = "1.0.0"

# CORS Configuration for external website integration
CORS_ORIGINS: List[str] = ["*"]
CORS_ALLOW_CREDENTIALS: bool = True
CORS_ALLOW_METHODS: List[str] = ["*"]
CORS_ALLOW_HEADERS: List[str] = ["*"]

# Hardware & RTX 3060 12GB VRAM Optimization Settings
TARGET_GPU_VRAM_GB = 12.0
MAX_VRAM_ALLOCATION_RATIO = 0.85  # Safe margin for 12GB
ENABLE_FP16 = True
ENABLE_XFORMERS = True
ENABLE_CPU_OFFLOAD = False  # Keep in full GPU VRAM for RTX 3060 12GB (15x faster)
ENABLE_VAE_SLICING = True

# Task & Processing Defaults
DEFAULT_FPS = 30
DEFAULT_DURATION = 3.0  # seconds
MAX_DURATION = 10.0
MAX_IMAGE_DIMENSION = 2048
DEFAULT_PREVIEW_SIZE = (512, 512)
TASK_TIMEOUT_SECONDS = 300
CLEANUP_TEMP_FILES_HOURS = 24
