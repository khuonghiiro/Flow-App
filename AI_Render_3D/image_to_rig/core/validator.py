"""
Input image and 3D mesh geometry validation module.
Validates input characteristics and enforces bilateral mesh symmetry checks.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Tuple
import numpy as np
from PIL import Image

from image_to_rig.config import ValidationConfig


@dataclass
class ValidationResult:
    """Result container for input or geometry validation checks."""
    is_valid: bool
    warnings: List[str] = field(default_factory=list)
    errors: List[str] = field(default_factory=list)
    symmetry_score: float = 1.0  # 1.0 = perfect bilateral symmetry, 0.0 = completely asymmetric


class PipelineValidator:
    """Validates 2D image inputs and 3D intermediate meshes."""

    def __init__(self, config: ValidationConfig):
        self.config = config

    def validate_image(self, image_path: str) -> ValidationResult:
        """
        Validate 2D character input image for dimensions, format, and subject framing.
        """
        path = Path(image_path)
        if not path.exists():
            return ValidationResult(is_valid=False, errors=[f"File not found: {image_path}"])

        if path.suffix.lower() not in self.config.allowed_extensions:
            return ValidationResult(
                is_valid=False,
                errors=[
                    f"Unsupported extension '{path.suffix}'. Allowed: {self.config.allowed_extensions}"
                ],
            )

        try:
            with Image.open(image_path) as img:
                width, height = img.size
                warnings: List[str] = []
                errors: List[str] = []

                if width < self.config.min_image_size or height < self.config.min_image_size:
                    errors.append(
                        f"Image resolution ({width}x{height}) is below minimum {self.config.min_image_size}px."
                    )

                if width > self.config.max_image_size or height > self.config.max_image_size:
                    warnings.append(
                        f"Image resolution ({width}x{height}) is very large; it will be auto-downscaled."
                    )

                # Check aspect ratio
                aspect_ratio = width / height
                if aspect_ratio < 0.33 or aspect_ratio > 3.0:
                    warnings.append(
                        f"Extreme aspect ratio ({aspect_ratio:.2f}). For best results use a portrait character frame."
                    )

                # Check subject centering and edge contrast
                img_rgb = img.convert("RGB")
                img_np = np.array(img_rgb)
                edge_pixels = np.concatenate(
                    [img_np[0, :, :], img_np[-1, :, :], img_np[:, 0, :], img_np[:, -1, :]],
                    axis=0,
                )
                edge_std = float(np.std(edge_pixels))
                if edge_std > 45.0:
                    warnings.append(
                        "Background appears complex/textured. Background removal may leave artifacts."
                    )

                is_valid = len(errors) == 0
                return ValidationResult(is_valid=is_valid, warnings=warnings, errors=errors)

        except Exception as ex:
            return ValidationResult(
                is_valid=False, errors=[f"Failed to read image file: {str(ex)}"]
            )

    def validate_mesh_symmetry(
        self, vertices: np.ndarray, faces: np.ndarray
    ) -> ValidationResult:
        """
        Check bilateral symmetry (left-right along X-axis) of 3D character mesh.
        Prevents unilateral auto-rigging failures on truncated or deformed meshes.
        """
        warnings: List[str] = []
        errors: List[str] = []

        if vertices is None or len(vertices) < 100:
            return ValidationResult(
                is_valid=False, errors=["Mesh has insufficient vertices (<100)."]
            )

        if faces is None or len(faces) < 50:
            return ValidationResult(
                is_valid=False, errors=["Mesh has insufficient faces (<50)."]
            )

        # Check for NaNs or Infs
        if not np.isfinite(vertices).all():
            return ValidationResult(
                is_valid=False, errors=["Mesh contains invalid NaN/Inf vertex coordinates."]
            )

        # Compute lateral balance (X-axis symmetry)
        x_coords = vertices[:, 0]
        left_mask = x_coords < -0.01
        right_mask = x_coords > 0.01

        left_count = int(np.sum(left_mask))
        right_count = int(np.sum(right_mask))
        total_lateral = left_count + right_count

        if total_lateral > 0:
            imbalance = abs(left_count - right_count) / float(total_lateral)
            symmetry_score = 1.0 - imbalance
        else:
            imbalance = 0.0
            symmetry_score = 1.0

        if imbalance > self.config.max_asymmetry_ratio:
            errors.append(
                f"Severe lateral mesh asymmetry detected (imbalance: {imbalance:.1%}). "
                "Subject might be occluded or cropped on one side. Auto-rigging aborted."
            )
        elif imbalance > 0.12:
            warnings.append(
                f"Moderate lateral asymmetry ({imbalance:.1%}). Rig bones may require minor adjustment."
            )

        is_valid = len(errors) == 0
        return ValidationResult(
            is_valid=is_valid,
            warnings=warnings,
            errors=errors,
            symmetry_score=float(symmetry_score),
        )
