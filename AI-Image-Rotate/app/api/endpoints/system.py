import logging
from typing import Dict, Any, List, Optional
from pydantic import BaseModel, Field
from fastapi import APIRouter

from app.schemas.rotation import SystemStatusResponse
from app.core.device_manager import device_manager
from app.core.zero123_pipeline import ai_engine
from app.config import settings

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/system", tags=["System & AI Model Manager"])


class LoadModelRequest(BaseModel):
    model_id: str = Field(default="sudo-ai/zero123plus-v1.1", description="HuggingFace model repo or local path")


@router.get("/status", response_model=SystemStatusResponse)
async def get_system_status():
    """Returns GPU status, VRAM consumption, and active AI model state."""
    gpu_info = device_manager.get_gpu_info()
    return SystemStatusResponse(
        status="healthy",
        device=gpu_info["device"],
        gpu_name=gpu_info["gpu_name"],
        vram_total_gb=gpu_info["vram_total_gb"],
        vram_used_gb=gpu_info["vram_used_gb"],
        vram_free_gb=gpu_info["vram_free_gb"],
        model_loaded=ai_engine.is_loaded,
        model_name=ai_engine.model_name,
        half_precision=settings.HALF_PRECISION,
        port=settings.PORT
    )


@router.get("/model-status")
async def get_model_status():
    """Returns real-time progress and status of model loading into VRAM."""
    status = ai_engine.get_status()
    gpu_info = device_manager.get_gpu_info()
    status["gpu_info"] = gpu_info
    return status


@router.post("/load-model")
async def load_model_endpoint(req: LoadModelRequest):
    """Triggers asynchronous model weight loading into RTX 3060 VRAM."""
    ai_engine.load_model_weights_async(req.model_id)
    return {
        "success": True,
        "message": f"Bắt đầu nạp model {req.model_id} vào VRAM GPU...",
        "status": ai_engine.get_status()
    }


@router.post("/unload-model")
async def unload_model_endpoint():
    """Unloads model and purges GPU VRAM cache."""
    ai_engine.unload_model()
    return {
        "success": True,
        "message": "Đã giải phóng VRAM GPU thành công.",
        "status": ai_engine.get_status()
    }


@router.get("/models")
async def list_available_models():
    """Lists available supported 3D Novel View Synthesis models."""
    return {
        "active_model": ai_engine.model_name,
        "is_loaded": ai_engine.is_loaded,
        "available_models": [
            {
                "id": "sudo-ai/zero123plus-v1.1",
                "name": "Zero123++ v1.1 (Khuyên dùng - 3D Multi-View RTX 3060)",
                "size": "4.1 GB",
                "recommended": True
            },
            {
                "id": "stabilityai/stable-zero123",
                "name": "Stable Zero123 (High Quality Single View)",
                "size": "3.8 GB",
                "recommended": True
            },
            {
                "id": "ashawkey/zero123-xl-diffusers",
                "name": "Zero123-XL (Large Novel View Synthesis)",
                "size": "4.5 GB",
                "recommended": False
            }
        ]
    }


@router.post("/clean-vram")
async def clean_vram_cache():
    """Manually purges PyTorch CUDA cache and runs garbage collection."""
    device_manager.clean_vram()
    gpu_info = device_manager.get_gpu_info()
    return {
        "success": True,
        "message": "VRAM cache cleared successfully.",
        "vram_free_gb": gpu_info["vram_free_gb"]
    }
