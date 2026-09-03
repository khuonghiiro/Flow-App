---
name: code-modularity-standards
description: Enforces file length limits (max 800-1000 lines), method limits (max 50-80 lines), universal modular file splitting across Python, JavaScript, TypeScript, CSS, HTML, C#, and clean multi-layered architecture.
---

# Universal Code Modularity & Line Limit Standards

When editing, refactoring, or creating new files in this workspace across any language (Python, JavaScript, TypeScript, CSS, HTML, C#, XAML), you MUST adhere to the following standards:

## 1. Hard File Size Limits
- **Optimal Size**: 150 – 500 lines per file.
- **Maximum Ceiling**: **800 – 1,000 lines**. Under NO circumstances should any file exceed 1,000 lines.
- **Action Required**: When a file approaches 800 lines, immediately identify distinct functional domains and extract them into dedicated sub-modules.

## 2. Function & Method Size Limit
- Functions and methods must stay within **50 – 80 lines**.
- Extract internal loops, complex transformations, and helper algorithms into descriptive private helper functions.

## 3. Technology-Specific Modular Patterns

### Python Web & AI Projects (e.g. FastAPI / PyTorch):
- `config.py`: Port, hardware detection (GPU/VRAM), directory paths.
- `schemas/`: Pydantic input/output validation models.
- `api/endpoints/`: Route handlers grouped by entity/feature.
- `core/`: Core business logic, physics algorithms, AI model pipelines.
- `utils/`: Common helpers (file I/O, image conversions, mask filters).

### Web Frontend (Vanilla JS / Modern HTML5 / CSS3):
- `index.html`: Clean semantic DOM tree, referencing modular stylesheets and scripts.
- `css/`: Split into `theme.css` (variables/tokens), `layout.css`, `canvas.css`, `controls.css`, `modals.css`.
- `js/`: Split into `api_client.js`, `canvas_engine.js`, `mask_painter.js`, `vector_tools.js`, `ui_controller.js`, `export_manager.js`, `app.js`.

### C# / WPF:
- Split massive view models / classes into partial classes (`[Class].[Feature].cs`) or services.
- Split XAML views into sub-UserControls or ResourceDictionaries under 800 lines.

## 4. Verification Check
Before concluding any task:
1. Check line counts of all modified or created files (`wc -l` or equivalent).
2. Confirm 0 syntax / compilation errors.
