"""
Master Pipeline Orchestrator.
Coordinates Stage 1 (Mesh Gen), Stage 2 (Auto Rig), Stage 3 (GLB Export), and Stage 4 (Blendshapes).
"""

from dataclasses import dataclass
from pathlib import Path
import time
from typing import Any, Callable, Dict, Optional

from image_to_rig.config import PipelineConfig, DEFAULT_CONFIG
from image_to_rig.core.queue_manager import GPUJobQueue, get_gpu_queue
from image_to_rig.core.validator import PipelineValidator, ValidationResult
from image_to_rig.core.stage1_mesh import TripoSRMeshGenerator, Stage1MeshResult
from image_to_rig.core.stage2_rig import UniRigAutoRigger, Stage2RigResult
from image_to_rig.core.stage3_export import GLBExporter, Stage3ExportResult
from image_to_rig.core.stage4_blendshapes import FacialBlendshapeManager, FaceScaffoldResult
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


@dataclass
class PipelineExecutionResult:
    """Full execution summary containing outputs from all pipeline stages."""
    success: bool
    glb_path: Optional[str]
    metadata_path: Optional[str]
    metadata: Optional[Dict[str, Any]]
    stage1_result: Optional[Stage1MeshResult]
    stage2_result: Optional[Stage2RigResult]
    stage3_result: Optional[Stage3ExportResult]
    face_scaffold: Optional[FaceScaffoldResult]
    total_time_seconds: float
    error_message: Optional[str] = None


class ImageToRigPipeline:
    """High-level facade orchestrating the entire Image-to-Rig conversion pipeline."""

    def __init__(self, config: Optional[PipelineConfig] = None):
        self.config = config or DEFAULT_CONFIG
        self.config.ensure_directories()
        self.logger = get_logger()

        self.validator = PipelineValidator(self.config.validation)
        self.stage1_mesh = TripoSRMeshGenerator(self.config.triposr)
        self.stage2_rig = UniRigAutoRigger(self.config.unirig)
        self.stage3_export = GLBExporter(self.config.export)
        self.stage4_blendshapes = FacialBlendshapeManager()
        self.queue = get_gpu_queue()

    def validate_input(self, image_path: str) -> ValidationResult:
        """Validate input 2D image before execution."""
        return self.validator.validate_image(image_path)

    def run_image_to_mesh(
        self, image_path: str, output_obj_path: Optional[str] = None
    ) -> Stage1MeshResult:
        """Execute Stage 1 alone (Image -> Mesh OBJ)."""
        val = self.validate_input(image_path)
        if not val.is_valid:
            raise ValueError(f"Input validation error: {'; '.join(val.errors)}")

        if not output_obj_path:
            stem = Path(image_path).stem
            output_obj_path = str(Path(self.config.temp_dir) / f"{stem}_raw_mesh.obj")

        return self.stage1_mesh.generate(image_path, output_obj_path)

    def run_auto_rig(self, obj_mesh_path: str) -> Stage2RigResult:
        """Execute Stage 2 alone (Mesh OBJ -> Rigging)."""
        return self.stage2_rig.rig_mesh(obj_mesh_path)

    def run_export_glb(
        self,
        rig_result: Stage2RigResult,
        output_glb_path: str,
        texture_path: Optional[str] = None,
    ) -> Stage3ExportResult:
        """Execute Stage 3 alone (Rigging Result -> GLB + Metadata)."""
        return self.stage3_export.export_rigged_glb(
            rig_result=rig_result,
            output_glb_path=output_glb_path,
            texture_path=texture_path,
        )

    def run_pipeline(
        self,
        image_path: str,
        output_glb_path: Optional[str] = None,
        progress_cb: Optional[Callable[[float, str], None]] = None,
        extract_face_scaffold: bool = True,
    ) -> PipelineExecutionResult:
        """
        Execute full end-to-end pipeline:
        Image -> Validation -> Stage 1 -> Stage 2 -> Stage 3 -> Stage 4.
        """
        start_time = time.time()
        self.logger.info(f"Initiating full Image-to-Rig pipeline for: {image_path}")

        try:
            # 1. Validation (0% - 10%)
            if progress_cb:
                progress_cb(0.05, "Validating input image...")
            val = self.validate_input(image_path)
            if not val.is_valid:
                raise ValueError(f"Input image rejected: {'; '.join(val.errors)}")

            stem = Path(image_path).stem
            temp_obj = str(Path(self.config.temp_dir) / f"{stem}_mesh.obj")
            final_glb = output_glb_path or str(
                Path(self.config.export.output_dir) / f"{stem}_rigged.glb"
            )

            # 2. Stage 1: TripoSR (10% - 50%)
            if progress_cb:
                progress_cb(0.15, "Generating 3D mesh via TripoSR...")
            s1_res = self.stage1_mesh.generate(image_path, temp_obj)

            # 3. Stage 2: UniRig (50% - 80%)
            if progress_cb:
                progress_cb(0.55, "Predicting skeleton & skinning weights via UniRig...")
            s2_res = self.stage2_rig.rig_mesh(s1_res.mesh_path)

            # 4. Stage 3: GLB Export (80% - 95%)
            if progress_cb:
                progress_cb(0.85, "Assembling glTF 2.0 binary asset...")
            s3_res = self.stage3_export.export_rigged_glb(
                rig_result=s2_res,
                output_glb_path=final_glb,
                texture_path=s1_res.texture_path,
                total_pipeline_time=(time.time() - start_time),
            )

            # 5. Stage 4: Blendshape Scaffold (95% - 100%)
            face_scaffold = None
            if extract_face_scaffold:
                if progress_cb:
                    progress_cb(0.95, "Generating facial sculpt scaffold...")
                face_scaffold = self.stage4_blendshapes.extract_face_for_blender_sculpt(
                    mesh=s2_res.mesh,
                    output_dir=str(Path(self.config.export.output_dir) / "face_scaffolds"),
                    base_name=stem,
                )

            total_duration = time.time() - start_time
            if progress_cb:
                progress_cb(1.0, f"Completed in {total_duration:.2f}s!")

            return PipelineExecutionResult(
                success=True,
                glb_path=s3_res.glb_path,
                metadata_path=s3_res.metadata_path,
                metadata=s3_res.metadata,
                stage1_result=s1_res,
                stage2_result=s2_res,
                stage3_result=s3_res,
                face_scaffold=face_scaffold,
                total_time_seconds=round(total_duration, 2),
            )

        except Exception as ex:
            self.logger.error(f"Pipeline execution failed: {str(ex)}")
            GPUManager.cleanup_memory()
            return PipelineExecutionResult(
                success=False,
                glb_path=None,
                metadata_path=None,
                metadata=None,
                stage1_result=None,
                stage2_result=None,
                stage3_result=None,
                face_scaffold=None,
                total_time_seconds=round(time.time() - start_time, 2),
                error_message=str(ex),
            )
