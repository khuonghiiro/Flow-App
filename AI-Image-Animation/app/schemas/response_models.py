from typing import Optional, Dict, Any, List
from pydantic import BaseModel, Field


class GPUStatusResponse(BaseModel):
    device_name: str
    is_cuda_available: bool
    total_vram_gb: float
    used_vram_gb: float
    free_vram_gb: float
    is_rtx_3060_optimized: bool
    recommended_engine: str


class WindPresetInfo(BaseModel):
    id: str
    name: str
    description: str
    icon: str
    wind_strength: float
    wave_frequency: float
    turbulence: float
    flutter_scale: float


class TaskStatusResponse(BaseModel):
    task_id: str
    status: str = Field(..., description="'queued' | 'processing' | 'completed' | 'failed' | 'cancelled'")
    progress: float = Field(0.0, ge=0.0, le=1.0)
    message: str = ""
    result_url: Optional[str] = None
    video_url: Optional[str] = None
    gif_url: Optional[str] = None
    duration_seconds: Optional[float] = None
    file_size_bytes: Optional[int] = None
    error: Optional[str] = None


class AnimateResponse(BaseModel):
    task_id: str
    status: str
    message: str
    result_url: Optional[str] = None
    video_url: Optional[str] = None
    gif_url: Optional[str] = None
    metadata: Optional[Dict[str, Any]] = None


class PreviewFrameResponse(BaseModel):
    preview_image: str = Field(..., description="Base64 encoded PNG of warped preview frame")
    phase: float
    processing_time_ms: float
