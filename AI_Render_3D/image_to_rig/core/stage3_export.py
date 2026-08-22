"""
Stage 3: glTF 2.0 Binary (.glb) Exporter and Asset Metadata Generator.
Builds valid skinned humanoid meshes with joint hierarchies, materials, vertex colors, and embedded textures.
"""

import json
from dataclasses import asdict, dataclass
from pathlib import Path
import struct
import time
from typing import Any, Dict, List, Optional
import numpy as np
import pygltflib
import trimesh

from image_to_rig.config import ExportConfig
from image_to_rig.core.stage2_rig import Stage2RigResult
from image_to_rig.utils.gpu_utils import GPUManager
from image_to_rig.utils.logger import get_logger


@dataclass
class Stage3ExportResult:
    """Output container for Stage 3 export."""
    glb_path: str
    metadata_path: str
    metadata: Dict[str, Any]
    duration_seconds: float


class GLBExporter:
    """Exports rigged mesh and skeleton into standard glTF 2.0 binary (.glb) files with textures."""

    def __init__(self, config: ExportConfig):
        self.config = config
        self.logger = get_logger()

    def _compute_inverse_bind_matrices(
        self, joint_positions: np.ndarray
    ) -> np.ndarray:
        """Compute 4x4 column-major inverse bind matrices for all skeleton joints."""
        num_joints = len(joint_positions)
        ibms = np.zeros((num_joints, 4, 4), dtype=np.float32)
        for i in range(num_joints):
            mat = np.eye(4, dtype=np.float32)
            mat[0:3, 3] = -joint_positions[i]
            ibms[i] = mat.T
        return ibms

    def _compute_dual_tile_uvs(self, mesh: trimesh.Trimesh) -> np.ndarray:
        """
        Computes precise, distortion-free 360-degree dual-tile UV coordinates:
        - Front tile: [0.0, 0.5] for front-facing vertices (Nz >= 0 or Z >= 0)
        - Back tile: [0.5, 1.0] for back-facing vertices (Nz < 0, horizontally flipped)
        Includes 4% padding margin to ensure limbs, head, and feet never sample outer background.
        """
        vertices = mesh.vertices
        normals = mesh.vertex_normals
        bounds = mesh.bounds

        span_x = bounds[1, 0] - bounds[0, 0] + 1e-5
        span_y = bounds[1, 1] - bounds[0, 1] + 1e-5

        u_rel = np.clip((vertices[:, 0] - bounds[0, 0]) / span_x, 0.0, 1.0)
        v_rel = np.clip((vertices[:, 1] - bounds[0, 1]) / span_y, 0.0, 1.0)

        # Front Tile occupies U in [0.04, 0.46]
        u_front = 0.04 + u_rel * (0.46 - 0.04)
        # Back Tile occupies U in [0.54, 0.96] (horizontally mirrored)
        u_back = 0.96 - u_rel * (0.96 - 0.54)

        # V is inverted for glTF 2.0 (V=0 is top, V=1 is bottom)
        v_gltf = 1.0 - (0.04 + v_rel * (0.96 - 0.04))

        is_front = (normals[:, 2] >= 0.0) | (vertices[:, 2] >= 0.0)

        u_final = np.where(is_front, u_front, u_back)
        v_final = v_gltf

        return np.column_stack([np.clip(u_final, 0.0, 1.0), np.clip(v_final, 0.0, 1.0)]).astype(np.float32)

    def export_rigged_glb(
        self,
        rig_result: Stage2RigResult,
        output_glb_path: str,
        texture_path: Optional[str] = None,
        normal_map_path: Optional[str] = None,
        metallic_roughness_path: Optional[str] = None,
        total_pipeline_time: float = 0.0,
    ) -> Stage3ExportResult:
        """
        Assemble complete glTF 2.0 binary containing skinned mesh, joint hierarchy,
        inverse bind matrices, materials, vertex colors, embedded texture, and companion metadata.
        """
        start_time = time.time()
        self.logger.start_stage("Stage 3: Convert & Export GLB")

        out_glb = Path(output_glb_path)
        out_glb.parent.mkdir(parents=True, exist_ok=True)
        out_meta = out_glb.with_suffix(".json")

        mesh = rig_result.mesh
        vertices = np.array(mesh.vertices, dtype=np.float32)
        faces = np.array(mesh.faces, dtype=np.uint32)
        normals = np.array(mesh.vertex_normals, dtype=np.float32)

        # UV Coordinates: compute dual-tile 360-degree coordinates
        if hasattr(mesh.visual, "uv") and mesh.visual.uv is not None and len(mesh.visual.uv) == len(vertices):
            u_max = mesh.visual.uv[:, 0].max()
            if u_max > 0.5:
                uvs = np.array(mesh.visual.uv, dtype=np.float32)
            else:
                uvs = self._compute_dual_tile_uvs(mesh)
        else:
            uvs = self._compute_dual_tile_uvs(mesh)

        joints = rig_result.skinning_joints.astype(np.uint16)
        weights = rig_result.skinning_weights.astype(np.float32)
        ibms = self._compute_inverse_bind_matrices(rig_result.joint_positions)

        # Build glTF structure using pygltflib
        gltf = pygltflib.GLTF2()
        binary_blob = bytearray()

        def add_buffer_data(data: bytes, target: Optional[int] = None) -> int:
            offset = len(binary_blob)
            pad = (4 - (offset % 4)) % 4
            binary_blob.extend(b"\x00" * pad)
            offset = len(binary_blob)
            binary_blob.extend(data)
            bv_idx = len(gltf.bufferViews)
            gltf.bufferViews.append(
                pygltflib.BufferView(
                    buffer=0,
                    byteOffset=offset,
                    byteLength=len(data),
                    target=target,
                )
            )
            return bv_idx

        # 1. Indices (ELEMENT_ARRAY_BUFFER = 34963)
        faces_flat = faces.flatten()
        idx_bv = add_buffer_data(faces_flat.tobytes(), target=34963)
        idx_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=idx_bv,
                componentType=pygltflib.UNSIGNED_INT,
                count=len(faces_flat),
                type=pygltflib.SCALAR,
                max=[int(faces_flat.max())],
                min=[int(faces_flat.min())],
            )
        )

        # 2. Positions (ARRAY_BUFFER = 34962)
        pos_bv = add_buffer_data(vertices.tobytes(), target=34962)
        pos_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=pos_bv,
                componentType=pygltflib.FLOAT,
                count=len(vertices),
                type=pygltflib.VEC3,
                max=vertices.max(axis=0).tolist(),
                min=vertices.min(axis=0).tolist(),
            )
        )

        # 3. Normals
        norm_bv = add_buffer_data(normals.tobytes(), target=34962)
        norm_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=norm_bv,
                componentType=pygltflib.FLOAT,
                count=len(normals),
                type=pygltflib.VEC3,
            )
        )

        # 4. UVs
        uv_bv = add_buffer_data(uvs.tobytes(), target=34962)
        uv_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=uv_bv,
                componentType=pygltflib.FLOAT,
                count=len(uvs),
                type=pygltflib.VEC2,
            )
        )

        # 5. Vertex Colors (COLOR_0)
        col_acc = None
        if hasattr(mesh.visual, "vertex_colors") and mesh.visual.vertex_colors is not None and len(mesh.visual.vertex_colors) == len(vertices):
            vcols = np.array(mesh.visual.vertex_colors)
            if vcols.dtype != np.uint8:
                if vcols.max() <= 1.0:
                    vcols = (vcols * 255.0).astype(np.uint8)
                else:
                    vcols = vcols.astype(np.uint8)
            if vcols.shape[1] == 3:
                vcols = np.column_stack([vcols, np.full((len(vcols), 1), 255, dtype=np.uint8)])
            
            col_bv = add_buffer_data(vcols.tobytes(), target=34962)
            col_acc = len(gltf.accessors)
            gltf.accessors.append(
                pygltflib.Accessor(
                    bufferView=col_bv,
                    componentType=pygltflib.UNSIGNED_BYTE,
                    normalized=True,
                    count=len(vcols),
                    type=pygltflib.VEC4,
                )
            )

        # 6. Joints (JOINTS_0)
        joints_bv = add_buffer_data(joints.tobytes(), target=34962)
        joints_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=joints_bv,
                componentType=pygltflib.UNSIGNED_SHORT,
                count=len(joints),
                type=pygltflib.VEC4,
            )
        )

        # 7. Weights (WEIGHTS_0)
        weights_bv = add_buffer_data(weights.tobytes(), target=34962)
        weights_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=weights_bv,
                componentType=pygltflib.FLOAT,
                count=len(weights),
                type=pygltflib.VEC4,
            )
        )

        # 8. Inverse Bind Matrices (IBM)
        ibm_bv = add_buffer_data(ibms.tobytes())
        ibm_acc = len(gltf.accessors)
        gltf.accessors.append(
            pygltflib.Accessor(
                bufferView=ibm_bv,
                componentType=pygltflib.FLOAT,
                count=len(rig_result.joint_positions),
                type=pygltflib.MAT4,
            )
        )

        # Build Bone Nodes & Joint indices
        joint_nodes_indices: List[int] = []
        for i, (name, pos, parent_idx) in enumerate(
            zip(rig_result.joint_names, rig_result.joint_positions, rig_result.joint_parents)
        ):
            if parent_idx >= 0:
                rel_pos = pos - rig_result.joint_positions[parent_idx]
            else:
                rel_pos = pos

            node_idx = len(gltf.nodes)
            joint_nodes_indices.append(node_idx)
            gltf.nodes.append(
                pygltflib.Node(
                    name=name,
                    translation=rel_pos.tolist(),
                    children=[],
                )
            )

        # Wire node children hierarchy
        for i, parent_idx in enumerate(rig_result.joint_parents):
            if parent_idx >= 0:
                parent_node_idx = joint_nodes_indices[parent_idx]
                child_node_idx = joint_nodes_indices[i]
                gltf.nodes[parent_node_idx].children.append(child_node_idx)

        # 9. Embedded PBR Textures (Albedo 2K, Normal Map 2K, Metallic-Roughness 2K)
        def _embed_texture_asset(path_str: Optional[str]) -> Optional[int]:
            if not path_str or not Path(path_str).exists():
                return None
            try:
                with open(path_str, "rb") as f:
                    img_data = f.read()
                img_bv = add_buffer_data(img_data)
                img_idx = len(gltf.images)
                mime = "image/png" if str(path_str).lower().endswith(".png") else "image/jpeg"
                gltf.images.append(pygltflib.Image(bufferView=img_bv, mimeType=mime))

                sampler_idx = len(gltf.samplers)
                gltf.samplers.append(
                    pygltflib.Sampler(
                        magFilter=pygltflib.LINEAR,
                        minFilter=pygltflib.LINEAR_MIPMAP_LINEAR,
                        wrapS=pygltflib.REPEAT,
                        wrapT=pygltflib.REPEAT,
                    )
                )

                tex_idx = len(gltf.textures)
                gltf.textures.append(pygltflib.Texture(sampler=sampler_idx, source=img_idx))
                return tex_idx
            except Exception as tex_err:
                self.logger.warning(f"PBR Texture embedding notice ('{path_str}'): {tex_err}")
                return None

        albedo_tex_idx = _embed_texture_asset(texture_path)
        normal_tex_idx = _embed_texture_asset(normal_map_path)
        mr_tex_idx = _embed_texture_asset(metallic_roughness_path)

        pbr_mr = pygltflib.PbrMetallicRoughness(
            baseColorFactor=[1.0, 1.0, 1.0, 1.0],
            roughnessFactor=0.85,
            metallicFactor=0.02,
        )
        if albedo_tex_idx is not None:
            pbr_mr.baseColorTexture = pygltflib.TextureInfo(index=albedo_tex_idx)
        if mr_tex_idx is not None:
            pbr_mr.metallicRoughnessTexture = pygltflib.TextureInfo(index=mr_tex_idx)

        mat_dict: Dict[str, Any] = {
            "name": "CharacterMaterial",
            "pbrMetallicRoughness": pbr_mr,
            "doubleSided": True,
        }
        if normal_tex_idx is not None:
            mat_dict["normalTexture"] = pygltflib.NormalMaterialTexture(index=normal_tex_idx, scale=1.0)

        gltf.materials.append(pygltflib.Material(**mat_dict))

        # Primitive Attributes
        prim_attrs_dict = {
            "POSITION": pos_acc,
            "NORMAL": norm_acc,
            "TEXCOORD_0": uv_acc,
            "JOINTS_0": joints_acc,
            "WEIGHTS_0": weights_acc,
        }
        if col_acc is not None and albedo_tex_idx is None:
            prim_attrs_dict["COLOR_0"] = col_acc

        gltf.meshes.append(
            pygltflib.Mesh(
                name="CharacterMesh",
                primitives=[
                    pygltflib.Primitive(
                        attributes=pygltflib.Attributes(**prim_attrs_dict),
                        indices=idx_acc,
                        material=0,
                    )
                ],
            )
        )

        # Skin definition
        gltf.skins.append(
            pygltflib.Skin(
                name="HumanoidArmature",
                inverseBindMatrices=ibm_acc,
                joints=joint_nodes_indices,
                skeleton=joint_nodes_indices[0],
            )
        )

        # Mesh node referencing Skin
        mesh_node_idx = len(gltf.nodes)
        gltf.nodes.append(
            pygltflib.Node(
                name="CharacterModel",
                mesh=0,
                skin=0,
            )
        )

        # Root Scene
        root_node_idx = len(gltf.nodes)
        gltf.nodes.append(
            pygltflib.Node(
                name="RootNode",
                children=[mesh_node_idx, joint_nodes_indices[0]],
            )
        )

        gltf.scenes.append(pygltflib.Scene(nodes=[root_node_idx]))
        gltf.scene = 0

        # Save GLB
        gltf.buffers.append(pygltflib.Buffer(byteLength=len(binary_blob)))
        gltf.set_binary_blob(bytes(binary_blob))
        gltf.save_binary(str(out_glb))

        file_size = out_glb.stat().st_size
        duration = self.logger.end_stage("Stage 3: Convert & Export GLB")

        # Step 3.2: Export Metadata JSON
        vram_stats = GPUManager.get_vram_status_mb()
        bounds_dim = (mesh.bounds[1] - mesh.bounds[0]).tolist()

        metadata = {
            "model_id": out_glb.stem,
            "format": "gltf_binary_humanoid_rigged",
            "has_skinning": True,
            "bone_count": len(rig_result.joint_names),
            "bone_names": rig_result.joint_names,
            "vertex_count": len(vertices),
            "triangle_count": len(faces),
            "bounding_box": {
                "min": mesh.bounds[0].tolist(),
                "max": mesh.bounds[1].tolist(),
                "dimensions": bounds_dim,
            },
            "file_size_bytes": file_size,
            "processing_time_seconds": round(total_pipeline_time + duration, 2),
            "gpu_memory_used_mb": vram_stats["allocated_mb"],
            "created_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        }

        with open(out_meta, "w", encoding="utf-8") as f:
            json.dump(metadata, f, indent=2)

        self.logger.info(f"Asset exported: {out_glb} ({file_size / (1024*1024):.2f} MB)")
        self.logger.info(f"Metadata exported: {out_meta}")

        return Stage3ExportResult(
            glb_path=str(out_glb),
            metadata_path=str(out_meta),
            metadata=metadata,
            duration_seconds=duration,
        )
