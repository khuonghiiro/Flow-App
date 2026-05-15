# NodeControl Migration Guide

## Overview

This guide documents the pattern for creating and migrating NodeControl classes using `BaseNodeControlHelper`. The refactoring eliminated 60-70% of duplicated code across 38 NodeControl classes by centralizing common event handling, title management, hover behavior, zoom handling, and cleanup logic into a single helper class.

## What Was Achieved

| Metric | Result |
|--------|--------|
| NodeControl classes migrated | 38 of 38 eligible classes |
| Typical file size before | 400–500 lines |
| Typical file size after | 100–200 lines |
| Code reduction | ~60–75% |
| Build errors after migration | 0 |
| `BaseNodeControlHelper.cs` size | 1,515 lines (all shared logic in one place) |

### Representative File Size Reductions

| File | After (lines) | Estimated Before | Reduction |
|------|--------------|-----------------|-----------|
| `OutputNodeControl.cs` | 122 | ~500 | ~76% |
| `CodeNodeControl.cs` | 122 | ~400 | ~70% |
| `AssignDataNodeControl.cs` | 116 | ~400 | ~71% |
| `BreakNodeControl.cs` | 101 | ~400 | ~75% |
| `InputNodeControl.cs` | 103 | ~400 | ~74% |
| `StorageNodeControl.cs` | 116 | ~400 | ~71% |

---

## Pattern for Creating a New NodeControl Class

A new NodeControl class follows this structure:

```csharp
using FlowMy.Views.NodeControls.Helpers;
// ... other usings

namespace FlowMy.Views.NodeControls
{
    public static class MyNewNodeControl
    {
        public static Border CreateBorder(MyNewNode node, Window? ownerWindow, IWorkflowEditorHost? host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // 1. Create the icon (node-specific)
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri),
                "your-icon-key duotone-regular",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey)
            };

            // 2. Create the grid and add the icon
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };
            grid.Children.Add(iconSvg);

            // 3. Create the title TextBlock
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "My Node",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode,
                    node.TitleColorKey,
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            // 4. Create the Border
            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            // 5. Define node-specific property handlers (only what's unique to this node)
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                }
                // Add other node-specific property handlers here if needed
            };

            // 6. Initialize with fluent API — this replaces ~300 lines of duplicated event handler code
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithZoomHandling()
                .WithDialogSupport(ctx => new MyNewNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }
    }
}
```

### Fluent API Methods Reference

| Method | Purpose |
|--------|---------|
| `Initialize(border, title, node, host)` | Creates the builder and `NodeControlContext` |
| `.WithTitleManagement()` | Registers Loaded, SizeChanged, LayoutUpdated for title positioning |
| `.WithHoverBehavior()` | Registers MouseEnter/MouseLeave for hover effects and focus |
| `.WithKeyboardPorts()` | Registers PreviewKeyDown for arrow-key port positioning |
| `.WithPropertySync(handlers)` | Registers PropertyChanged with thread-safe UI updates |
| `.WithZoomHandling()` | Hides title during zoom, restores after zoom completes |
| `.WithDialogSupport(factory)` | Registers MouseRightButtonUp to open the node dialog |
| `.WithCleanup()` | Registers Unloaded to stop timers, remove title, dispose context |
| `.WithVisibilitySync()` | Syncs title visibility with border visibility (viewport culling) |
| `.WithCanvasIntegration()` | Adds title TextBlock to WorkflowCanvas on Loaded |
| `.Build()` | Applies all registrations and stores context in the dictionary |

All methods return `this` for chaining. `Build()` returns the `NodeControlContext`.

### Utility Methods

```csharp
// Resolve icon fill color from resource dictionary
Brush fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);

// Resolve title foreground brush based on TitleColorMode
Brush titleBrush = BaseNodeControlHelper.ResolveTitleBrush(
    node.TitleColorMode, node.TitleColorKey, node.NodeBrush);

// Get the context for a border (useful for advanced scenarios)
var ctx = BaseNodeControlHelper.GetContext(border);
```

---

