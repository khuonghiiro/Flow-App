# Image-to-Rig Pipeline Module

A modular, production-ready Python pipeline for transforming 2D character images into fully rigged glTF 2.0 (`.glb`) humanoid assets ready for direct consumption in Three.js and real-time WebGL engines.

Optimized for local workstations equipped with **NVIDIA RTX 3060 12GB VRAM** and 32GB system RAM.

---

## 🌟 Key Features

- **Stage 1 (Image-to-Mesh)**: Integrated with **TripoSR** (`stabilityai/TripoSR`) for NeRF/isosurface reconstruction and UV texture atlas baking.
- **Stage 2 (Auto-Rigging)**: Integrated with **UniRig** (`VAST-AI-Research/UniRig`) for 3D humanoid skeleton landmark estimation and continuous vertex skinning weights.
- **Stage 3 (Convert & Export)**: Standards-compliant glTF 2.0 binary (`.glb`) generator with embedded inverse bind matrices, skinning weights (`JOINTS_0`, `WEIGHTS_0`), and automated companion `metadata.json` catalog export.
- **Stage 4 (Dual-Branch Facial Blendshapes)**:
  - **Branch A (VRM/VRoid Characters)**: Native extraction and preservation of 52+ ARKit visemes and expression morph targets.
  - **Branch B (Image-Generated Meshes)**: Face mesh extraction tool and Blender sculpting helper (`blender_blendshape_helper.py`) for rapid 2-3 minute manual shape key sculpting.
- **GPU Mutex & Queue**: Strict single-worker concurrency lock preventing out-of-memory errors on 12GB VRAM.
- **Dual Interfaces**: Interactive Gradio Web UI (`python app.py`) and FastAPI REST Server (`python server.py`).

---

## 📁 Repository Architecture

```
AI_Render_3D/
├── image_to_rig/
│   ├── config.py                 # Hardware thresholds, VRAM limits, paths
│   ├── core/
│   │   ├── queue_manager.py      # Thread-safe GPU mutex job queue
│   │   ├── validator.py          # Input image & mesh bilateral symmetry validator
│   │   ├── stage1_mesh.py        # TripoSR image-to-mesh runner
│   │   ├── stage2_rig.py         # UniRig skeleton & skinning predictor
│   │   ├── stage3_export.py      # glTF 2.0 binary (.glb) assembler & metadata exporter
│   │   ├── stage4_blendshapes.py # VRM blendshape keeper & face sculpt scaffold generator
│   │   └── pipeline.py           # Master end-to-end orchestrator
│   ├── api/
│   │   ├── schemas.py            # Pydantic models for API request/response
│   │   └── server.py             # FastAPI REST endpoints
│   ├── ui/
│   │   └── gradio_app.py         # Gradio 3D Web UI with real-time Model3D viewer
│   ├── tools/
│   │   ├── blender_headless_export.py   # Headless Blender glTF exporter
│   │   └── blender_blendshape_helper.py # Blender facial shape key sculpt operator
│   └── utils/
│       ├── gpu_utils.py          # CUDA VRAM monitoring and garbage collector
│       └── logger.py             # Stage-aware structured logger
├── app.py                        # Gradio Web UI entrypoint (localhost:7860)
├── server.py                     # FastAPI REST server entrypoint (localhost:8000)
├── setup.bat                     # Windows setup automation (PyTorch CUDA 12.1)
├── setup.sh                      # Linux/WSL2 setup script
├── requirements.txt              # Pinned Python package dependencies
├── examples/
│   ├── threejs_loader_example.ts # TypeScript Three.js asset loader
│   └── sample_viewer.html        # Standalone WebGL 3D viewer & animation tester
└── tests/                        # Pytest unit & integration test suite
```

---

## 🚀 Quickstart

### 1. Installation

**Windows**:
```bat
setup.bat
```

**Linux / macOS**:
```bash
chmod +x setup.sh
./setup.sh
```

### 2. Launch Gradio Web UI
```bash
python app.py
```
Open [http://localhost:7860](http://localhost:7860) in your web browser.

### 3. Launch FastAPI REST Server
```bash
python server.py
```
Interactive Swagger API documentation available at [http://localhost:8000/docs](http://localhost:8000/docs).

---

## 🔌 API & TypeScript Integration

### Python Pipeline Programmatic Usage
```python
from image_to_rig.core.pipeline import ImageToRigPipeline

pipeline = ImageToRigPipeline()
result = pipeline.run_pipeline("character.png", "outputs/hero_rigged.glb")

if result.success:
    print(f"Rigged GLB: {result.glb_path}")
    print(f"Metadata: {result.metadata_path}")
```

### TypeScript / Three.js Loader
```typescript
import { loadRiggedCharacter, playWaveAnimation } from './examples/threejs_loader_example';

const character = await loadRiggedCharacter('/outputs/hero_rigged.glb');
scene.add(character.scene);

// Render loop animation
function animate(time: number) {
  playWaveAnimation(character, time);
  renderer.render(scene, camera);
}
```

---

## 🎭 Facial Blendshape Technical Specification

TripoSR and UniRig generate rigid and skinned skeletons; **they do not automatically synthesize facial morph targets**.

1. **Branch A (`.vrm`)**: Directly preserve all ARKit morph targets during conversion.
2. **Branch B (Image to Mesh)**: Output includes `<name>_face_scaffold.obj` and `<name>_setup_shapekeys.py`. Open in Blender to sculpt `mouth_open`, `eye_blink_left`, `eye_blink_right`, and `smile` in under 3 minutes.

---

## 📄 License
MIT License. Built for Studio 3D Animation Pipelines.
