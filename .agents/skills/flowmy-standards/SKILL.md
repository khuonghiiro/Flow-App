---
name: flowmy-standards
description: Enforces file length limits (max 1000 lines), method limits (max 80 lines), modularity grouping, and thread-safety rules when editing or creating code in FlowMy.
---

# FlowMy Coding & Modularity Skill

When modifying, extending, or creating code in the FlowMy workspace, follow this step-by-step skill:

## Step 1: Pre-Edit Inspection
1. Check line count of target file.
2. If file has > 1,000 lines:
   - Plan to split into partial classes `[ClassName].[Feature].cs` or extract dedicated services.
3. If creating a new feature:
   - Identify whether it belongs to `FlowMy.Core` (Models, Interfaces, Helpers) or `FlowMy.Wpf-UI` (Views, ViewModels, Controls, Services).

## Step 2: Implementation Standards
1. **Method Length**: Keep methods under 50–80 lines. Split complex sequences into small private helpers.
2. **Logic Grouping**: Keep cohesive logic together (e.g. `WebNode.Extraction.cs` for scraping, `WebNode.NetworkInterceptor.cs` for CefSharp handlers).
3. **XAML UI & Theming**:
   - Limit `.xaml` files to **800 – 1,000 lines**.
   - Always use `{DynamicResource TokenKey}` from `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`.
   - **Button Padding**: When Button has explicit `Width`/`Height`, ALWAYS set `Padding="0"`.
   - Put new custom styles in `FlowMy.Wpf-UI/Themes/Control_News/`.
4. **Thread Safety**: Never call `Dispatcher.Invoke` from background threads. Use cached view models or `IScopedOutputSync`.

## Step 3: Verification
1. Run incremental build: `dotnet build FlowMy.sln --no-restore`
2. Ensure 0 compilation errors.
