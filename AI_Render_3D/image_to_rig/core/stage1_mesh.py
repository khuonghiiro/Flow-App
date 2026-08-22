"""
Stage 1: Unified Multi-Engine 3D Mesh Generator.
Coordinates SOTA TRELLIS and TripoSR neural 3D reconstruction engines.
Optimized for NVIDIA RTX 3060 12GB VRAM.
"""

from dataclasses import dataclass
import os
from pathlib import Path
import time
from typing import Optional, Tuple, Literal
import numpy as np
from PIL import Image
import trimesh

from image_to_rig.config import PipelineConfig, TripoSRConfig, TrellisConfig, Hunyuan3DConfig
from image_to_rig.core.stage1_trellis import TrellisMeshGenerator, TrellisMeshResult
from image_to_rig.core.stage1_hunyuan3d import Hunyuan3DMeshGenerator, Hunyuan3DMeshResult
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


@dataclass
class Stage1MeshResult:
    """Output container for Stage 1 mesh generation."""
    mesh_path: str
    texture_path: Optional[str]
    vertex_count: int
    triangle_count: int
    duration_seconds: float
    is_watertight: bool = True
    volume: float = 0.0
    normal_path: Optional[str] = None
    metallic_roughness_path: Optional[str] = None
    hole_count: int = 0
    non_manifold_edges: int = 0
    engine_used: str = "trellis"
    is_mock: bool = False


