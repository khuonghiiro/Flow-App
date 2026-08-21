# Image-to-Rig Pipeline Module

A modular, production-ready Python pipeline for transforming 2D character images into fully rigged glTF 2.0 (`.glb`) humanoid assets ready for direct consumption in Three.js and real-time WebGL engines.

Optimized for local workstations equipped with **NVIDIA RTX 3060 12GB VRAM** and 32GB system RAM.

---

## 🌟 Key Features

- **Stage 1 (Image-to-Mesh)**: Integrated with **TripoSR** (`models/triposr` / `stabilityai/TripoSR`) for NeRF/isosurface reconstruction and UV texture atlas baking.
- **Stage 2 (Auto-Rigging)**: Integrated with **UniRig** (`models/unirig` / `VAST-AI-Research/UniRig`) for 3D humanoid skeleton landmark estimation and continuous vertex skinning weights.
- **Stage 3 (Convert & Export)**: Standards-compliant glTF 2.0 binary (`.glb`) generator with embedded inverse bind matrices, skinning weights (`JOINTS_0`, `WEIGHTS_0`), and automated companion `metadata.json` catalog export.
- **Stage 4 (Dual-Branch Facial Blendshapes)**:
  - **Branch A (VRM/VRoid Characters)**: Native extraction and preservation of 52+ ARKit visemes and expression morph targets.
  - **Branch B (Image-Generated Meshes)**: Face mesh extraction tool and Blender sculpting helper (`blender_blendshape_helper.py`) for rapid 2-3 minute manual shape key sculpting.
- **Root `models/` Directory Management**: Centralized checkpoint management with 1-click downloading script (`python download_models.py`) and UI manager.
- **AI Prompt & Character Creation Guide**: Built-in interactive cheat-sheet on the Web UI covering dimensions, framing, A-poses, garments (shirts, pants), footwear, hair, hats/accessories, and PBR texture synthesis.
- **GPU Mutex & Queue**: Strict single-worker concurrency lock preventing out-of-memory errors on 12GB VRAM.
- **Dual Interfaces**: Interactive Gradio Web UI (`python app.py`) and FastAPI REST Server (`python server.py`).
- **One-Click Automation**: `_start.bat` for automatic API server initialization, Gradio UI launch, and browser popup.

---

## 📁 Repository Architecture & Component Responsibilities

| File / Directory | Responsibility & Details |
| :--- | :--- |
| [`models/`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/models/) | **Centralized AI Models Root**: Stores TripoSR (~1.7GB), RemBG U2Net (~176MB), and UniRig checkpoints |
| [`image_to_rig/tools/model_downloader.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/tools/model_downloader.py) | **Model Downloader**: Automated checkpoint retrieval, storage inspection, and progress tracking |
| [`image_to_rig/ui/prompt_guide.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/prompt_guide.py) | **AI Prompt Guide**: Technical character design rules, PBR materials, and copyable prompt templates |
| [`image_to_rig/ui/models_tab.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/models_tab.py) | **Models UI Tab**: Gradio dashboard for 1-click model downloading and disk usage metrics |
| [`download_models.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/download_models.py) | **CLI Model Downloader**: Standalone root script to fetch missing models into `models/` |
| [`image_to_rig/config.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/config.py) | **VRAM & Hardware Config**: RTX 3060 12GB tuning, `models/` paths, `chunk_size = 8192`, symmetry thresholds |
| [`image_to_rig/core/queue_manager.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/queue_manager.py) | **GPU Mutex Queue**: Single-worker semaphore queue protecting 12GB VRAM against concurrent GPU memory crashes |
| [`image_to_rig/core/validator.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/validator.py) | **Validation Guard**: Image dimension/contrast check and bilateral X-axis mesh symmetry enforcement |
| [`image_to_rig/core/stage1_mesh.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage1_mesh.py) | **Stage 1 (Image-to-Mesh)**: Preprocessing, background removal, TripoSR NeRF inference, and texture baking |
| [`image_to_rig/core/stage2_rig.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage2_rig.py) | **Stage 2 (Auto-Rigging)**: UniRig skeleton prediction, humanoid hierarchy mapping, continuous skinning weights |
| [`image_to_rig/core/stage3_export.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage3_export.py) | **Stage 3 (GLB Export)**: Pure-Python glTF 2.0 binary assembler with skins + `metadata.json` asset catalog export |
| [`image_to_rig/core/stage4_blendshapes.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage4_blendshapes.py) | **Stage 4 (Blendshapes)**: Branch A (VRM ARKit preservation) & Branch B (Face scaffold & shape key generator) |
| [`image_to_rig/core/pipeline.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/pipeline.py) | **Pipeline Orchestrator**: High-level execution manager connecting Stages 1 to 4 with stage progress callbacks |
| [`image_to_rig/ui/gradio_app.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/gradio_app.py) | **Gradio Web UI**: Interactive dashboard with 3D model viewport, prompt guide, and model manager |
| [`image_to_rig/api/server.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/api/server.py) | **FastAPI REST Server**: Endpoints for Studio TypeScript/Node.js backend communication |
| [`_start.bat`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/_start.bat) | **One-Click Launcher**: Auto-starts FastAPI backend, Gradio Web UI, and launches web browser |
| [`app.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/app.py) | Standalone entrypoint for Gradio Web UI (`http://localhost:7860`) |
| [`server.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/server.py) | Standalone entrypoint for FastAPI REST server (`http://localhost:8000`) |

---

## 🚀 Quickstart

### 1. Installation & Environment Setup
**Windows**:
```bat
setup.bat
```

**Linux / macOS**:
```bash
chmod +x setup.sh
./setup.sh
```

### 2. Download AI Model Weights (Optional / Pre-caching)
```powershell
python download_models.py
```

### 3. One-Click Launch (Recommended)
Double click on `_start.bat` or run:
```bat
_start.bat
```
This automatically launches the FastAPI REST server in the background, spins up the Gradio Web UI, and opens `http://localhost:7860` in your web browser.

---

## 🎨 AI Image Generation & PBR Material Guidelines

For optimal 3D mesh reconstruction and humanoid rigging:
- **Dimensions**: `1024x1024` (1:1 square) or `896x1152` (3:4 full body).
- **Framing**: Character should occupy **80-85%** of the frame, with 10% padding above head and below feet.
- **Pose**: Symmetrical **A-Pose** (arms 45° from torso, relaxed open fingers, feet shoulder-width apart on flat ground).
- **Garments**: Clean seam lines, distinct arm-torso gaps, separated thigh/pant legs.
- **Footwear**: Flat ground contact, clear ankle transition.
- **Hair & Accessories**: Solid clean clumps, clear neck-shoulder gap, no handheld weapons or props attached to hands.
- **Lighting**: Diffused soft studio lighting (`#808080` or `#FFFFFF` background), avoid dark baked shadows.

---

## 📄 License
MIT License. Built for Studio 3D Animation Pipelines.
