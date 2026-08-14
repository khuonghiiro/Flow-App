# AI CODING & MODULAR ARCHITECTURE STANDARDS

> **Target Audience**: AI Coding Assistants (Antigravity, Gemini, GitHub Copilot, Cursor, Claude, ChatGPT)  
> **Repository**: FlowMy (WPF & .NET 8 Multi-Project Solution)

---

## 1. FILE LENGTH & MODULARITY GUIDELINES

### 1.1 Line Count Thresholds
- **Optimal Range**: **200 – 600 lines** per file.
- **Warning Threshold**: **> 1,000 lines**.
- **Hard Limit**: **Never allow any file to exceed 1,200 – 1,500 lines**.
- When an existing file approaches 1,000 lines, or when adding a new feature that inflates a file, **YOU MUST SPLIT** the file into cohesive partial classes or dedicated sub-services.

### 1.2 Separation & Decomposition Patterns

#### A. Partial Classes for Views, ViewModels, and Complex Nodes:
Use the naming convention: `[ClassName].[FeatureGroup].cs`
- **Examples**:
  - `WebNode.cs` (Core properties, metadata, and lifecycle)
  - `WebNode.NetworkInterceptor.cs` (CefSharp request/response handling and filtering)
  - `WebNode.DataExtraction.cs` (cURL, headers, payload, and params extraction)
  - `WebNode.ExecutionRun.cs` (Thread-safe state management for active workflow runs)
  - `WorkflowEditorWindow.xaml.cs` (Window lifecycle, DI, and core properties)
  - `WorkflowEditorWindow.CanvasEvents.cs` (Mouse, pan, zoom, and selection interactions)
  - `WorkflowEditorWindow.ConnectionHandler.cs` (Wire routing and port linking logic)

#### B. Service-Based Separation:
- Extract heavy computation, business operations, and background tasks into separate classes in `Services/` or `FlowMy.Core/Utils/`.
- Depend on interfaces (e.g., `IScopedOutputSync`, `IWorkflowExecutionVisualizer`) to keep `FlowMy.Core` decoupled from `FlowMy.Wpf-UI`.

#### C. Pure Utility & Helper Functions:
- Place pure, side-effect-free helper routines into `FlowMy.Core/Helpers/` or `FlowMy.Core/Extensions/`.

---

## 2. METHOD & FUNCTION STANDARDS

1. **Method Size Limit**:
   - Maximum **50 – 80 lines** per method.
   - Any method handling multi-step logic (e.g., Validate -> Extract -> Transform -> UI Update -> Signal) **MUST** be decomposed into concise, descriptive private sub-methods:
     - `ValidateRequestPayload(...)`
     - `ParseResponseHeaders(...)`
     - `DispatchScopedOutputUpdate(...)`
2. **Single Responsibility Principle (SRP)**:
   - Each method must perform exactly **ONE** task well.
   - Avoid deeply nested control flow (more than 3 levels of `if`/`switch`/`loop`). Use early returns / guard clauses.
3. **State Immutability & Thread Safety**:
   - Avoid mutating shared state across threads without proper synchronization (`lock`, `ConcurrentDictionary`, `Interlocked`).

---

## 3. MANDATORY AI HEADER COMMENT

Every new C# file (`.cs`) or refactored/split file should include the following standardized English header comment at line 1 directing AI assistants to the centralized documentation:

```csharp
// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
```

---

## 4. MULTI-THREADING & WPF DISPATCHER RULES

1. **Non-Blocking Background Threads**:
   - Background workers (CefSharp network interceptors, `Task.Run`, timer callbacks) **MUST NEVER** invoke blocking `Dispatcher.Invoke(...)` on UI controls or `DependencyObject`s.
   - Use memory-cached fields (e.g., `_cachedViewModel`), thread-safe interfaces (`IScopedOutputSync`), or `Dispatcher.BeginInvoke(...)` with low priority when updating UI displays.
2. **Win32 & Screen Metrics**:
   - Avoid calling WPF `SystemParameters` inside background worker constructors. Use thread-safe Win32 `GetSystemMetrics` via `WindowHelper`.

---

## 5. XAML UI & THEME TOKEN RULES

1. **XAML Modularity & Line Limit**:
   - Keep `.xaml` files under **800 – 1,000 lines**.
   - For large dialogs, panels, or complex node contents, decompose layouts into smaller `UserControl`s or split `ResourceDictionary` files.
2. **Strict DynamicResource Theming**:
   - **NEVER** hardcode raw hex colors (e.g. `Background="#FF1A1B1E"`) or named colors in XAML.
   - **ALWAYS** use `{DynamicResource TokenKey}` matching tokens in `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`.
3. **Button Padding Rule (Explicit Width/Height)**:
   - When a `<Button>` specifies explicit `Width` and/or `Height` (e.g., icon buttons `Width="32" Height="32"`), **YOU MUST SET `Padding="0"`** to prevent default style padding from misaligning or clipping the inner icon or text.
   ```xml
   <!-- ✅ Correct: Fixed size button with Padding="0" -->
   <Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32" Padding="0">
       <controls:SvgViewboxEx Source="Assets/Icons/edit.svg" Width="14" Height="14"/>
   </Button>
   ```
4. **Custom Styles Directory**:
   - When creating custom styles or control templates for new features, place the `.xaml` files in `FlowMy.Wpf-UI/Themes/Control_News/`.

---

## 6. BUILD PERFORMANCE (INCREMENTAL BUILDS < 5s)

1. **Solution Structure**:
   - `FlowMy.Core`: Lightweight Class Library containing Models, Interfaces, Utilities, and Helpers.
   - `FlowMy.Wpf-UI`: Main Presentation application referencing `FlowMy.Core`.
2. **Keep Incremental Compilation Fast**:
   - Avoid unnecessary inter-assembly dependencies or cyclic references.

---

## 7. PRE-COMMIT AI CHECKLIST

Before completing any task, ensure:
- [ ] No C# file exceeds 1,000 lines (and no XAML file exceeds 800-1,000 lines).
- [ ] No method exceeds 80 lines.
- [ ] XAML uses `{DynamicResource ...}` theme tokens (no hardcoded colors); new styles are in `Themes/Control_News/`.
- [ ] Header comment referencing `README.md` and `FlowMy.Docs/AI_CODING_STANDARDS.md` is present.
- [ ] Code is thread-safe and non-blocking.
- [ ] Solution compiles cleanly with zero errors (`dotnet build FlowMy.sln`).
