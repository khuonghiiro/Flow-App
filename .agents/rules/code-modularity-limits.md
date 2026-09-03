# Universal Code Modularity & Line Limit Standards

> This rule is automatically enforced across ALL projects and languages in this workspace.

## 1. Absolute File Line Limits (Hard Rule)
- **Target Size**: 150 – 500 lines per file.
- **Maximum Threshold**: **NEVER exceed 800 – 1,000 lines in ANY file.**
- **Applicable Languages**: Python (`.py`), JavaScript (`.js`), TypeScript (`.ts`, `.tsx`), CSS (`.css`), HTML (`.html`), C# (`.cs`), XAML (`.xaml`), etc.
- **Enforcement Action**: If any file approaches 800 lines, you MUST decompose and split it into smaller, cohesive modules according to its specific responsibilities before writing more code.

## 2. Method / Function Size Limit
- Keep individual functions or methods under **50 – 80 lines**.
- Break complex algorithms or procedures into private helper functions, specialized pipeline steps, or pure utility functions.

## 3. Modular Separation by Technology:

### A. Python Backend
- **Separate Layers**:
  - `schemas/`: Pydantic models for request & response validation.
  - `api/endpoints/`: Endpoint routes (one file per domain / feature).
  - `core/`: Business logic, AI pipeline, device management, rendering physics.
  - `utils/`: Reusable helper utilities (image processing, file I/O, hashing).
  - `config.py`: Centralized environment and hardware configuration.
- **Never create monolithic `main.py`** containing routes, schemas, and processing algorithms in a single file.

### B. Frontend (HTML, CSS, JavaScript / TypeScript)
- **HTML**: Keep semantic, minimal layout without embedded `<style>` or massive `<script>` tags.
- **CSS**: Separate styles into modular stylesheets:
  - `theme.css`: Colors, variables, typography, glassmorphism tokens.
  - `layout.css`: Header, sidebar, main grid, responsive breakpoints.
  - `canvas.css`: Viewport, interactive canvas overlays, zoom/pan.
  - `controls.css`: Buttons, sliders, switches, preset badges.
  - `modals.css`: Dialogs, export popups, integration guides.
- **JavaScript / TypeScript**: Separate into distinct service/module files:
  - `api_client.js`: HTTP fetch & WebSocket communication.
  - `mask_painter.js`: Brush, eraser, and mask rendering logic.
  - `vector_tools.js`: Motion arrow and anchor pin interaction logic.
  - `canvas_engine.js`: Real-time rendering / WebGL / Canvas physics simulation.
  - `ui_controller.js`: DOM event listeners, slider bindings, tabs.
  - `export_manager.js`: Export dialogs, format conversion, downloads.
  - `app.js`: Application bootstrapper and lifecycle.

### C. C# / WPF
- Separate into partial classes (`[Class].[Feature].cs`), dedicated Services, and sub-UserControls.
- Keep `.xaml` views under 800 lines by extracting child components and ResourceDictionaries.
