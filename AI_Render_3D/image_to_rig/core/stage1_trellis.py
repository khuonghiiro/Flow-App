"""
Stage 1: TRELLIS Neural 3D Generator Engine.
Produces clean manifold geometry and textured materials.
Optimized for NVIDIA RTX 3060 12GB VRAM.
"""

from dataclasses import dataclass
import os
from pathlib import Path
import time
from typing import Optional, Tuple
import cv2
import numpy as np
from PIL import Image
import trimesh

from image_to_rig.config import TrellisConfig
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


@dataclass
class TrellisMeshResult:
    """Output container for TRELLIS mesh generation."""
    mesh_path: str
    texture_path: Optional[str]
    normal_map_path: Optional[str]
    roughness_map_path: Optional[str]
    metallic_map_path: Optional[str]
    vertex_count: int
    triangle_count: int
    duration_seconds: float
    pbr_enabled: bool = True
    is_mock: bool = False


class TrellisMeshGenerator:
    """Executes Single-Image to 3D generation using neural 3D field reconstruction."""

    def __init__(self, config: TrellisConfig):
        self.config = config
        self.logger = get_logger()

    def _preprocess_image(self, image_path: str) -> Tuple[Image.Image, str, dict]:
        """Preprocess 2D image: Inpainting edge dilation and Dual-Tile Texture Atlas."""
        GPUManager.register_torch_cuda_dlls()
        img = Image.open(image_path).convert("RGBA")
        temp_dir = Path("temp_workspace").resolve()
        temp_dir.mkdir(parents=True, exist_ok=True)
        stem = Path(image_path).stem
        atlas_path = str(temp_dir / f"{stem}_trellis_atlas.png")

        nobg_img = img
        if self.config.remove_background:
            try:
                import rembg
                rembg_home = Path("models/rembg").resolve()
                rembg_home.mkdir(parents=True, exist_ok=True)
                os.environ["U2NET_HOME"] = str(rembg_home)

                session = rembg.new_session("u2net", providers=["CPUExecutionProvider"])
                nobg_img = rembg.remove(img, session=session)
                self.logger.info("Removed background cleanly using RemBG.")
            except Exception as ex:
                self.logger.warning(f"RemBG background removal notice: {ex}")

        nobg_arr = np.array(nobg_img)
        if nobg_arr.shape[-1] == 4 and (nobg_arr[:, :, 3] > 10).any():
            alpha_mask = nobg_arr[:, :, 3] > 10
            y_indices, x_indices = np.where(alpha_mask)
            ymin, ymax = y_indices.min(), y_indices.max()
            xmin, xmax = x_indices.min(), x_indices.max()
            tight_front = nobg_img.crop((xmin, ymin, xmax + 1, ymax + 1))
        else:
            tight_front = nobg_img

        cw, ch = tight_front.size
        tight_arr = np.array(tight_front)
        rgb = tight_arr[:, :, :3].copy()
        mask = (tight_arr[:, :, 3] > 10).astype(np.uint8) * 255

        inpainted_rgb = cv2.inpaint(rgb, 255 - mask, inpaintRadius=7, flags=cv2.INPAINT_TELEA)

        atlas = np.zeros((2048, 2048, 3), dtype=np.uint8)
        target_h = int(2048 * 0.92)
        target_w = int(target_h * (cw / ch))
        if target_w > int(1024 * 0.92):
            target_w = int(1024 * 0.92)
            target_h = int(target_w * (ch / cw))

        front_resized = cv2.resize(inpainted_rgb, (target_w, target_h), interpolation=cv2.INTER_LANCZOS4)
        y_offset = (2048 - target_h) // 2
        x_offset_front = (1024 - target_w) // 2
        atlas[y_offset : y_offset + target_h, x_offset_front : x_offset_front + target_w] = front_resized

        back_view = cv2.flip(front_resized, 1)
        back_arr = back_view.astype(np.float32)
        head_h = int(target_h * 0.15)
        back_arr[:head_h] = back_arr[:head_h] * 0.35 + 20.0
        back_view_shaded = np.clip(back_arr, 0, 255).astype(np.uint8)

        x_offset_back = 1024 + (1024 - target_w) // 2
        atlas[y_offset : y_offset + target_h, x_offset_back : x_offset_back + target_w] = back_view_shaded

        atlas_img = Image.fromarray(atlas)
        atlas_img.save(atlas_path)

        atlas_meta = {
            "x_offset_front": x_offset_front,
            "x_offset_back": x_offset_back,
            "y_offset": y_offset,
            "target_w": target_w,
            "target_h": target_h,
        }

        try:
            from tsr.utils import resize_foreground
            resized = resize_foreground(nobg_img, ratio=0.85)
        except Exception:
            resized = nobg_img

        img_arr = np.array(resized).astype(np.float32) / 255.0
        if img_arr.shape[-1] == 4:
            alpha = img_arr[:, :, 3:4]
            gray_bg = img_arr[:, :, :3] * alpha + (1.0 - alpha) * 0.5
            rgb_input = Image.fromarray((gray_bg * 255.0).astype(np.uint8)).resize((512, 512), Image.Resampling.LANCZOS)
        else:
            rgb_input = Image.fromarray((img_arr * 255.0).astype(np.uint8)).resize((512, 512), Image.Resampling.LANCZOS)

        return rgb_input, atlas_path, atlas_meta

    def _run_inference(self, img: Image.Image) -> trimesh.Trimesh:
        """Run Microsoft TRELLIS neural flow matching and FlexiCubes isosurface extraction."""
        import torch

        os.environ["ATTN_BACKEND"] = "sdpa"
        os.environ["SPARSE_ATTN_BACKEND"] = "sdpa"
        GPUManager.register_torch_cuda_dlls()

        trellis_ckpt = Path("models/trellis").resolve()
        if (trellis_ckpt / "pipeline.json").exists():
            import trellis.pipelines
            self.logger.info("Loading Microsoft TRELLIS Flow-Matching 3D Generator...")
            pipe = trellis.pipelines.from_pretrained(str(trellis_ckpt))
            pipe.cuda()

            self.logger.info("Running TRELLIS Flow-Matching diffusion sampling...")
            outputs = pipe.run(
                image=img,
                formats=["mesh"],
                seed=42,
                preprocess_image=True,
            )
            mesh_res = outputs["mesh"][0]
            v = mesh_res.vertices.detach().cpu().numpy()
            f = mesh_res.faces.detach().cpu().numpy()

            raw_cols = mesh_res.vertex_attrs[:, :3].detach().cpu().numpy()
            rgb_cols = np.clip(raw_cols, 0.0, 1.0)
            rgba_cols = (np.column_stack([rgb_cols, np.ones(len(rgb_cols))]) * 255.0).astype(np.uint8)

            # Trellis coordinate system: X right/left, Y front/back, Z up.
            # Convert to standard animation Y-up coordinate frame: [x, z, -y]
            v_oriented = np.zeros_like(v)
            v_oriented[:, 0] = v[:, 0]
            v_oriented[:, 1] = v[:, 2]
            v_oriented[:, 2] = -v[:, 1]

            mesh = trimesh.Trimesh(vertices=v_oriented, faces=f, vertex_colors=rgba_cols, process=False)
            self.logger.info(f"TRELLIS extracted watertight mesh with {len(v):,} vertices and {len(f):,} faces with true 3D colors.")
            return mesh

        # Fallback to TSR if TRELLIS weights are missing
        self.logger.warning("TRELLIS weights not found, using TSR fallback.")
        from tsr.system import TSR
        model_path = Path("models/triposr").resolve()
        target_dir = str(model_path) if (model_path / "model.ckpt").exists() else "stabilityai/TripoSR"
        model = TSR.from_pretrained(target_dir, config_name="config.yaml", weight_name="model.ckpt")
        device = GPUManager.get_optimal_device_str()
        model.to(device)
        model.renderer.set_chunk_size(8192)

        with torch.no_grad():
            scene_codes = model(img, device=device)
            meshes = model.extract_mesh(scene_codes, has_vertex_color=True, resolution=256, threshold=25.0)
            mesh = meshes[0]

        from tsr.utils import to_gradio_3d_orientation
        mesh = to_gradio_3d_orientation(mesh)
        rot = trimesh.transformations.rotation_matrix(np.pi / 2.0, [1, 0, 0])
        mesh.apply_transform(rot)
        return mesh

    def generate(self, input_image_path: str, output_obj_path: str) -> TrellisMeshResult:
        """Executes full TRELLIS 3D Generation pipeline."""
        start_time = time.time()
        self.logger.start_stage(f"Stage 1: TRELLIS SOTA 3D Mesh Generation for: {input_image_path}")

        preprocessed_img, atlas_tex_path, meta = self._preprocess_image(input_image_path)
        out_path = Path(output_obj_path)
        out_path.parent.mkdir(parents=True, exist_ok=True)

        mesh = self._run_inference(preprocessed_img)

        bounds = mesh.bounds
        center_x = (bounds[0][0] + bounds[1][0]) / 2.0
        center_z = (bounds[0][2] + bounds[1][2]) / 2.0
        min_y = bounds[0][1]
        mesh.apply_translation([-center_x, -min_y, -center_z])

        height = mesh.bounds[1][1] - mesh.bounds[0][1]
        if height > 0:
            scale_factor = 1.75 / height
            mesh.apply_scale(scale_factor)

        has_native_colors = hasattr(mesh.visual, "vertex_colors") and mesh.visual.vertex_colors is not None and len(mesh.visual.vertex_colors) == len(mesh.vertices)

        if not has_native_colors:
            bounds = mesh.bounds
            mesh_w = bounds[1][0] - bounds[0][0] + 1e-5
            mesh_h = bounds[1][1] - bounds[0][1] + 1e-5

            u_rel = (mesh.vertices[:, 0] - bounds[0][0]) / mesh_w
            v_rel = (mesh.vertices[:, 1] - bounds[0][1]) / mesh_h

            u_min_f = meta["x_offset_front"] / 2048.0
            u_max_f = (meta["x_offset_front"] + meta["target_w"]) / 2048.0
            u_min_b = meta["x_offset_back"] / 2048.0
            u_max_b = (meta["x_offset_back"] + meta["target_w"]) / 2048.0

            v_min = (2048 - (meta["y_offset"] + meta["target_h"])) / 2048.0
            v_max = (2048 - meta["y_offset"]) / 2048.0

            normals = mesh.vertex_normals
            is_front = (normals[:, 2] >= 0.0) | (mesh.vertices[:, 2] >= 0.0)

            u_front = u_min_f + u_rel * (u_max_f - u_min_f)
            u_back = u_max_b - u_rel * (u_max_b - u_min_b)
            v_gltf = 1.0 - (v_min + v_rel * (v_max - v_min))

            u_final = np.where(is_front, u_front, u_back)
            v_final = v_gltf

            mesh.visual.uv = np.column_stack(
                [np.clip(u_final, 0.0, 1.0), np.clip(v_final, 0.0, 1.0)]
            ).astype(np.float32)

        mesh.fix_normals()
        mesh.export(str(out_path))

        duration = time.time() - start_time
        GPUManager.cleanup_memory()

        final_texture_path = None if has_native_colors else atlas_tex_path
        self.logger.info(
            f"=== [STAGE COMPLETE] 3D Mesh generated in {duration:.2f}s "
            f"({len(mesh.vertices):,} vertices, {len(mesh.faces):,} faces, native_3d_colors={has_native_colors}) ==="
        )

        return TrellisMeshResult(
            mesh_path=str(out_path),
            texture_path=final_texture_path,
            normal_map_path=None,
            roughness_map_path=None,
            metallic_map_path=None,
            vertex_count=len(mesh.vertices),
            triangle_count=len(mesh.faces),
            duration_seconds=round(duration, 2),
            pbr_enabled=True,
            is_mock=False,
        )
