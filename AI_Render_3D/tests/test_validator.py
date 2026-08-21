"""
Unit tests for PipelineValidator (image validation & mesh symmetry checks).
"""

import numpy as np
import pytest
from PIL import Image

from image_to_rig.config import ValidationConfig
from image_to_rig.core.validator import PipelineValidator


@pytest.fixture
def validator():
    config = ValidationConfig(min_image_size=256, max_image_size=2048)
    return PipelineValidator(config)


def test_validate_image_format_and_size(validator, tmp_path):
    # Test valid image
    img_path = tmp_path / "test_char.png"
    img = Image.new("RGB", (512, 512), color="white")
    img.save(img_path)

    res = validator.validate_image(str(img_path))
    assert res.is_valid is True
    assert len(res.errors) == 0

    # Test undersized image
    small_path = tmp_path / "small.png"
    img_small = Image.new("RGB", (100, 100), color="white")
    img_small.save(small_path)

    res_small = validator.validate_image(str(small_path))
    assert res_small.is_valid is False
    assert any("below minimum" in e for e in res_small.errors)

    # Test invalid extension
    txt_path = tmp_path / "test.txt"
    txt_path.write_text("not an image")
    res_txt = validator.validate_image(str(txt_path))
    assert res_txt.is_valid is False
    assert any("Unsupported extension" in e for e in res_txt.errors)


def test_validate_mesh_symmetry_pass(validator):
    # Symmetric mesh vertices
    x = np.array([-1.0, 1.0, -0.5, 0.5, 0.0] * 50)
    y = np.linspace(0, 2.0, len(x))
    z = np.zeros(len(x))
    verts = np.column_stack([x, y, z])
    faces = np.arange(len(verts) - (len(verts) % 3)).reshape(-1, 3)

    res = validator.validate_mesh_symmetry(verts, faces)
    assert res.is_valid is True
    assert res.symmetry_score > 0.8


def test_validate_mesh_symmetry_fail_asymmetric(validator):
    # Severely asymmetrical mesh (all vertices on left side X < 0)
    x = np.array([-1.0, -0.8, -0.5, -0.2] * 60)
    y = np.linspace(0, 2.0, len(x))
    z = np.zeros(len(x))
    verts = np.column_stack([x, y, z])
    faces = np.arange(len(verts) - (len(verts) % 3)).reshape(-1, 3)

    res = validator.validate_mesh_symmetry(verts, faces)
    assert res.is_valid is False
    assert any("asymmetry detected" in e for e in res.errors)
