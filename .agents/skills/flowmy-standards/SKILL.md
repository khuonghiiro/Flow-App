---
name: flowmy-standards
description: Enforces file length limits (max 800-1000 lines), method limits (max 50-80 lines), modular UI tab extraction, and clean architecture when editing or creating code in FlowMy and AI Studio.
---

# FlowMy & AI Studio Coding Standards

When modifying, extending, or creating code in FlowMy workspaces (across all languages: Python, JavaScript, TypeScript, CSS, HTML, C#, XAML), you MUST follow these mandatory standards:

## 1. File Size Limits (Hard Rule)
- **Target Size**: 150 – 500 lines per file.
- **Maximum Threshold**: Never exceed **800 – 1,000 lines** in any file.
- **Pre-Edit Inspection**: Always check line count before adding new code to a file.
- **Decomposition**: If a file is approaching 800–1000 lines, you MUST extract sub-components, helper modules, custom hooks, or services into dedicated sub-folders.
- **UI Tabs & Views**: For complex modals or tabbed interfaces, NEVER stuff all tabs into one file. Extract each tab / panel into its own sub-component.

## 2. Method Size Limit
- Keep methods and functions under **50 – 80 lines**.
- Decompose complex procedures into private sub-methods and pure utility functions.

## 3. Universal Modularity Across Languages
- **Python**: Decompose into `config.py`, `schemas/`, `api/endpoints/`, `core/`, `utils/`.
- **CSS**: Decompose into `theme.css`, `layout.css`, `canvas.css`, `controls.css`, `modal.css`.
- **JS/TS**: Decompose into `api_client.js`, `canvas_engine.js`, `mask_painter.js`, `vector_tools.js`, `ui_controller.js`, `export_manager.js`, `app.js`.
- **C# / WPF**: Split large partial classes and UserControls.

## 4. Verification
- Always ensure 0 compilation / syntax errors.
