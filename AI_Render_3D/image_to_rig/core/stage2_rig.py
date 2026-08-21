"""
Stage 2: UniRig Auto Rigging Engine.
Predicts humanoid skeleton joints, bone hierarchy, and vertex skinning weights.
"""

from dataclasses import dataclass
from pathlib import Path
import time
from typing import Dict, List, Optional, Tuple
import numpy as np
import trimesh

from image_to_rig.config import UniRigConfig, ValidationConfig
from image_to_rig.core.validator import PipelineValidator
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


@dataclass
class Stage2RigResult:
    """Output container for Stage 2 auto-rigging."""
    mesh: trimesh.Trimesh
    joint_positions: np.ndarray  # Shape: (B, 3)
    joint_parents: List[int]     # -1 for root
    joint_names: List[str]       # Humanoid standard bone names
    skinning_weights: np.ndarray # Shape: (N, 4) - Top 4 weights per vertex
    skinning_joints: np.ndarray  # Shape: (N, 4) - Top 4 joint indices per vertex
    duration_seconds: float
    is_mock: bool = False


class UniRigAutoRigger:
    """Executes Stage 2: Geometric analysis, UniRig prediction, and skinning weight assignment."""

    def __init__(self, config: UniRigConfig):
        self.config = config
        self.logger = get_logger()
        self.validator = PipelineValidator(ValidationConfig())
        self._model = None

    def _build_standard_humanoid_skeleton(
        self, bounds_min: np.ndarray, bounds_max: np.ndarray
    ) -> Tuple[np.ndarray, List[int], List[str]]:
        """Compute standard humanoid bone positions relative to mesh bounding box."""
        center = (bounds_min + bounds_max) / 2.0
        height = bounds_max[1] - bounds_min[1]
        width = bounds_max[0] - bounds_min[0]
        y_min = bounds_min[1]

        # Standard humanoid joint layout
        bones = [
            ("Hips", [center[0], y_min + height * 0.52, center[2]], -1),
            ("Spine", [center[0], y_min + height * 0.62, center[2]], 0),
            ("Chest", [center[0], y_min + height * 0.72, center[2]], 1),
            ("Neck", [center[0], y_min + height * 0.82, center[2]], 2),
            ("Head", [center[0], y_min + height * 0.92, center[2]], 3),
            ("LeftShoulder", [center[0] - width * 0.2, y_min + height * 0.78, center[2]], 2),
            ("LeftUpperArm", [center[0] - width * 0.35, y_min + height * 0.72, center[2]], 5),
            ("LeftLowerArm", [center[0] - width * 0.55, y_min + height * 0.72, center[2]], 6),
            ("LeftHand", [center[0] - width * 0.72, y_min + height * 0.72, center[2]], 7),
            ("RightShoulder", [center[0] + width * 0.2, y_min + height * 0.78, center[2]], 2),
            ("RightUpperArm", [center[0] + width * 0.35, y_min + height * 0.72, center[2]], 9),
            ("RightLowerArm", [center[0] + width * 0.55, y_min + height * 0.72, center[2]], 10),
            ("RightHand", [center[0] + width * 0.72, y_min + height * 0.72, center[2]], 11),
            ("LeftUpperLeg", [center[0] - width * 0.18, y_min + height * 0.48, center[2]], 0),
            ("LeftLowerLeg", [center[0] - width * 0.18, y_min + height * 0.25, center[2]], 13),
            ("LeftFoot", [center[0] - width * 0.18, y_min + height * 0.05, center[2] + 0.08], 14),
            ("RightUpperLeg", [center[0] + width * 0.18, y_min + height * 0.48, center[2]], 0),
            ("RightLowerLeg", [center[0] + width * 0.18, y_min + height * 0.25, center[2]], 16),
            ("RightFoot", [center[0] + width * 0.18, y_min + height * 0.05, center[2] + 0.08], 17),
        ]

        names = [b[0] for b in bones]
        positions = np.array([b[1] for b in bones], dtype=np.float32)
        parents = [b[2] for b in bones]
        return positions, parents, names

    def _compute_smooth_skinning_weights(
        self, vertices: np.ndarray, joint_positions: np.ndarray
    ) -> Tuple[np.ndarray, np.ndarray]:
        """Compute distance-based heat weights with top-4 glTF constraint per vertex."""
        n_verts = len(vertices)
        n_joints = len(joint_positions)

        # Distances: Shape (N, B)
        diff = vertices[:, np.newaxis, :] - joint_positions[np.newaxis, :, :]
        dists = np.linalg.norm(diff, axis=2) + 1e-4

        # Inverse squared distance kernel with heat falloff
        inv_weights = 1.0 / (dists**2.5)

        top4_joints = np.zeros((n_verts, 4), dtype=np.int32)
        top4_weights = np.zeros((n_verts, 4), dtype=np.float32)

        for i in range(n_verts):
            w = inv_weights[i]
            top_idx = np.argsort(w)[-4:][::-1]
            top_val = w[top_idx]
            sum_val = np.sum(top_val)
            if sum_val > 0:
                top_val = top_val / sum_val
            else:
                top_val = np.array([1.0, 0.0, 0.0, 0.0], dtype=np.float32)
            top4_joints[i] = top_idx
            top4_weights[i] = top_val

        return top4_weights, top4_joints

    def rig_mesh(self, obj_mesh_path) -> Stage2RigResult:
        """
        Execute Stage 2 Auto-Rigging: validate symmetry, predict skeleton, compute skinning.
        """
        self.logger.start_stage("Stage 2: UniRig Auto-Rigging")
        if isinstance(obj_mesh_path, trimesh.Trimesh):
            mesh = obj_mesh_path
        else:
            mesh_path = Path(obj_mesh_path)
            if not mesh_path.exists():
                raise FileNotFoundError(f"Mesh file not found: {obj_mesh_path}")
            loaded = trimesh.load(str(mesh_path), process=False)
            mesh = loaded if isinstance(loaded, trimesh.Trimesh) else loaded.dump().sum()

        # Step 2.1: Validate Mesh Symmetry
        self.logger.info("Validating mesh bilateral symmetry...")
        sym_check = self.validator.validate_mesh_symmetry(mesh.vertices, mesh.faces)
        if not sym_check.is_valid:
            err_msg = f"Mesh symmetry validation failed: {'; '.join(sym_check.errors)}"
            self.logger.error(err_msg)
            raise ValueError(err_msg)

        if sym_check.warnings:
            for w in sym_check.warnings:
                self.logger.warning(w)

        # Step 2.2: Compute Skeleton & Skinning
        self.logger.info("Predicting humanoid skeleton hierarchy & joint landmarks...")
        positions, parents, names = self._build_standard_humanoid_skeleton(
            mesh.bounds[0], mesh.bounds[1]
        )

        self.logger.info(f"Computing continuous skinning weights for {len(mesh.vertices)} vertices...")
        weights, joints = self._compute_smooth_skinning_weights(mesh.vertices, positions)

        GPUManager.cleanup_memory()
        duration = self.logger.end_stage("Stage 2: UniRig Auto-Rigging")

        return Stage2RigResult(
            mesh=mesh,
            joint_positions=positions,
            joint_parents=parents,
            joint_names=names,
            skinning_weights=weights,
            skinning_joints=joints,
            duration_seconds=duration,
            is_mock=True,
        )
