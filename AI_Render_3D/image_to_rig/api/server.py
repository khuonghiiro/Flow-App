"""
FastAPI Server providing REST endpoints for Studio backend / TypeScript integration.
"""

from pathlib import Path
from typing import Dict
from fastapi import FastAPI, HTTPException, UploadFile, File, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse

from image_to_rig.api.schemas import (
    PipelineRequest,
    PipelineResponse,
    Stage1Response,
    Stage2Response,
    JobStatusResponse,
    VRMConversionResponse,
)
from image_to_rig.config import DEFAULT_CONFIG
from image_to_rig.core.pipeline import ImageToRigPipeline
from image_to_rig.core.queue_manager import get_gpu_queue
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


def create_api_app() -> FastAPI:
    """Create and configure the FastAPI application."""
    app = FastAPI(
        title="Image-to-Rig Pipeline API",
        version="1.0.0",
        description="REST API for generating skinned humanoid 3D assets from 2D images.",
    )

    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    pipeline = ImageToRigPipeline(DEFAULT_CONFIG)
    queue = get_gpu_queue()
    logger = get_logger()

    @app.get("/api/v1/health")
    async def health_check() -> Dict:
        """Return system health and GPU VRAM diagnostics."""
        return {
            "status": "healthy",
            "gpu_device": GPUManager.get_device_name(),
            "cuda_available": GPUManager.is_cuda_available(),
            "vram_status_mb": GPUManager.get_vram_status_mb(),
        }

    @app.post("/api/v1/pipeline/full", response_model=PipelineResponse)
    async def run_full_pipeline(req: PipelineRequest):
        """Run complete 4-stage pipeline synchronously within GPU queue mutex."""
        job = queue.create_job()
        
        def task_fn():
            return pipeline.run_pipeline(
                image_path=req.image_path,
                engine=req.engine,
                ss_steps=req.ss_steps,
                ss_cfg_strength=req.ss_cfg_strength,
                slat_steps=req.slat_steps,
                slat_cfg_strength=req.slat_cfg_strength,
                seed=req.seed,
                extract_face_scaffold=req.extract_face_scaffold,
                progress_cb=lambda p, s: queue.update_job_progress(job.job_id, p, s),
            )

        try:
            result = queue.run_synchronous_job(job.job_id, task_fn)
            if not result.success:
                return PipelineResponse(
                    success=False,
                    job_id=job.job_id,
                    total_time_seconds=result.total_time_seconds,
                    error_message=result.error_message,
                )

            glb_file = Path(result.glb_path).name if result.glb_path else None
            meta_file = Path(result.metadata_path).name if result.metadata_path else None

            return PipelineResponse(
                success=True,
                job_id=job.job_id,
                glb_url=f"/api/v1/download/{glb_file}" if glb_file else None,
                metadata_url=f"/api/v1/download/{meta_file}" if meta_file else None,
                metadata=result.metadata,
                total_time_seconds=result.total_time_seconds,
            )
        except Exception as ex:
            raise HTTPException(status_code=500, detail=str(ex))

    @app.post("/api/v1/upload-and-run", response_model=PipelineResponse)
    async def upload_and_run(file: UploadFile = File(...)):
        """Upload an image file directly and execute full pipeline."""
        upload_dir = Path(DEFAULT_CONFIG.temp_dir) / "uploads"
        upload_dir.mkdir(parents=True, exist_ok=True)
        dest_path = upload_dir / file.filename

        with open(dest_path, "wb") as f:
            content = await file.read()
            f.write(content)

        req = PipelineRequest(image_path=str(dest_path))
        return await run_full_pipeline(req)

    @app.get("/api/v1/download/{filename}")
    async def download_file(filename: str):
        """Download generated output file (.glb, .json, .obj, .png)."""
        output_dir = Path(DEFAULT_CONFIG.export.output_dir)
        target_path = output_dir / filename

        if not target_path.exists():
            # Check temp dir
            target_path = Path(DEFAULT_CONFIG.temp_dir) / filename

        if not target_path.exists():
            raise HTTPException(status_code=404, detail=f"File {filename} not found.")

        return FileResponse(path=str(target_path), filename=filename)

    @app.get("/api/v1/models/status")
    async def get_models_status_api() -> Dict:
        """Inspect and return current status of AI models in the root models/ directory."""
        from image_to_rig.tools.model_downloader import get_model_status
        return get_model_status()

    @app.post("/api/v1/models/download")
    async def trigger_model_download(background_tasks: BackgroundTasks, component: str = "all") -> Dict:
        """Trigger background download of AI models into models/."""
        from image_to_rig.tools.model_downloader import (
            download_triposr,
            download_rembg,
            download_unirig,
            download_all_models,
        )

        def bg_download():
            if component == "triposr":
                download_triposr()
            elif component == "rembg":
                download_rembg()
            elif component == "unirig":
                download_unirig()
            else:
                download_all_models()

        background_tasks.add_task(bg_download)
        return {
            "status": "download_started",
            "component": component,
            "message": f"Downloading {component} in background into models/.",
        }

    return app
