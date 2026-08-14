# FlowMy Coding & Modularity Standards

> This rule is automatically enforced on all AI sessions working in this workspace.

Before editing existing code or generating new code, AI must adhere to the following rules:

1. **File Size Limit (Hard Rule)**:
   - Target size: 200 – 600 lines per file.
   - If a file exceeds **1,000 – 1,500 lines**, you **MUST** split it into partial classes (`[Class].[Feature].cs`), dedicated services, or helpers.
   - Never write monolithic files containing thousands of lines.

2. **Method Size Limit**:
   - Keep methods under **50 – 80 lines**.
   - Decompose multi-step procedures into descriptive private sub-methods.

3. **Logic Grouping**:
   - Group related functionality into cohesive sub-files or specialized services.

4. **Thread Safety in WPF**:
   - Background threads (Task.Run, CefSharp interceptors, timers) **MUST NEVER** call blocking `Dispatcher.Invoke(...)`.
   - Use thread-safe cached instances or `IScopedOutputSync`.

5. **XAML UI Modularity & Theming**:
   - Keep `.xaml` files under **800 – 1,000 lines**. Split large layouts into sub-UserControls or ResourceDictionaries.
   - **Never hardcode raw colors**. Always use `{DynamicResource TokenKey}` from `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`.
   - **Button Padding**: When a `<Button>` specifies explicit `Width` and/or `Height`, **ALWAYS set `Padding="0"`** to prevent icon/text misalignment.
   - Place new custom styles in `FlowMy.Wpf-UI/Themes/Control_News/`.

6. **Header Comment**:
   Include the standardized header comment at line 1 of new or refactored files:
   ```csharp
   // =========================================================================================
   // AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
   // =========================================================================================
   ```

For detailed documentation, read:
👉 `FlowMy.Docs/AI_CODING_STANDARDS.md`
👉 `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`
