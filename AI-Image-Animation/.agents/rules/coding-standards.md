# AI-Image-Animation Coding Standards

> Enforced on all AI coding sessions in this repository.

1. **Strict File Size Ceiling**: Under NO circumstances may any file exceed 800 - 1000 lines. Target 150 - 500 lines.
2. **Method Size Limit**: Maximum 50 - 80 lines per function.
3. **Modular Layering**:
   - Backend: `main.py`, `config.py`, `schemas/`, `api/endpoints/`, `core/`, `utils/`
   - Frontend: `index.html`, `css/` (modular), `js/` (modular services).
4. **VRAM Safety**: Always guard RTX 3060 12GB VRAM with FP16 and garbage collection.
5. **Port**: 3979.
