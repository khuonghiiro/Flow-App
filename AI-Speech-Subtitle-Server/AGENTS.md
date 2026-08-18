# Antigravity Workspace Guidelines: File Length & Code Modularization

## 1. Hard Rules on File Size & Modularity
- **Max File Limit**: No file (`.py`, `.js`, `.css`, `.html`) must exceed **1,000 lines**.
- **Proactive Modularization**: When a file reaches **500 - 600 lines**, proactively split and decouple logic into domain-specific modules before adding more features.
- **Function Limit**: Keep functions and methods below **80 lines** (ideally 30-50 lines). Extract helper routines.

## 2. Separation of Concerns by Language
- **Python (`.py`)**:
  - `models/` or `schemas.py`: Pydantic models, request/response schemas.
  - `routers/`: Modular FastAPI/Flask routers (`APIRouter`) per feature domain.
  - `engines/` or `services/`: Heavy business, ML, and audio processing logic.
  - `utils/`: Reusable standalone helper functions.
- **JavaScript (`.js`)**:
  - Separate state management, API networking (`fetch`/`FormData`), UI rendering (`components/`), event listeners, and data formatters into dedicated files.
- **CSS (`.css`)**:
  - Separate variables/tokens (`variables.css`), layout shells (`layout.css`), and modular component styles (`components/`).
- **HTML (`.html`)**:
  - Strictly no inline JavaScript (`<script>` inside HTML body) or inline CSS (`style="..."`). Use external linked assets.
