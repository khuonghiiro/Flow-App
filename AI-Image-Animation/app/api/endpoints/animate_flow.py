import asyncio
import os
from fastapi import APIRouter, BackgroundTasks, HTTPException
from app.schemas.request_models import FlowAnimateRequest, ExportFormat
from app.schemas.response_models import AnimateResponse
from app.utils.file_manager import generate_unique_id, get_output_path
from app.utils.image_processing import (
    decode_base64_image,
    decode_base64_mask,
    process_feather_mask,
    resize_maintaining_aspect,
)
from app.core.flow_engine import FlowWarpEngine
from app.core.seamless_looper import SeamlessLooper
from app.core.video_exporter import VideoExporter
from app.api.deps import task_manager

router = APIRouter(prefix="/animate", tags=["Animation Engines"])


def _render_flow_task_sync(task_id: str, req: FlowAnimateRequest):
    """
    Synchronous worker that renders and encodes the video sequence.
    """
    try:
        # Decode source image
        raw_image = decode_base64_image(req.image)
        
        # Apply resolution scale if specified
        target_dim = int(1024 * req.resolution_scale)
        image_np, _ = resize_maintaining_aspect(raw_image, max_dim=target_dim)
        h, w = image_np.shape[:2]
        
        # Process mask
        mask_np = decode_base64_mask(req.mask, (h, w))
        feathered_mask = process_feather_mask(mask_np, blur_radius=15)
        
        # Generate frames
        frames = FlowWarpEngine.generate_frame_sequence(image_np, feathered_mask, req)
        
        # Apply seamless looping
        processed_frames = SeamlessLooper.apply_loop_mode(frames, req.loop_mode)
        
        # Export primary requested format
        out_path, rel_url = get_output_path(task_id, req.format.value)
        VideoExporter.export(processed_frames, out_path, req.format, fps=req.fps)
        
        # Also generate GIF companion if user requested MP4/WebM
        gif_url = None
        if req.format != ExportFormat.GIF:
            gif_path, gif_rel = get_output_path(task_id, "gif")
            # Downsample slightly for lighter GIF
            step = max(1, len(processed_frames) // 30)
            sub_frames = processed_frames[::step]
            VideoExporter.export(sub_frames, gif_path, ExportFormat.GIF, fps=min(req.fps, 15))
            gif_url = gif_rel
            
        file_size = os.path.getsize(out_path) if out_path.exists() else 0
        
        # Async notification via event loop
        loop = asyncio.get_event_loop()
        if loop.is_running():
            asyncio.run_coroutine_threadsafe(
                task_manager.complete_task(
                    task_id=task_id,
                    result_url=rel_url,
                    video_url=rel_url if req.format in [ExportFormat.MP4, ExportFormat.WEBM] else None,
                    gif_url=gif_url or rel_url,
                    duration=req.duration_seconds,
                    file_size=file_size
                ),
                loop
            )
    except Exception as ex:
        loop = asyncio.get_event_loop()
        if loop.is_running():
            asyncio.run_coroutine_threadsafe(
                task_manager.fail_task(task_id=task_id, error_message=str(ex)),
                loop
            )


@router.post("/flow", response_model=AnimateResponse, summary="Render neural vector flow animation")
async def create_flow_animation(
    req: FlowAnimateRequest,
    background_tasks: BackgroundTasks
):
    """
    Renders hair, cloth, and fluid animations from still images using directional vectors and physics.
    """
    try:
        task_id = generate_unique_id("flow")
        await task_manager.create_task(task_id, "Rendering animation frames...")
        
        # Run rendering in background thread to keep API responsive
        background_tasks.add_task(_render_flow_task_sync, task_id, req)
        
        return AnimateResponse(
            task_id=task_id,
            status="queued",
            message="Animation rendering started in background",
            result_url=f"/api/tasks/{task_id}"
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to schedule animation task: {str(e)}")
