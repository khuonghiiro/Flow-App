"""
Core modules implementing the 4-stage Image-to-Rig generation pipeline.
"""

from image_to_rig.core.queue_manager import GPUJobQueue
from image_to_rig.core.validator import PipelineValidator, ValidationResult
from image_to_rig.core.stage1_mesh import TripoSRMeshGenerator, Stage1MeshResult
from image_to_rig.core.stage2_rig import UniRigAutoRigger, Stage2RigResult
from image_to_rig.core.stage3_export import GLBExporter, Stage3ExportResult
from image_to_rig.core.stage4_blendshapes import FacialBlendshapeManager
from image_to_rig.core.pipeline import ImageToRigPipeline, PipelineExecutionResult

__all__ = [
    "GPUJobQueue",
    "PipelineValidator",
    "ValidationResult",
    "TripoSRMeshGenerator",
    "Stage1MeshResult",
    "UniRigAutoRigger",
    "Stage2RigResult",
    "GLBExporter",
    "Stage3ExportResult",
    "FacialBlendshapeManager",
    "ImageToRigPipeline",
    "PipelineExecutionResult",
]
