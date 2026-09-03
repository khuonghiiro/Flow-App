# FlowMy Coding & Modularity Standards

> This rule is automatically enforced on all AI sessions working in this workspace.

Before editing existing code or generating new code, AI must adhere to the following rules:

1. **File Size Limit (Universal Hard Rule across ALL Languages)**:
   - Target size: **150 – 500 lines per file**.
   - If a file reaches **800 – 1,000 lines**, you **MUST** split it into smaller modular files by function/responsibility (CSS, JS/TS, HTML, Python, C#, etc.).
   - **Never write monolithic files containing thousands of lines.**

2. **Method & Function Size Limit**:
   - Keep methods and functions under **50 – 80 lines**.
   - Decompose multi-step procedures into descriptive sub-methods and utility functions.

3. **Logic & Architecture Grouping**:
   - Separate concerns strictly: Models / Schemas, Controllers / API Routes, Core Engines, Utilities, and UI Presentation.
   - Frontend files must separate HTML, modular CSS (`theme.css`, `layout.css`, `controls.css`), and modular JS (`api_client.js`, `canvas_engine.js`, etc.).

4. **Thread Safety in WPF**:
   - Background threads (Task.Run, CefSharp interceptors, timers) **MUST NEVER** call blocking `Dispatcher.Invoke(...)`.
   - Use thread-safe cached instances or `IScopedOutputSync`.

5. **XAML UI Modularity & Theming**:
   - Keep `.xaml` files under **800 – 1,000 lines**. Split large layouts into sub-UserControls or ResourceDictionaries.
   - **Never hardcode raw colors**. Always use `{DynamicResource TokenKey}` from `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`.
   - **Button Padding**: When a `<Button>` specifies explicit `Width` and/or `Height`, **ALWAYS set `Padding="0"`** to prevent icon/text misalignment.
   - Place new custom styles in `FlowMy.Wpf-UI/Themes/Control_News/`.

For detailed documentation, read:
👉 `.agents/rules/code-modularity-limits.md`
👉 `FlowMy.Docs/AI_CODING_STANDARDS.md`
👉 `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`
