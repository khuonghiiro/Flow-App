# AI-Image-Animation Workspace Instructions

## Coding Standards & Architectural Rules
1. **File Length Limit**: Every source file (Python, JavaScript, CSS, HTML, etc.) MUST NOT exceed 800 - 1000 lines. Target: 150 - 500 lines per file.
2. **Method / Function Limit**: Keep functions under 50 - 80 lines.
3. **Modular Decomposition**: Separate concerns strictly into dedicated directories:
   - `app/api/endpoints/`: Routing layer (one file per entity/feature)
   - `app/core/`: Business logic, physics flow engines, and AI pipelines
   - `app/schemas/`: Pydantic models (request & response)
   - `app/static/css/`: Modular stylesheets (`theme.css`, `layout.css`, `canvas.css`, `controls.css`, `api_modal.css`)
   - `app/static/js/`: Modular frontend logic (`api_client.js`, `mask_painter.js`, `vector_tools.js`, `canvas_engine.js`, `ui_controller.js`, `export_manager.js`, `app.js`)
4. **Target Hardware**: NVIDIA GeForce RTX 3060 12GB VRAM. Use FP16, automatic VRAM cleanup, and efficient tensor allocation.
5. **Port**: 3979 (FastAPI backend + Static UI + Swagger at `/docs`).
6. **Environment**: Isolate strictly within local virtual environment (`venv`). Keep `.gitignore` updated.
7. **External API Compatibility**: Keep CORS fully enabled (`*`) and provide clean JSON/REST/WebSocket interfaces.
