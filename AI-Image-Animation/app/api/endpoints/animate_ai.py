import os
import anyio.to_thread
from fastapi import APIRouter, BackgroundTasks, HTTPException
from app.schemas.request_models import DiffusionAnimateRequest, ExportFormat
from app.schemas.response_models import AnimateResponse
from app.utils.file_manager import generate_unique_id, get_output_path
from app.utils.image_processing import decode_base64_image
from app.core.ai_motion_model import ai_model_instance
from app.core.video_exporter import VideoExporter
from app.api.deps import task_manager

router = APIRouter(prefix="/animate", tags=["Animation Engines"])


def _compute_diffusion_sync(task_id: str, req: DiffusionAnimateRequest):
    """
    Synchronous worker for AI diffusion generative motion.
    """
    raw_image = decode_base64_image(req.image)
    frames = ai_model_instance.generate(raw_image, req)
    
    out_path, rel_url = get_output_path(task_id, "mp4")
    VideoExporter.export(frames, out_path, ExportFormat.MP4, fps=req.fps)
    
    file_size = os.path.getsize(out_path) if out_path.exists() else 0
    duration = len(frames) / float(req.fps)
    return rel_url, duration, file_size


async def _render_diffusion_task(task_id: str, req: DiffusionAnimateRequest):
    """
    Asynchronous background handler that offloads diffusion computation and updates task status.
    """
    try:
        rel_url, duration, file_size = await anyio.to_thread.run_sync(_compute_diffusion_sync, task_id, req)
        await task_manager.complete_task(
            task_id=task_id,
            result_url=rel_url,
            video_url=rel_url,
            duration=duration,
            file_size=file_size
        )
    except Exception as ex:
        await task_manager.fail_task(task_id=task_id, error_message=str(ex))


@router.post("/diffusion", response_model=AnimateResponse, summary="Render AI generative diffusion video")
async def create_diffusion_animation(
    req: DiffusionAnimateRequest,
    background_tasks: BackgroundTasks
):
    """
    Generates dynamic video from still image using PyTorch diffusion models optimized for RTX 3060 12GB.
    """
    try:
        task_id = generate_unique_id("diff")
        await task_manager.create_task(task_id, "AI Diffusion generation started...")
        
        background_tasks.add_task(_render_diffusion_task, task_id, req)
        
        return AnimateResponse(
            task_id=task_id,
            status="queued",
            message="Diffusion task dispatched to GPU",
            result_url=f"/api/tasks/{task_id}"
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to start diffusion task: {str(e)}")

