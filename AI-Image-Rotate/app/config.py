import os
from pathlib import Path
from pydantic import BaseModel

BASE_DIR = Path(__file__).resolve().parent.parent
OUTPUTS_DIR = BASE_DIR / "outputs"
TEMP_DIR = BASE_DIR / "temp"
MODELS_DIR = BASE_DIR / "models_cache"

# Ensure runtime directories exist
for directory in (OUTPUTS_DIR, TEMP_DIR, MODELS_DIR):
    directory.mkdir(parents=True, exist_ok=True)


class Settings(BaseModel):
    """Global configuration settings for AI Image Rotate Server."""

    # Server settings
    HOST: str = "0.0.0.0"
    PORT: int = 3978
    APP_NAME: str = "AI 360 Image Rotate API"
    APP_VERSION: str = "1.0.0"
    DEBUG: bool = False

    # Hardware target (Optimized for RTX 3060 12GB)
    DEVICE: str = "cuda"  # Auto falls back to cpu if cuda not available
    HALF_PRECISION: bool = True  # FP16 to fit easily within 12GB VRAM
    VRAM_LIMIT_GB: float = 12.0
    BATCH_SIZE: int = 4

    # AI Model Settings
    DEFAULT_MODEL_ID: str = "stabilityai/stable-zero123"
    FALLBACK_MODEL_ID: str = "sudo-ai/zero123plus-v1.1"
    IMAGE_SIZE: int = 512

    # Storage
    OUTPUT_DIR: Path = OUTPUTS_DIR
    TEMP_DIR: Path = TEMP_DIR
    MODELS_CACHE_DIR: Path = MODELS_DIR


settings = Settings()
