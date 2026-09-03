from fastapi import APIRouter, Query
from app.core.ai_motion_model import ai_model_instance
from app.core.gpu_manager import gpu_manager

router = APIRouter(prefix="/model", tags=["Model Management"])


@router.get("/status", summary="Get AI Model & VRAM Status")
async def get_model_status():
    """
    Returns whether AI diffusion model is loaded in VRAM and available GPU memory.
    """
    return ai_model_instance.get_status()


@router.post("/load", summary="Load AI Model into GPU VRAM")
async def load_model_to_vram(model_type: str = Query("animatediff", description="Model engine: animatediff or svd")):
    """
    Explicitly triggers loading model weights into RTX 3060 12GB VRAM.
    """
    try:
        ai_model_instance.load_pipeline(model_type)
        return {
            "success": True,
            "message": f"AI Model ({model_type}) loaded into VRAM successfully",
            "status": ai_model_instance.get_status()
        }
    except Exception as e:
        return {
            "success": False,
            "message": f"Failed to load model into VRAM: {str(e)}",
            "status": ai_model_instance.get_status()
        }


@router.post("/unload", summary="Unload Model & Release GPU VRAM")
async def unload_model_from_vram():
    """
    Releases model from GPU memory and runs garbage collection to free VRAM.
    """
    unloaded = ai_model_instance.unload_from_vram()
    gpu_manager.clean_memory()
    return {
        "success": True,
        "unloaded": unloaded,
        "message": "VRAM cache cleared and model unloaded",
        "status": ai_model_instance.get_status()
    }
