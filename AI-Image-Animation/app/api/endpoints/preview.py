import time
from fastapi import APIRouter, HTTPException
from app.schemas.request_models import SingleFramePreviewRequest
from app.schemas.response_models import PreviewFrameResponse
from app.utils.image_processing import (
    decode_base64_image,
    decode_base64_mask,
    encode_image_base64,
    process_feather_mask,
    resize_maintaining_aspect,
)
from app.core.vector_field import VectorFieldGenerator
from app.core.flow_engine import FlowWarpEngine

router = APIRouter(prefix="/preview", tags=["Real-time Preview"])


@router.post("/frame", response_model=PreviewFrameResponse, summary="Compute single warped preview frame")
async def compute_preview_frame(req: SingleFramePreviewRequest):
    """
    Computes a fast warped frame at normalized phase [0.0, 1.0] for instant timeline scrubbing.
    """
    start_time = time.time()
    try:
        # Decode and downscale for high responsiveness
        raw_image = decode_base64_image(req.image)
        resized_img, _ = resize_maintaining_aspect(raw_image, max_dim=req.preview_size)
        h, w = resized_img.shape[:2]
        
        # Process mask
        mask = decode_base64_mask(req.mask, (h, w))
        feathered_mask = process_feather_mask(mask, blur_radius=12)
        
        # Calculate dense flow field
        flow_x, flow_y = VectorFieldGenerator.generate_flow_field(h, w, req.vectors, req.pins)
        
        # Warp single frame
        warped = FlowWarpEngine.warp_frame(
            source_image=resized_img,
            flow_x=flow_x,
            flow_y=flow_y,
            mask=feathered_mask,
            phase=req.phase,
            wind_strength=req.wind_strength,
            wave_frequency=req.wave_frequency,
            turbulence=req.turbulence,
            flutter_scale=req.flutter_scale
        )
        
        encoded_png = encode_image_base64(warped, format="PNG")
        elapsed_ms = (time.time() - start_time) * 1000.0
        
        return PreviewFrameResponse(
            preview_image=encoded_png,
            phase=req.phase,
            processing_time_ms=round(elapsed_ms, 2)
        )
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Preview computation failed: {str(e)}")
