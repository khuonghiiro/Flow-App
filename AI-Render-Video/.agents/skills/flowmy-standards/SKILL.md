---
name: flowmy-standards
description: Enforces file length limits (max 800-1000 lines), method limits (max 50-80 lines), modular UI tab extraction, and clean architecture when editing or creating code in FlowMy and AI Studio.
---

# FlowMy & AI Studio Coding Standards

When modifying, extending, or creating code in FlowMy workspaces (both C#/WPF and TypeScript/React/Vite), you MUST follow these mandatory standards:

## 1. File Size Limits (Hard Rule)
- **Target Size**: 200 – 600 lines per file.
- **Maximum Threshold**: Never exceed **800 – 1,000 lines** in any file.
- **Pre-Edit Inspection**: Always check line count before adding new code to a file.
- **Decomposition**: If a file is approaching 800–1000 lines, you MUST extract sub-components, helper modules, custom hooks, or services into dedicated sub-folders.
- **UI Tabs & Views**: For complex modals or tabbed interfaces, NEVER stuff all tabs into one file. Extract each tab / panel into its own sub-component (e.g. `src/ui/studio2d/catalog/`).

## 2. Method Size Limit
- Keep methods and functions under **50 – 80 lines**.
- Decompose complex procedures into private sub-methods and pure utility functions.

## 3. Component & State Modularity
- Group cohesive features together in modular directories.
- Separate business/data logic (Storage, Registries, State engines) from presentation components (JSX/Canvas/WebGL viewports).

## 4. UI Layout & User Experience
- **RPG / 3D Customizer Layouts**: Main viewport on Column 1 with equipment slots overlay/HUD, materials/catalogs on Column 2, and structure controls in dedicated panels.
- Maintain responsive, dark-mode, glassmorphism aesthetics.

## 5. Verification
- Always run `npm run build` to ensure 0 compilation errors.
