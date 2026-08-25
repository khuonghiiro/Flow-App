from enum import Enum
from typing import List, Optional
from pydantic import BaseModel, Field


class ExportFormat(str, Enum):
    MP4 = "mp4"
    GIF = "gif"
    WEBM = "webm"
    APNG = "apng"


class LoopMode(str, Enum):
    SEAMLESS_PHASE = "seamless_phase"  # Harmonic sinusoidal phase loop
    PING_PONG = "ping_pong"            # Forward-backward smooth ease
    CYCLIC_CROSSFADE = "cyclic_crossfade" # Continuous flow with blend window


class WindPresetEnum(str, Enum):
    GENTLE_BREEZE = "gentle_breeze"
    MODERATE_WIND = "moderate_wind"
    STRONG_GALE = "strong_gale"
    HAIR_SWAY = "hair_sway"
    FABRIC_FLUTTER = "fabric_flutter"
    OCEAN_WAVE = "ocean_wave"
    IDLE_BREATHING = "idle_breathing"
    CUSTOM = "custom"


class MotionVector(BaseModel):
    start_x: float = Field(..., ge=0.0, le=1.0, description="Normalized start X coordinate [0.0, 1.0]")
    start_y: float = Field(..., ge=0.0, le=1.0, description="Normalized start Y coordinate [0.0, 1.0]")
    end_x: float = Field(..., ge=0.0, le=1.0, description="Normalized end X coordinate [0.0, 1.0]")
    end_y: float = Field(..., ge=0.0, le=1.0, description="Normalized end Y coordinate [0.0, 1.0]")
    strength: float = Field(1.0, ge=0.1, le=5.0, description="Relative vector strength factor")


class AnchorPin(BaseModel):
    x: float = Field(..., ge=0.0, le=1.0, description="Normalized pin X coordinate [0.0, 1.0]")
    y: float = Field(..., ge=0.0, le=1.0, description="Normalized pin Y coordinate [0.0, 1.0]")
    radius: float = Field(0.08, ge=0.01, le=0.5, description="Normalized influence radius")
    weight: float = Field(1.0, ge=0.0, le=1.0, description="Anchor rigidity weight (1.0 = completely static)")


class FlowAnimateRequest(BaseModel):
    image: str = Field(..., description="Base64 encoded image string or image data URI")
    mask: Optional[str] = Field(None, description="Optional Base64 encoded mask (grayscale, 255=animate, 0=static)")
    vectors: List[MotionVector] = Field(default_factory=list, description="List of directional motion vectors")
    pins: List[AnchorPin] = Field(default_factory=list, description="List of static anchor pins to prevent deformation")
    
    # Physics settings
    preset: WindPresetEnum = Field(WindPresetEnum.GENTLE_BREEZE, description="Preset name for wind physics")
    wind_strength: float = Field(1.0, ge=0.0, le=4.0, description="Overall wind force multiplier")
    wave_frequency: float = Field(1.5, ge=0.1, le=6.0, description="Oscillation wave frequency in Hz")
    turbulence: float = Field(0.5, ge=0.0, le=2.0, description="Chaos & secondary flutter variation")
    flutter_scale: float = Field(1.0, ge=0.0, le=3.0, description="Scale of micro-flutter for hair strands & cloth ripples")
    
    # Output video specifications
    duration_seconds: float = Field(3.0, ge=0.5, le=10.0, description="Video duration in seconds")
    fps: int = Field(30, ge=12, le=60, description="Frames per second")
    format: ExportFormat = Field(ExportFormat.MP4, description="Target export container format")
    loop_mode: LoopMode = Field(LoopMode.SEAMLESS_PHASE, description="Loop transition technique")
    resolution_scale: float = Field(1.0, ge=0.25, le=2.0, description="Resolution multiplier (1.0 = original size)")


class SingleFramePreviewRequest(BaseModel):
    image: str = Field(..., description="Base64 encoded source image")
    mask: Optional[str] = Field(None, description="Optional Base64 mask")
    vectors: List[MotionVector] = Field(default_factory=list)
    pins: List[AnchorPin] = Field(default_factory=list)
    wind_strength: float = Field(1.0, ge=0.0, le=4.0)
    wave_frequency: float = Field(1.5, ge=0.1, le=6.0)
    turbulence: float = Field(0.5, ge=0.0, le=2.0)
    flutter_scale: float = Field(1.0, ge=0.0, le=3.0)
    phase: float = Field(0.0, ge=0.0, le=1.0, description="Normalized time phase [0.0 to 1.0]")
    preview_size: int = Field(512, ge=256, le=1024, description="Max width/height for fast preview")


class DiffusionAnimateRequest(BaseModel):
    image: str = Field(..., description="Base64 encoded source image")
    prompt: Optional[str] = Field("blowing hair, clothes fluttering in the wind, highly detailed", description="Motion prompt")
    negative_prompt: Optional[str] = Field("distorted, bad anatomy, blurry, flickering", description="Negative prompt")
    motion_bucket_id: int = Field(127, ge=1, le=255, description="SVD / AnimateDiff motion intensity")
    noise_aug_strength: float = Field(0.02, ge=0.0, le=1.0)
    fps: int = Field(24, ge=8, le=30)
    num_frames: int = Field(25, ge=14, le=49)
    guidance_scale: float = Field(3.0, ge=1.0, le=10.0)
