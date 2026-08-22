"""
Pipeline configuration settings and hardware presets.
Configured for NVIDIA RTX 3060 12GB VRAM and 32GB system RAM.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import List


@dataclass
class Hunyuan3DConfig:
    """Configuration for SOTA Stage 1: Tencent Hunyuan3D-2GP Image to 3D."""
    model_id: str = "tencent/Hunyuan3D-2"
    local_model_dir: str = "models/hunyuan3d"
    low_vram_mode: bool = True  # Automatically uses mmgp offloading for 12GB VRAM
    texture_resolution: int = 2048  # 360-degree PBR texture atlas
    finger_refinement: bool = True  # Anatomical digit isolation & hand geometry mesh refinement
    foreground_ratio: float = 0.88
    remove_background: bool = True
    device: str = "cuda"


@dataclass
class TrellisConfig:
    """Configuration for SOTA Stage 1: TRELLIS Image to High-Fidelity 3D Mesh."""
    model_id: str = "microsoft/TRELLIS-image-large"
    local_model_dir: str = "models/trellis"
    sparse_structure_resolution: int = 512
    ss_steps: int = 35          # Golden sweet spot: 35 steps for complete geometric fidelity
    ss_cfg_strength: float = 5.8 # Golden ratio: 5.8 prevents protruding stalks & excessive distortion
    slat_steps: int = 35        # Golden sweet spot: 35 steps for rich organic textures
    slat_cfg_strength: float = 5.8 # High color detail without noise
    seed: int = 42
    simplify_target_faces: int = 100000  # Polygon budget for animation mode
    subdivide_high_poly: bool = False    # FlexiCubes native ~300k-400k triangles preserves exact details without RAM overflow
    texture_size: int = 2048  # High resolution PBR texture atlas
    foreground_ratio: float = 0.90
    remove_background: bool = True
    device: str = "cuda"


@dataclass
class TripoSRConfig:
    """Configuration for Stage 1: TripoSR Image to Mesh (Fast Preview Mode)."""
    model_id: str = "stabilityai/TripoSR"
    local_model_dir: str = "models/triposr"
    chunk_size: int = 8192  # Reduced chunk size for 12GB VRAM safety
    mc_resolution: int = 256  # Marching cubes isosurface grid resolution
    bake_texture: bool = True
    texture_resolution: int = 1024
    foreground_ratio: float = 0.85
    remove_background: bool = True
    device: str = "cuda"



@dataclass
class UniRigConfig:
    """Configuration for Stage 2: UniRig Auto Rigging."""
    model_repo: str = "VAST-AI-Research/UniRig"
    checkpoint_dir: str = "models/unirig"
    max_bones: int = 54
    symmetry_tolerance: float = 0.15  # Max allowed lateral centroid deviation
    normalize_scale: bool = True
    device: str = "cuda"


@dataclass
class ValidationConfig:
    """Validation thresholds for inputs and intermediate geometry."""
    min_image_size: int = 256
    max_image_size: int = 4096
    allowed_extensions: List[str] = field(
        default_factory=lambda: [".png", ".jpg", ".jpeg", ".webp"]
    )
    min_contrast_ratio: float = 0.15
    max_asymmetry_ratio: float = 0.25


@dataclass
class ExportConfig:
    """Export configuration for glTF 2.0 binary (.glb) output."""
    output_dir: str = "outputs"
    export_metadata: bool = True
    embed_textures: bool = True
    humanoid_bone_names: List[str] = field(
        default_factory=lambda: [
            "Hips",
            "Spine",
            "Chest",
            "UpperChest",
            "Neck",
            "Head",
            "LeftShoulder",
            "LeftUpperArm",
            "LeftLowerArm",
            "LeftHand",
            "RightShoulder",
            "RightUpperArm",
            "RightLowerArm",
            "RightHand",
            "LeftUpperLeg",
            "LeftLowerLeg",
            "LeftFoot",
            "LeftToes",
            "RightUpperLeg",
            "RightLowerLeg",
            "RightFoot",
            "RightToes",
        ]
    )


@dataclass
class PipelineConfig:
    """Master pipeline configuration combining all stages."""
    models_dir: str = "models"
    rembg_dir: str = "models/rembg"
    default_engine: str = "hunyuan3d"  # "hunyuan3d" (SOTA Meshy-Grade), "trellis", or "triposr" (Fast Preview)
    hunyuan3d: Hunyuan3DConfig = field(default_factory=Hunyuan3DConfig)
    trellis: TrellisConfig = field(default_factory=TrellisConfig)
    triposr: TripoSRConfig = field(default_factory=TripoSRConfig)
    unirig: UniRigConfig = field(default_factory=UniRigConfig)
    validation: ValidationConfig = field(default_factory=ValidationConfig)
    export: ExportConfig = field(default_factory=ExportConfig)
    temp_dir: str = "temp_workspace"
    max_concurrent_jobs: int = 1  # GPU mutex lock for 12GB VRAM
    job_timeout_seconds: int = 300

    def ensure_directories(self) -> None:
        """Create necessary project runtime directories if missing."""
        Path(self.models_dir).mkdir(parents=True, exist_ok=True)
        Path(self.rembg_dir).mkdir(parents=True, exist_ok=True)
        Path(self.hunyuan3d.local_model_dir).mkdir(parents=True, exist_ok=True)
        Path(self.trellis.local_model_dir).mkdir(parents=True, exist_ok=True)
        Path(self.triposr.local_model_dir).mkdir(parents=True, exist_ok=True)
        Path(self.unirig.checkpoint_dir).mkdir(parents=True, exist_ok=True)
        Path(self.export.output_dir).mkdir(parents=True, exist_ok=True)
        Path(self.temp_dir).mkdir(parents=True, exist_ok=True)



# Default global singleton configuration instance
DEFAULT_CONFIG = PipelineConfig()