class TripoSRMeshGenerator:
    """Executes Stage 1: 2D image preprocessing, TripoSR neural inference, and 3D mesh extraction."""

    def __init__(self, config: TripoSRConfig):
        self.config = config
        self.logger = get_logger()
        self._model = None

    def _load_model_if_needed(self):
        """Lazily initialize and load the TripoSR model on GPU from local models/ or HF."""
        if self._model is not None:
            return self._model

        GPUManager.register_torch_cuda_dlls()
        local_dir = Path(getattr(self.config, "local_model_dir", "models/triposr")).resolve()
        target_pretrained = str(local_dir) if (local_dir / "model.ckpt").exists() else self.config.model_id

        try:
            from tsr.system import TSR
            self.logger.info(f"Loading TripoSR neural model from '{target_pretrained}' on GPU...")
            model = TSR.from_pretrained(
                target_pretrained,
                config_name="config.yaml",
                weight_name="model.ckpt",
            )
            device = GPUManager.get_optimal_device_str()
            model.to(device)
            # Set chunk size to 8192 for 12GB VRAM safety (prevents CUDA OOM on Marching Cubes)
            chunk_size = getattr(self.config, "chunk_size", 8192) or 8192
            model.renderer.set_chunk_size(chunk_size)
            self.logger.info(f"TripoSR successfully loaded on {device} (VRAM Chunk Size: {chunk_size}).")
            self._model = model
            return self._model
        except Exception as ex:
            self.logger.warning(
                f"TripoSR load warning ({ex}). "
                "Will use procedural humanoid generator if neural checkpoint is missing."
            )
            return None

    def preprocess_image(self, input_image_path: str) -> Tuple[Image.Image, Optional[str]]:
        """
        Preprocess 2D image following official TripoSR training distribution:
        1. Remove background cleanly using RemBG.
        2. Resize foreground to 85% with tight bounding box padding.
        3. Alpha composite over 0.5 mid-gray background (128, 128, 128) -> Strict 3-channel RGB.
        """
        GPUManager.register_torch_cuda_dlls()
        self.logger.info(f"Preprocessing input image: {input_image_path}")
        raw_image = Image.open(input_image_path).convert("RGBA")
        temp_dir = Path("temp_workspace").resolve()
        temp_dir.mkdir(parents=True, exist_ok=True)
        baked_tex_path = str(temp_dir / f"{Path(input_image_path).stem}_diffuse.png")

        nobg_image = raw_image
        if self.config.remove_background:
            try:
                rembg_home = Path("models/rembg").resolve()
                rembg_home.mkdir(parents=True, exist_ok=True)
                os.environ["U2NET_HOME"] = str(rembg_home)

                import rembg
                session = rembg.new_session("u2net", providers=["CPUExecutionProvider"])
                nobg_image = rembg.remove(raw_image, session=session)
                self.logger.info("Background removed cleanly via RemBG.")
            except Exception as ex:
                self.logger.warning(f"RemBG background removal notice: {ex}")

        # 1. Save tightly cropped foreground character as the diffuse texture map
        nobg_arr = np.array(nobg_image)
        if nobg_arr.shape[-1] == 4 and (nobg_arr[:, :, 3] > 10).any():
            alpha_mask = nobg_arr[:, :, 3] > 10
            y_indices, x_indices = np.where(alpha_mask)
            ymin, ymax = y_indices.min(), y_indices.max()
            xmin, xmax = x_indices.min(), x_indices.max()
            tight_tex = nobg_image.crop((xmin, ymin, xmax + 1, ymax + 1))
        else:
            tight_tex = nobg_image

        tight_tex.save(baked_tex_path)

        # 2. Foreground 85% normalization (TripoSR camera framing standard)
        try:
            from tsr.utils import resize_foreground
            resized_nobg = resize_foreground(nobg_image, ratio=0.85)
        except Exception:
            resized_nobg = nobg_image

        # Alpha composite on 0.5 mid-gray background
        img_arr = np.array(resized_nobg).astype(np.float32) / 255.0
        if img_arr.shape[-1] == 4:
            alpha = img_arr[:, :, 3:4]
            gray_bg = img_arr[:, :, :3] * alpha + (1.0 - alpha) * 0.5
            rgb_input = Image.fromarray((gray_bg * 255.0).astype(np.uint8)).resize((512, 512), Image.Resampling.LANCZOS)
        else:
            rgb_input = Image.fromarray((img_arr * 255.0).astype(np.uint8)).resize((512, 512), Image.Resampling.LANCZOS)

        return rgb_input, baked_tex_path

    def _generate_mock_humanoid_mesh(
        self, output_obj_path: Path, output_tex_path: Path
    ) -> Stage1MeshResult:
        """Procedural humanoid mesh used strictly when no neural model weights exist."""
        self.logger.info("Generating procedural fallback mesh...")
        
        torso = trimesh.creation.cylinder(radius=0.25, height=0.7, sections=24)
        torso.apply_translation([0, 1.0, 0])

        head = trimesh.creation.icosphere(radius=0.18, subdivisions=3)
        head.apply_translation([0, 1.55, 0])

        left_arm = trimesh.creation.cylinder(radius=0.08, height=0.6, sections=16)
        left_arm.apply_translation([-0.4, 1.0, 0])

        right_arm = trimesh.creation.cylinder(radius=0.08, height=0.6, sections=16)
        right_arm.apply_translation([0.4, 1.0, 0])

        left_leg = trimesh.creation.cylinder(radius=0.1, height=0.7, sections=16)
        left_leg.apply_translation([-0.18, 0.35, 0])

        right_leg = trimesh.creation.cylinder(radius=0.1, height=0.7, sections=16)
        right_leg.apply_translation([0.18, 0.35, 0])

        combined = trimesh.util.concatenate([torso, head, left_arm, right_arm, left_leg, right_leg])
        
        output_obj_path.parent.mkdir(parents=True, exist_ok=True)
        combined.export(str(output_obj_path))

        texture_img = Image.new("RGB", (self.config.texture_resolution, self.config.texture_resolution), (120, 150, 200))
        texture_img.save(str(output_tex_path))

        return Stage1MeshResult(
            mesh_path=str(output_obj_path),
            texture_path=str(output_tex_path),
            vertex_count=len(combined.vertices),
            triangle_count=len(combined.faces),
            duration_seconds=0.5,
            engine_used="triposr",
            is_mock=True,
        )

    def generate(self, input_image_path: str, output_obj_path: str) -> Stage1MeshResult:
        """
        Execute full TripoSR neural inference:
        Input 2D Image -> Neural Triplane Forward -> Chunked Marching Cubes -> Upright 3D Mesh OBJ.
        """
        start_time = time.time()
        self.logger.start_stage("Stage 1: TripoSR Image-to-Mesh")

        out_obj = Path(output_obj_path)
        out_obj.parent.mkdir(parents=True, exist_ok=True)
        out_tex = out_obj.with_suffix(".png")

        processed_rgb, baked_tex = self.preprocess_image(input_image_path)
        model = self._load_model_if_needed()

        if model is None:
            result = self._generate_mock_humanoid_mesh(out_obj, out_tex)
            GPUManager.cleanup_memory()
            duration = self.logger.end_stage("Stage 1: TripoSR Image-to-Mesh")
            result.duration_seconds = duration
            return result

        # Real Neural TripoSR Inference
        try:
            import torch
            device = GPUManager.get_optimal_device_str()
            with torch.no_grad():
                self.logger.info("Running TripoSR neural network inference on CUDA...")
                scene_codes = model(processed_rgb, device=device)
                self.logger.info("Extracting 3D surface geometry via Marching Cubes...")
                meshes = model.extract_mesh(
                    scene_codes,
                    has_vertex_color=True,
                    resolution=self.config.mc_resolution,
                    threshold=25.0,
                )
                mesh = meshes[0]

            # 1. Coordinate transform to upright humanoid orientation
            from tsr.utils import to_gradio_3d_orientation
            mesh = to_gradio_3d_orientation(mesh)
            rot = trimesh.transformations.rotation_matrix(np.pi / 2.0, [1, 0, 0])
            mesh.apply_transform(rot)

            # 2. Re-orient upright, center at ground pivot (x=0, z=0, y=0)
            bounds = mesh.bounds
            center_x = (bounds[0][0] + bounds[1][0]) / 2.0
            center_z = (bounds[0][2] + bounds[1][2]) / 2.0
            min_y = bounds[0][1]
            mesh.apply_translation([-center_x, -min_y, -center_z])

            # 3. Scale to realistic humanoid height (1.75m)
            height = mesh.bounds[1][1] - mesh.bounds[0][1]
            if height > 0:
                scale_factor = 1.75 / height
                mesh.apply_scale(scale_factor)

            mesh.fix_normals()
            out_glb = out_obj.with_suffix(".glb")
            mesh.export(str(out_glb), file_type="glb")

            GPUManager.cleanup_memory()
            duration = self.logger.end_stage("Stage 1: TripoSR Image-to-Mesh")

            self.logger.info(
                f"Neural 3D Mesh successfully generated from 2D image in {duration:.2f}s "
                f"({len(mesh.vertices):,} vertices, {len(mesh.faces):,} faces)!"
            )

            return Stage1MeshResult(
                mesh_path=str(out_glb),
                texture_path=None,
                vertex_count=len(mesh.vertices),
                triangle_count=len(mesh.faces),
                duration_seconds=duration,
                engine_used="triposr",
                is_mock=False,
            )

        except Exception as ex:
            self.logger.error(f"TripoSR inference failed ({ex}), falling back to procedural mesh.")
            result = self._generate_mock_humanoid_mesh(out_obj, out_tex)
            GPUManager.cleanup_memory()
            duration = self.logger.end_stage("Stage 1: TripoSR Image-to-Mesh")
            result.duration_seconds = duration
            return result


