"""
Unit tests for GLBExporter and asset metadata generation.
"""

import json
from pathlib import Path
import numpy as np
import pytest
import trimesh

from image_to_rig.config import ExportConfig
from image_to_rig.core.stage2_rig import Stage2RigResult
from image_to_rig.core.stage3_export import GLBExporter


@pytest.fixture
def mock_rig_result():
    mesh = trimesh.creation.cylinder(radius=0.2, height=1.0, sections=16)
    n_verts = len(mesh.vertices)
    joint_positions = np.array(
        [[0.0, 0.0, 0.0], [0.0, 0.5, 0.0], [0.0, 1.0, 0.0]], dtype=np.float32
    )
    joint_parents = [-1, 0, 1]
    joint_names = ["Hips", "Spine", "Head"]
    skinning_weights = np.ones((n_verts, 4), dtype=np.float32) / 4.0
    skinning_joints = np.zeros((n_verts, 4), dtype=np.int32)

    return Stage2RigResult(
        mesh=mesh,
        joint_positions=joint_positions,
        joint_parents=joint_parents,
        joint_names=joint_names,
        skinning_weights=skinning_weights,
        skinning_joints=skinning_joints,
        duration_seconds=0.1,
    )


def test_export_rigged_glb_and_metadata(mock_rig_result, tmp_path):
    exporter = GLBExporter(ExportConfig(output_dir=str(tmp_path)))
    out_glb = tmp_path / "test_char.glb"

    res = exporter.export_rigged_glb(
        rig_result=mock_rig_result,
        output_glb_path=str(out_glb),
        total_pipeline_time=1.5,
    )

    assert Path(res.glb_path).exists()
    assert Path(res.metadata_path).exists()
    assert Path(res.glb_path).stat().st_size > 0

    # Verify metadata JSON content
    with open(res.metadata_path, "r", encoding="utf-8") as f:
        meta = json.load(f)

    assert meta["model_id"] == "test_char"
    assert meta["has_skinning"] is True
    assert meta["bone_count"] == 3
    assert meta["bone_names"] == ["Hips", "Spine", "Head"]
    assert meta["vertex_count"] > 0
    assert "bounding_box" in meta
