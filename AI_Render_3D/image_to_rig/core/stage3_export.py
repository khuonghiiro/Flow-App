"""
Stage 3: glTF 2.0 Binary (.glb) Exporter and Asset Metadata Generator.
Builds valid skinned humanoid meshes with joint hierarchies and inverse bind matrices.
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
    """Exports rigged mesh and skeleton into standard glTF 2.0 binary (.glb) files."""

    def __init__(self, config: ExportConfig):
        self.config = config
        self.logger = get_logger()

    def _compute_inverse_bind_matrices(
        self, joint_positions: np.ndarray
    ) -> np.ndarray:
        """Compute $4 \\times 4$ column-major inverse bind matrices for all skeleton joints."""
        num_joints = len(joint_positions)
        ibms = np.zeros((num_joints, 4, 4), dtype=np.float32)
        for i in range(num_joints):
            mat = np.eye(4, dtype=np.float32)
            mat[0:3, 3] = -joint_positions[i]
            # Column-major format for glTF specification
            ibms[i] = mat.T
        return ibms

    def export_rigged_glb(
        self,
        rig_result: Stage2RigResult,
        output_glb_path: str,
        texture_path: Optional[str] = None,
        total_pipeline_time: float = 0.0,
    ) -> Stage3ExportResult:
        """
        Assemble complete glTF 2.0 binary containing skinned mesh, joint hierarchy,
        inverse bind matrices, materials, and generate companion metadata JSON.
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

        # Fallback UV coordinates if not present
        if hasattr(mesh.visual, "uv") and mesh.visual.uv is not None and len(mesh.visual.uv) == len(vertices):
            uvs = np.array(mesh.visual.uv, dtype=np.float32)
        else:
            # Cylindrical procedural UV mapping
            theta = np.arctan2(vertices[:, 0], vertices[:, 2]) / (2 * np.pi) + 0.5
            y_norm = (vertices[:, 1] - mesh.bounds[0, 1]) / (mesh.bounds[1, 1] - mesh.bounds[0, 1] + 1e-5)
            uvs = np.column_stack([theta, y_norm]).astype(np.float32)

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

        # 5. Joints (JOINTS_0)
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

        # 6. Weights (WEIGHTS_0)
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

        # 7. Inverse Bind Matrices (IBM)
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
            # If root (parent -1), position is relative to origin; otherwise relative to parent
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

        # Mesh Definition
        gltf.meshes.append(
            pygltflib.Mesh(
                name="CharacterMesh",
                primitives=[
                    pygltflib.Primitive(
                        attributes=pygltflib.Attributes(
                            POSITION=pos_acc,
                            NORMAL=norm_acc,
                            TEXCOORD_0=uv_acc,
                            JOINTS_0=joints_acc,
                            WEIGHTS_0=weights_acc,
                        ),
                        indices=idx_acc,
                        material=0,
                    )
                ],
            )
        )

        # Materials
        gltf.materials.append(
            pygltflib.Material(
                name="CharacterMaterial",
                pbrMetallicRoughness=pygltflib.PbrMetallicRoughness(
                    baseColorFactor=[0.9, 0.9, 0.9, 1.0],
                    roughnessFactor=0.6,
                    metallicFactor=0.1,
                ),
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

        # Step 3.2: Export Metadata JSON for Studio Asset Scanner
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
