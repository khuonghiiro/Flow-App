from typing import List, Optional
from pydantic import BaseModel, Field


class RotateSingleRequest(BaseModel):
    """Request schema for predicting a single novel view angle."""
    image_base64: Optional[str] = Field(None, description="Base64 encoded input image (PNG/JPEG)")
    image_url: Optional[str] = Field(None, description="Direct URL to image")
    azimuth_deg: float = Field(0.0, ge=-180.0, le=360.0, description="Horizontal rotation angle in degrees")
    elevation_deg: float = Field(0.0, ge=-90.0, le=90.0, description="Vertical elevation angle in degrees")
    radius_scale: float = Field(1.0, ge=0.5, le=2.5, description="Distance/Zoom scale (default 1.0)")
    remove_background: bool = Field(True, description="Automatically remove background and center subject")
    num_inference_steps: int = Field(28, ge=10, le=75, description="Diffusion steps")
    guidance_scale: float = Field(3.0, ge=1.0, le=10.0, description="Classifier-free guidance scale")


class TurntableRequest(BaseModel):
    """Request schema for generating a 360-degree turntable orbital sequence."""
    image_base64: Optional[str] = Field(None, description="Base64 encoded input image")
    image_url: Optional[str] = Field(None, description="Direct URL to image")
    num_frames: int = Field(16, ge=4, le=36, description="Number of turntable frames across 360 degrees")
    elevation_deg: float = Field(0.0, ge=-60.0, le=60.0, description="Elevation tilt in degrees")
    remove_background: bool = Field(True, description="Auto background removal & centering")
    generate_gif: bool = Field(True, description="Generate looping animated GIF")
    generate_spritesheet: bool = Field(True, description="Generate 360 turntable spritesheet")
    num_inference_steps: int = Field(24, ge=10, le=50, description="Diffusion steps per frame")


class MultiViewRequest(BaseModel):
    """Request schema for multi-image input conditioning."""
    images_base64: List[str] = Field(..., description="List of base64 images from different angles")
    target_azimuth_deg: float = Field(0.0, description="Target azimuth angle to synthesize")
    target_elevation_deg: float = Field(0.0, description="Target elevation angle to synthesize")


class SingleFrameResult(BaseModel):
    """Data for a single synthesized angle."""
    frame_index: int
    azimuth_deg: float
    elevation_deg: float
    image_base64: str
    image_url: Optional[str] = None


class RotationResponse(BaseModel):
    """Response schema for rotation operations."""
    success: bool
    message: str
    elapsed_seconds: float
    preprocessed_image_base64: Optional[str] = None
    frames: List[SingleFrameResult] = Field(default_factory=list)
    gif_base64: Optional[str] = None
    spritesheet_base64: Optional[str] = None
    zip_url: Optional[str] = None


class SystemStatusResponse(BaseModel):
    """Hardware & AI Engine health status."""
    status: str
    device: str
    gpu_name: Optional[str]
    vram_total_gb: float
    vram_used_gb: float
    vram_free_gb: float
    model_loaded: bool
    model_name: str
    half_precision: bool
    port: int
