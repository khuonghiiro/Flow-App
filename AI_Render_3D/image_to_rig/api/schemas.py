"""
Pydantic schemas and data models for FastAPI REST API endpoints.
"""

from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field


class PipelineRequest(BaseModel):
    """Payload for submitting an end-to-end image-to-rig generation request."""
    image_path: str = Field(..., description="Absolute path or relative path to input 2D image.")
    engine: Optional[str] = Field("trellis", description="3D generator engine: 'trellis', 'hunyuan3d', or 'triposr'.")
    ss_steps: Optional[int] = Field(50, description="Structure sampling ODE steps (15-60).")
    ss_cfg_strength: Optional[float] = Field(7.5, description="Structure CFG guidance strength (3.0-12.0).")
    slat_steps: Optional[int] = Field(50, description="Slat color & detail sampling ODE steps (15-60).")
    slat_cfg_strength: Optional[float] = Field(7.5, description="Slat CFG guidance strength (3.0-12.0).")
    seed: Optional[int] = Field(42, description="Random seed.")
    output_filename: Optional[str] = Field(None, description="Optional custom name for output .glb file.")
    extract_face_scaffold: bool = Field(True, description="Whether to export head mesh for manual facial sculpt.")


class PipelineResponse(BaseModel):
    """Response payload returned upon completing an end-to-end pipeline run."""
    success: bool
    job_id: str
    glb_url: Optional[str] = None
    metadata_url: Optional[str] = None
    metadata: Optional[Dict[str, Any]] = None
    total_time_seconds: float
    error_message: Optional[str] = None


class Stage1Response(BaseModel):
    """Response payload for Stage 1 Image-to-Mesh execution."""
    success: bool
    mesh_path: str
    texture_path: Optional[str] = None
    vertex_count: int
    triangle_count: int
    duration_seconds: float


class Stage2Response(BaseModel):
    """Response payload for Stage 2 Auto-Rigging execution."""
    success: bool
    bone_count: int
    bone_names: List[str]
    duration_seconds: float


class JobStatusResponse(BaseModel):
    """Job queue status response."""
    job_id: str
    status: str
    progress: float
    current_stage: str
    error_message: Optional[str] = None


class VRMConversionResponse(BaseModel):
    """Response payload for Branch A VRM conversion."""
    success: bool
    glb_path: str
    blendshape_count: int
    blendshape_names: List[str]
    has_arkit_compatibility: bool
