# FlowMy - Desktop Automation & Workflow Engine

An advanced desktop workflow automation application built with **.NET 8** and **WPF**, featuring a visual node graph canvas, embedded Chromium browser interception (CefSharp / WebView2), and high-performance multi-threaded task orchestration.

---

## 🤖 Mandatory Rules for AI Assistants (Antigravity / Gemini / Copilot / Cursor)

When creating, editing, or refactoring code in this repository, **AI MUST STRICTLY FOLLOW**:
👉 Full Guide: [AI_CODING_STANDARDS.md](FlowMy.Docs/AI_CODING_STANDARDS.md) | [DEV_GUIDELINES_VI.md](FlowMy.Docs/DEV_GUIDELINES_VI.md)

1. **File Length Limits**:
   - Optimal: **200 – 600 lines**.
   - Hard Limit: **Never exceed 1,000 – 1,500 lines**.
   - If a file exceeds 1,000 lines, **BẮT BUỘC** split into partial files (`[ClassName].[FeatureGroup].cs`), dedicated services, or helpers.
2. **Method Length Limits**:
   - Keep methods under **50 – 80 lines**. Split complex workflows into private sub-methods.
3. **Logic Grouping & Modularity**:
   - Group related features into cohesive modules. Do not overload a single file or class.
4. **Thread Safety**:
   - Background threads (Task, CefSharp interceptors) **MUST NEVER** call blocking `Dispatcher.Invoke(...)`. Use cached state or thread-safe interfaces (`IScopedOutputSync`).

### Standard Header Comment for Source Files:
```csharp
// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
```

---

## 📁 Solution Architecture

```text
Flow-My/
├── FlowMy.sln                    → Visual Studio Solution file
├── Directory.Build.props         → Shared build optimization properties (Fast Incremental Builds)
├── FlowMy.Core/                  → Core Class Library (.NET 8 WPF)
│   ├── Models/                   → Node definitions, data models, and persistence DTOs
│   ├── Interfaces/               → Decoupled interfaces (IScopedOutputSync, etc.)
│   ├── Helpers/                  → Win32 and system helpers (WindowHelper, etc.)
│   └── Utils/                    → Parsing and calculation utilities
├── FlowMy.Wpf-UI/                → Presentation & UI Application (.NET 8 WinExe)
│   ├── Views/                    → Canvas, Node Controls, Dialogs, and Overlays
│   ├── ViewModels/               → MVVM ViewModels and execution orchestrators
│   ├── Services/                 → Workflow execution, rendering, and interaction services
│   ├── Controls/                 → Custom UI controls
│   └── Themes/                   → Styles, brush palettes, and resource dictionaries
└── FlowMy.Docs/                  → Documentation & Development Guidelines
    ├── AI_CODING_STANDARDS.md    → Coding standards & modularity rules (English for AI Assistants)
    ├── DEV_GUIDELINES_VI.md      → Hướng dẫn phân tách code & quy chuẩn (Tiếng Việt cho Developers)
    ├── AGENTS.md / GEMINI.md     → AI Workspace instructions
    └── wpf-docs/                 → Detailed architecture and node reference docs
```

---

## 📖 Guidelines & Documentation

- **For AI Coding Assistants**: [AI Coding & Modularity Standards (English)](FlowMy.Docs/AI_CODING_STANDARDS.md)
- **Dành cho Lập trình viên**: [Quy chuẩn phân tách mã nguồn & Thiết kế (Tiếng Việt)](FlowMy.Docs/DEV_GUIDELINES_VI.md)

---

## 🚀 Building & Publishing

### Debug Build
```powershell
dotnet build FlowMy.sln
```

### Fast Incremental Build
```powershell
dotnet build FlowMy.sln --no-restore
```

### Release Publish (Self-Contained Windows x64)
```powershell
dotnet publish FlowMy.Wpf-UI\FlowMy.csproj -c Release -r win-x64 --self-contained true
```
