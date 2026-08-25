from fastapi import APIRouter
from app.core.gpu_manager import gpu_manager
from app.schemas.response_models import GPUStatusResponse

router = APIRouter(prefix="/health", tags=["System & GPU"])


@router.get("", response_model=GPUStatusResponse, summary="Get GPU & VRAM status")
async def get_system_health():
    """
    Returns GPU model, available VRAM, and hardware optimization indicators.
    Designed for NVIDIA GeForce RTX 3060 12GB VRAM.
    """
    return gpu_manager.get_status()