class Stage1MeshRouter:
    """
    Master Stage 1 Router coordinating Hunyuan3D-2GP (SOTA Meshy-Grade), TRELLIS, and TripoSR engines.
    """

    def __init__(
        self,
        trellis_config: TrellisConfig,
        triposr_config: TripoSRConfig,
        hunyuan3d_config: Optional[Hunyuan3DConfig] = None,
    ):
        self.hunyuan3d_engine = Hunyuan3DMeshGenerator(hunyuan3d_config or Hunyuan3DConfig())
        self.trellis_engine = TrellisMeshGenerator(trellis_config)
        self.triposr_engine = TripoSRMeshGenerator(triposr_config)
        self.logger = get_logger()

    def generate(
        self,
        input_image_path: str,
        output_obj_path: str,
        engine: Literal["hunyuan3d", "trellis", "triposr"] = "hunyuan3d",
        side_image_path: Optional[str] = None,
        back_image_path: Optional[str] = None,
        persp_image_path: Optional[str] = None,
        ss_steps: Optional[int] = None,
        ss_cfg_strength: Optional[float] = None,
        slat_steps: Optional[int] = None,
        slat_cfg_strength: Optional[float] = None,
        seed: Optional[int] = None,
        subdivide_high_poly: Optional[bool] = None,
    ) -> Stage1MeshResult:
        """Route generation request to the selected 3D AI engine with customizable precision."""
        if engine == "hunyuan3d":
            self.logger.info("Executing Stage 1 with Hunyuan3D-2GP SOTA Multi-View Engine (Meshy-Grade)...")
            res = self.hunyuan3d_engine.generate(
                input_image_path=input_image_path,
                output_obj_path=output_obj_path,
                side_image_path=side_image_path,
                back_image_path=back_image_path,
                persp_image_path=persp_image_path,
                ss_steps=ss_steps,
                ss_cfg_strength=ss_cfg_strength,
                slat_steps=slat_steps,
                slat_cfg_strength=slat_cfg_strength,
                seed=seed,
                subdivide_high_poly=subdivide_high_poly,
            )
            return Stage1MeshResult(
                mesh_path=res.mesh_path,
                texture_path=res.texture_path,
                normal_path=getattr(res, "normal_path", None),
                metallic_roughness_path=getattr(res, "metallic_roughness_path", None),
                vertex_count=res.vertex_count,
                triangle_count=res.triangle_count,
                duration_seconds=res.duration_seconds,
                is_watertight=res.is_watertight,
                volume=res.volume,
                hole_count=res.hole_count,
                non_manifold_edges=res.non_manifold_edges,
                engine_used="hunyuan3d",
                is_mock=res.is_mock,
            )
        elif engine == "trellis":
            self.logger.info("Executing Stage 1 with SOTA TRELLIS Engine (High Precision Flow-Matching)...")
            res = self.trellis_engine.generate(
                input_image_path,
                output_obj_path,
                ss_steps=ss_steps,
                ss_cfg_strength=ss_cfg_strength,
                slat_steps=slat_steps,
                slat_cfg_strength=slat_cfg_strength,
                seed=seed,
                subdivide_high_poly=subdivide_high_poly,
            )
            return Stage1MeshResult(
                mesh_path=res.mesh_path,
                texture_path=res.texture_path,
                vertex_count=res.vertex_count,
                triangle_count=res.triangle_count,
                duration_seconds=res.duration_seconds,
                is_watertight=res.is_watertight,
                volume=res.volume,
                hole_count=res.hole_count,
                non_manifold_edges=res.non_manifold_edges,
                engine_used="trellis",
                is_mock=res.is_mock,
            )
        else:
            self.logger.info("Executing Stage 1 with TripoSR Engine (Neural 3D Mode)...")
            return self.triposr_engine.generate(input_image_path, output_obj_path)