## Files Intentionally NOT Migrated

The following 4 files are **container or content controls** with a fundamentally different pattern. They do not follow the standard NodeControl pattern (static class with `CreateBorder`) and therefore do not use `BaseNodeControlHelper`.

| File | Reason |
|------|--------|
| `BodyContainerControl.cs` | Container control for loop/conditional body regions; manages child layout, not a standard node border |
| `LoopContainerControl.cs` | Container control for loop body; manages child node layout and resize handles |
| `ImageProcessingNodeContentControl.xaml.cs` | XAML UserControl (code-behind) for the image processing node's content panel |
| `VideoProcessingNodeContentControl.cs` | Content control for the video processing node's embedded content panel |

These files should remain as-is. Do not attempt to migrate them to `BaseNodeControlHelper`.

---

## Node-Specific Customization Points

The only things that belong in individual NodeControl classes are:

1. **Icon key** — the specific icon for this node type
2. **Default title text** — the fallback title string
3. **Custom property handlers** — handlers for properties unique to this node (e.g., `Width`, `Height`, node-specific brush updates)
4. **Dialog factory** — the lambda that creates the node's configuration dialog
5. **Node-specific UI elements** — resize handles, embedded controls, extra buttons, etc.

Everything else (hover, keyboard, zoom, cleanup, canvas integration, visibility sync, property sync for standard properties) is handled automatically by `BaseNodeControlHelper`.

---

## Special Cases

### Nodes with No Title (dummy title)

Some nodes (e.g., `ScreenCaptureNodeControl`, `ScreenPositionPickerNodeControl`) use a dummy invisible title because they manage their own title display:

```csharp
var dummyTitle = new TextBlock { Visibility = Visibility.Collapsed, IsHitTestVisible = false };

BaseNodeControlHelper
    .Initialize(border, dummyTitle, node, host)
    .WithHoverBehavior()
    .WithKeyboardPorts()
    .WithPropertySync(customPropertyHandlers)
    .WithDialogSupport(ctx => new MyDialog(node, host, ownerWindow))
    .WithCleanup()
    .Build();
// Note: WithTitleManagement(), WithVisibilitySync(), WithCanvasIntegration() are omitted
```

### Nodes with Extra Cleanup

Some nodes (e.g., `WebNodeControl` with WebView2) need additional cleanup beyond what `BaseNodeControlHelper` provides. Add a separate `Unloaded` handler after the fluent API call:

```csharp
BaseNodeControlHelper
    .Initialize(border, titleTextBlock, node, host)
    // ... fluent chain ...
    .WithCleanup()  // BaseNodeControlHelper cleanup runs first
    .Build();

// Additional node-specific cleanup
border.Unloaded += (s, e) =>
{
    // Dispose WebView2 or other node-specific resources
    webView?.Dispose();
};
```

### Nodes with Resizable Borders

Nodes like `ImageProcessingNodeControl` and `VideoProcessingNodeControl` have resizable borders. Keep the resize handle logic in the NodeControl class — it is node-specific and does not belong in `BaseNodeControlHelper`.

---

## Architecture Summary

```
Views/NodeControls/
├── Helpers/
│   ├── BaseNodeControlHelper.cs    ← All shared logic (1,515 lines)
│   └── MIGRATION_GUIDE.md          ← This file
├── OutputNodeControl.cs            ← ~122 lines (node-specific only)
├── CodeNodeControl.cs              ← ~122 lines (node-specific only)
├── ... (36 more migrated files)
├── BodyContainerControl.cs         ← NOT migrated (container control)
├── LoopContainerControl.cs         ← NOT migrated (container control)
├── ImageProcessingNodeContentControl.xaml.cs  ← NOT migrated (XAML code-behind)
└── VideoProcessingNodeContentControl.cs       ← NOT migrated (content control)
```

`BaseNodeControlHelper` uses a `ConcurrentDictionary<Border, NodeControlContext>` to track all active node contexts, enabling safe access to any node's state from anywhere in the codebase via `BaseNodeControlHelper.GetContext(border)`.
