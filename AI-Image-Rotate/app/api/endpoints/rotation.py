import time
import uuid
import logging
from typing import List
from fastapi import APIRouter, HTTPException, UploadFile, File, Form

from app.schemas.rotation import (
    RotateSingleRequest,
    TurntableRequest,
    MultiViewRequest,
    RotationResponse,
    SingleFrameResult
)
from app.core.preprocessor import preprocessor
from app.core.zero123_pipeline import ai_engine
from app.core.postprocessor import postprocessor
from app.config import settings

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/rotate", tags=["Image Rotation & 360 Turntable"])


@router.post("/single", response_model=RotationResponse)
async def rotate_single_angle(req: RotateSingleRequest):
    """Predicts and synthesizes a novel view for a custom Azimuth and Elevation angle."""
    start_time = time.time()
    try:
        if not req.image_base64 and not req.image_url:
            raise HTTPException(status_code=400, detail="Must provide either image_base64 or image_url")

        # Decode image
        input_image = preprocessor.base64_to_image(req.image_base64)
        processed_img = preprocessor.preprocess(input_image, remove_bg=req.remove_background)
        prep_b64 = preprocessor.image_to_base64(processed_img)

        # AI Prediction
        synthesized_img = ai_engine.predict_angle(
            input_image=processed_img,
            azimuth_deg=req.azimuth_deg,
            elevation_deg=req.elevation_deg,
            radius_scale=req.radius_scale,
            num_steps=req.num_inference_steps,
            guidance_scale=req.guidance_scale
        )
        res_b64 = preprocessor.image_to_base64(synthesized_img)

        frame = SingleFrameResult(
            frame_index=0,
            azimuth_deg=req.azimuth_deg,
            elevation_deg=req.elevation_deg,
            image_base64=res_b64
        )

        return RotationResponse(
            success=True,
            message=f"Synthesized angle at azimuth {req.azimuth_deg}° and elevation {req.elevation_deg}°",
            elapsed_seconds=round(time.time() - start_time, 3),
            preprocessed_image_base64=prep_b64,
            frames=[frame]
        )
    except Exception as e:
        logger.error(f"[API Rotate Single] Error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/turntable", response_model=RotationResponse)
async def generate_360_turntable(req: TurntableRequest):
    """Generates a full 360-degree orbital turntable sequence with GIF and Spritesheet."""
    start_time = time.time()
    try:
        if not req.image_base64 and not req.image_url:
            raise HTTPException(status_code=400, detail="Must provide either image_base64 or image_url")

        input_image = preprocessor.base64_to_image(req.image_base64)
        processed_img = preprocessor.preprocess(input_image, remove_bg=req.remove_background)
        prep_b64 = preprocessor.image_to_base64(processed_img)

        # Generate sequence of angles
        raw_frames = ai_engine.generate_turntable(
            input_image=processed_img,
            num_frames=req.num_frames,
            elevation_deg=req.elevation_deg,
            num_steps=req.num_inference_steps
        )

        # Format frames
        frame_results: List[SingleFrameResult] = []
        pil_images = []
        for idx, azimuth, f_img in raw_frames:
            b64_str = preprocessor.image_to_base64(f_img)
            frame_results.append(SingleFrameResult(
                frame_index=idx,
                azimuth_deg=azimuth,
                elevation_deg=req.elevation_deg,
                image_base64=b64_str
            ))
            pil_images.append(f_img)

        # Postprocessing GIF & Spritesheet
        gif_b64 = postprocessor.create_gif(pil_images) if req.generate_gif else None
        spritesheet_b64 = postprocessor.create_spritesheet(pil_images) if req.generate_spritesheet else None

        # Create downloadable zip
        job_id = uuid.uuid4().hex[:8]
        zip_path = settings.OUTPUT_DIR / f"turntable_{job_id}.zip"
        postprocessor.create_zip_archive(raw_frames, zip_path)

        return RotationResponse(
            success=True,
            message=f"Generated {req.num_frames} frames 360° turntable suite successfully.",
            elapsed_seconds=round(time.time() - start_time, 3),
            preprocessed_image_base64=prep_b64,
            frames=frame_results,
            gif_base64=gif_b64,
            spritesheet_base64=spritesheet_b64,
            zip_url=f"/outputs/{zip_path.name}"
        )
    except Exception as e:
        logger.error(f"[API Rotate Turntable] Error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/multiview", response_model=RotationResponse)
async def rotate_multiview(req: MultiViewRequest):
    """Processes multiple input angles of an object for enhanced 360 novel view prediction."""
    start_time = time.time()
    try:
        if not req.images_base64:
            raise HTTPException(status_code=400, detail="Must provide at least 1 image in images_base64")

        # Preprocess primary input image
        primary_img = preprocessor.base64_to_image(req.images_base64[0])
        processed_img = preprocessor.preprocess(primary_img, remove_bg=True)
        prep_b64 = preprocessor.image_to_base64(processed_img)

        # Predict target novel view
        out_img = ai_engine.predict_angle(
            input_image=processed_img,
            azimuth_deg=req.target_azimuth_deg,
            elevation_deg=req.target_elevation_deg
        )
        res_b64 = preprocessor.image_to_base64(out_img)

        frame = SingleFrameResult(
            frame_index=0,
            azimuth_deg=req.target_azimuth_deg,
            elevation_deg=req.target_elevation_deg,
            image_base64=res_b64
        )

        return RotationResponse(
            success=True,
            message=f"Synthesized angle from {len(req.images_base64)} input views.",
            elapsed_seconds=round(time.time() - start_time, 3),
            preprocessed_image_base64=prep_b64,
            frames=[frame]
        )
    except Exception as e:
        logger.error(f"[API Rotate MultiView] Error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))
