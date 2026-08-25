import time
import logging
from pydantic import BaseModel, Field
from fastapi import APIRouter, HTTPException

from app.core.preprocessor import preprocessor

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/preprocess", tags=["Image Preprocessing"])


class BackgroundRemovalRequest(BaseModel):
    image_base64: str = Field(..., description="Base64 encoded input image")
    target_size: int = Field(512, ge=128, le=1024, description="Target canvas size (square 1:1)")


class BackgroundRemovalResponse(BaseModel):
    success: bool
    elapsed_seconds: float
    image_base64: str


@router.post("/remove-bg", response_model=BackgroundRemovalResponse)
async def remove_background_endpoint(req: BackgroundRemovalRequest):
    """Removes background and centers the subject on a transparent square canvas."""
    start_time = time.time()
    try:
        input_image = preprocessor.base64_to_image(req.image_base64)
        processed_img = preprocessor.preprocess(input_image, remove_bg=True, target_size=req.target_size)
        res_b64 = preprocessor.image_to_base64(processed_img)

        return BackgroundRemovalResponse(
            success=True,
            elapsed_seconds=round(time.time() - start_time, 3),
            image_base64=res_b64
        )
    except Exception as e:
        logger.error(f"[API Preprocess BG] Error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))
