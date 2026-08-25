---
name: code-modular-standards
description: Enforces file length limits (max 800-1000 lines), function limits (max 50-80 lines), modular UI/API decomposition, and clean separation of concerns for Python, JS, HTML, and CSS.
---

# Code Modular Standards & Decomposition Skill

Use this skill whenever reading, writing, refactoring, or reviewing code in the repository.

## Rules & Constraints

1. **Strict File Length Limits**:
   - **Target**: 150 – 500 lines per file.
   - **Hard Maximum**: 800 – 1000 lines.
   - If a file exceeds or is about to exceed 800 lines, automatically break it down into modular sub-files.

2. **Function & Method Limits**:
   - Keep methods between 20 – 50 lines (Maximum 80 lines).
   - Extract validation, math computations, data transformations, and rendering into isolated utility helpers.

3. **Modular Decomposition Strategy**:
   - **Python Backend**:
     - `app/config.py`: Environment and constants.
     - `app/schemas/`: Pydantic request/response structures.
     - `app/core/`: Dedicated modules for hardware/device management, AI inference pipelines, image pre/post-processing.
     - `app/api/endpoints/`: Group routes by resource (`rotation.py`, `background.py`, `system.py`).
   - **Web UI & Frontend**:
     - **CSS**: Split into `variables.css`, `layout.css`, `components.css`, `turntable.css`.
     - **JavaScript**: Split into `api.js`, `orbit_gizmo.js`, `turntable_viewer.js`, `gallery.js`, `app.js`.

4. **Safety & Cleanliness**:
   - Never commit heavy `.pt`, `.pth`, `.onnx` checkpoints or `venv/` to Git.
   - Isolate execution inside local virtual environment.
