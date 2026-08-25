# AI-Image-Rotate Workspace Instructions

## Coding Standards & Architectural Rules
1. **File Length Limit**: Every source file (Python, JavaScript, CSS, HTML, etc.) MUST NOT exceed 800 - 1000 lines. Target 150 - 500 lines per file.
2. **Method Limit**: Keep functions under 50 - 80 lines.
3. **Modular Decomposition**: Separate concerns strictly into dedicated directories:
   - `app/api/endpoints/`: Routing layer
   - `app/core/`: Business logic & AI pipelines (DeviceManager, Preprocessor, Zero123Pipeline, Postprocessor)
   - `app/schemas/`: Pydantic models
   - `app/static/css/`: Modular stylesheets (`variables.css`, `layout.css`, `components.css`, `turntable.css`)
   - `app/static/js/`: Modular frontend logic (`api.js`, `orbit_gizmo.js`, `turntable_viewer.js`, `gallery.js`, `app.js`)
4. **Target Hardware**: NVIDIA GeForce RTX 3060 12GB VRAM. Use FP16, cache cleanup, and efficient memory management.
5. **Port**: 3978 (FastAPI backend + Static UI + Swagger at `/docs`).
6. **Environment**: Isolate within local virtual environment (`venv`). Keep `.gitignore` updated.
