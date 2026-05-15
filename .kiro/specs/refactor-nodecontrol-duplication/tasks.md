# Implementation Plan: Refactor NodeControl Duplication

## Overview

This implementation plan refactors 40+ NodeControl classes to eliminate 60-70% code duplication by extracting common logic into a BaseNodeControlHelper class with a fluent API. The refactoring will reduce typical NodeControl class sizes from 400-500 lines to 120-200 lines while maintaining full backward compatibility.

The implementation follows a phased approach:
1. **Foundation**: Create core helper infrastructure (BaseNodeControlHelper, NodeControlContext, FluentBuilder)
2. **Event Handlers**: Implement all common event handling logic
3. **Utilities**: Add brush resolution, position calculation, and dialog manager utilities
4. **Pilot Migration**: Refactor 3 representative NodeControl classes to validate the approach
5. **Incremental Migration**: Migrate remaining NodeControl classes in batches

## Tasks

- [x] 1. Create BaseNodeControlHelper foundation
  - [x] 1.1 Create BaseNodeControlHelper.cs file with static class structure
    - Create file at `Views/NodeControls/BaseNodeControlHelper.cs`
    - Define static class with ConcurrentDictionary for state tracking
    - Add GetContext and RemoveContext methods
    - _Requirements: 2.1, 2.2, 4.1, 4.2, 4.3_
  
  - [x] 1.2 Implement NodeControlContext class
    - Define NodeControlContext class with Border, TextBlock, WorkflowNode, IWorkflowEditorHost properties
    - Add state tracking properties (IsHovered, IsZooming, TitleUpdatedAfterZoom, TitleUpdateTimer)
    - Implement GetDispatcher, IsBorderVisible, UpdateHoverState methods
    - Implement IDisposable with cleanup logic
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 4.6, 4.7_
  
  - [x] 1.3 Implement FluentBuilder class with initialization
    - Create nested FluentBuilder class inside BaseNodeControlHelper
    - Implement Initialize static method that creates FluentBuilder with NodeControlContext
    - Implement Build method that registers all events and stores context in dictionary
    - Add event registration list to track deferred registrations
    - _Requirements: 1.1, 1.2, 1.11, 4.4, 4.5_

- [x] 2. Implement fluent API configuration methods
  - [x] 2.1 Implement WithTitleManagement configuration method
    - Add WithTitleManagement method to FluentBuilder
    - Register deferred action to call RegisterTitleEvents
    - Return this for method chaining
    - _Requirements: 1.3, 3.1_
  
  - [x] 2.2 Implement WithHoverBehavior configuration method
    - Add WithHoverBehavior method to FluentBuilder
    - Set Border.Focusable and FocusVisualStyle properties
    - Register MouseEnter and MouseLeave event handlers
    - Return this for method chaining
    - _Requirements: 1.4, 3.2_
  
  - [x] 2.3 Implement WithKeyboardPorts configuration method
    - Add WithKeyboardPorts method to FluentBuilder
    - Register PreviewKeyDown event handler
    - Return this for method chaining
    - _Requirements: 1.5_
  
  - [x] 2.4 Implement WithPropertySync configuration method
    - Add WithPropertySync method to FluentBuilder with optional custom handlers parameter
    - Register deferred action to call RegisterPropertyEvents
    - Return this for method chaining
    - _Requirements: 1.6, 3.4_
  
  - [x] 2.5 Implement WithDialogSupport configuration method
    - Add WithDialogSupport method to FluentBuilder with dialogFactory parameter
    - Register MouseRightButtonUp event handler
    - Return this for method chaining
    - _Requirements: 1.8_
  
  - [x] 2.6 Implement WithCleanup configuration method
    - Add WithCleanup method to FluentBuilder
    - Register Unloaded event handler
    - Return this for method chaining
    - _Requirements: 1.9_
  
  - [x] 2.7 Implement WithVisibilitySync configuration method
    - Add WithVisibilitySync method to FluentBuilder
    - Register visibility change handler using DependencyPropertyDescriptor
    - Return this for method chaining
    - _Requirements: 3.5_
  
  - [x] 2.8 Implement WithCanvasIntegration configuration method
    - Add WithCanvasIntegration method to FluentBuilder
    - Register Loaded event handler for canvas integration
    - Return this for method chaining
    - _Requirements: 3.3_

- [ ] 3. Checkpoint - Verify fluent API structure
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement title management event handlers
  - [x] 4.1 Implement CreateTitleTextBlock method
    - Create static method that accepts WorkflowNode, TitleDisplayMode, isHovered
    - Set initial visibility based on TitleDisplayMode and hover state
    - Set Text, FontSize, FontWeight, HorizontalAlignment, VerticalAlignment, TextAlignment, IsHitTestVisible
    - Return configured TextBlock
    - _Requirements: 5.1, 5.5, 5.6, 5.7, 5.8, 5.9_
  
  - [x] 4.2 Implement UpdateTitleVisibility method
    - Create static method that accepts NodeControlContext, TitleDisplayMode, isHovered
    - Implement visibility logic: Hidden → Collapsed, Hover → Collapsed/Visible based on hover, Always → Visible
    - Handle border visibility: if border is Collapsed, title is Collapsed
    - Use Dispatcher for thread safety
    - _Requirements: 5.2, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 19.1, 19.2, 19.3_
  
  - [x] 4.3 Implement UpdateTitlePosition method
    - Create static method that accepts NodeControlContext
    - Call CalculateTitlePosition to get left and top coordinates
    - Set Canvas.Left and Canvas.Top on TitleTextBlock
    - Use Dispatcher for thread safety
    - _Requirements: 5.3, 5.11, 19.1, 19.2, 19.3_
  
  - [x] 4.4 Implement ScheduleThrottledTitleUpdate method
    - Create static method that accepts NodeControlContext and throttleMs parameter
    - Check if timer already exists and is running; if so, restart it
    - Create DispatcherTimer with specified interval
    - In timer tick handler: stop timer, update title position
    - Store timer in NodeControlContext.TitleUpdateTimer
    - _Requirements: 5.4, 5.11_
  
  - [x] 4.5 Implement RegisterTitleEvents method
    - Create static method that accepts NodeControlContext
    - Register Border.Loaded event to call HandleLoaded
    - Register Border.SizeChanged event to call HandleSizeChanged
    - Register Border.LayoutUpdated event to call HandleLayoutUpdated
    - _Requirements: 3.1, 5.11_

- [x] 5. Implement interaction event handlers
  - [x] 5.1 Implement HandleMouseEnter method
    - Create static method that accepts NodeControlContext
    - Update context hover state to true
    - If border is visible, update title visibility based on TitleDisplayMode
    - If border is visible, update title position
    - Set focus to border using Dispatcher with Input priority
    - _Requirements: 6.1, 6.3, 6.4, 6.5, 6.6, 6.9, 19.1, 19.2, 19.3_
  
  - [x] 5.2 Implement HandleMouseLeave method
    - Create static method that accepts NodeControlContext
    - Update context hover state to false
    - If border is visible, update title visibility based on TitleDisplayMode
    - _Requirements: 6.2, 6.7, 6.8, 6.9_
  
  - [x] 5.3 Implement HandleKeyboardPortPositioning method
    - Create static method that accepts NodeControlContext and KeyEventArgs
    - Check if border is hovered; if not, return early
    - Map arrow keys to port positions: Left → Left, Up → Top, Right → Right, Down → Bottom
    - If Shift is not pressed, update node.InputPortPosition
    - If Shift is pressed, update node.OutputPortPosition
    - Update connection paths and animations through IWorkflowEditorHost
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9_
  
  - [x] 5.4 Implement HandleMouseRightButtonUp method
    - Create static method that accepts NodeControlContext, MouseButtonEventArgs, dialogFactory
    - Mark event as handled
    - Release mouse capture if border has capture
    - Set IWorkflowEditorHost.DraggedNode to null
    - Set IWorkflowEditorHost.ViewModel.SelectedNode to null
    - Get or create NodeDialogManager
    - Check if dialog already open for same node; if so, return
    - Check if dialog open for different node; if so, close it
    - Invoke dialogFactory to create dialog and open it
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9_

- [ ] 6. Implement property change and lifecycle handlers
  - [ ] 6.1 Implement HandlePropertyChanged method
    - Create static method that accepts NodeControlContext, propertyName, optional customHandlers
    - Create default handlers dictionary for ColorKey, NodeBrush, Title, TitleDisplayMode, TitleColorMode, TitleColorKey
    - Merge custom handlers with default handlers
    - Look up handler for propertyName; if found, invoke with context
    - Use Dispatcher for thread safety when updating UI
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 8.12, 19.1, 19.2, 19.3_
  
  - [ ] 6.2 Implement RegisterPropertyEvents method
    - Create static method that accepts NodeControlContext and optional customHandlers
    - Check if node implements INotifyPropertyChanged
    - Register PropertyChanged event handler that calls HandlePropertyChanged
    - _Requirements: 3.4, 8.11_
  
  - [ ] 6.3 Implement HandleLayoutUpdated method
    - Create static method that accepts NodeControlContext
    - Check if NodeChrome.IsZooming is true; if so, set title visibility to Collapsed and mark not updated after zoom
    - Check if NodeChrome.IsZooming transitioned from true to false; if so, update title visibility and position, mark updated after zoom
    - Check if IWorkflowEditorHost.IsPanning or DraggedNode equals current node; if so, skip throttled update
    - Otherwise, schedule throttled title update
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9_
  
  - [ ] 6.4 Implement HandleVisibilityChanged method
    - Create static method that accepts NodeControlContext
    - Check border visibility; if not Visible, set title visibility to Collapsed
    - If border visibility is Visible, update title visibility based on TitleDisplayMode and hover state
    - Use Dispatcher for thread safety
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 19.1, 19.2, 19.3_
  
  - [ ] 6.5 Implement HandleLoaded method
    - Create static method that accepts NodeControlContext
    - Check if TitleTextBlock is already in WorkflowCanvas; if not, add it
    - Set Canvas.ZIndex to 20000
    - Update title visibility based on TitleDisplayMode and hover state
    - Update title position
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6_
  
  - [ ] 6.6 Implement HandleSizeChanged method
    - Create static method that accepts NodeControlContext
    - Update title position
    - _Requirements: 14.1, 14.2, 14.3_
  
  - [ ] 6.7 Implement HandleUnloaded method
    - Create static method that accepts NodeControlContext
    - Wrap all cleanup in try-catch to suppress exceptions
    - Stop and dispose TitleUpdateTimer
    - Remove TitleTextBlock from WorkflowCanvas
    - Clear node's TitleTextBlockUI reference if it matches
    - Call context.Dispose to unregister event handlers
    - Remove context from centralized dictionary
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

- [ ] 7. Checkpoint - Verify event handlers
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement utility methods
  - [ ] 8.1 Implement ResolveTextOnColorBrush method
    - Create static method that accepts colorKey string
    - Try to find resource with key "TextOnColor_{colorKey}"
    - If found and is Brush, return it
    - Otherwise return default SolidColorBrush with RGB(148, 163, 184)
    - _Requirements: 15.1, 15.7_
  
  - [ ] 8.2 Implement ResolveTitleBrush method
    - Create static method that accepts TitleColorMode, titleColorKey, nodeBrush
    - If mode is CustomColor and key is "LimeGreen", return SolidColorBrush with Colors.LimeGreen
    - If mode is CustomColor and key is valid resource, return brush from Application.Current.TryFindResource
    - If mode is CustomColor and key is not valid, return nodeBrush as fallback
    - If mode is NodeColor, return nodeBrush
    - _Requirements: 15.2, 15.3, 15.4, 15.5, 15.6_
  
  - [ ] 8.3 Implement CalculateTitlePosition method
    - Create static method that accepts Border, TextBlock, IWorkflowEditorHost
    - Get borderLeft from Canvas.GetLeft; if NaN, use node.X or 0
    - Get borderTop from Canvas.GetTop; if NaN, use node.Y or 0
    - Get borderWidth and borderHeight from Border.ActualWidth/ActualHeight
    - If titleWidth is 0, call Measure and Arrange to force layout
    - Calculate titleLeft as borderLeft + (borderWidth / 2) - (titleWidth / 2)
    - Calculate titleTop as borderTop - titleHeight - 4
    - Return (titleLeft, titleTop)
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.5, 16.6, 16.7, 16.8_
  
  - [ ] 8.4 Implement GetOrCreateDialogManager method
    - Create static method that accepts IWorkflowEditorHost
    - Check if host is WorkflowEditorWindow
    - Use reflection to access private _nodeDialogManager field
    - If field found and contains NodeDialogManager, return it
    - Otherwise create and return new NodeDialogManager instance
    - _Requirements: 17.1, 17.2, 17.3, 17.4_

- [ ] 9. Pilot migration - OutputNodeControl
  - [ ] 9.1 Refactor OutputNodeControl to use BaseNodeControlHelper
    - Keep UI element creation code (grid, icon, title, border)
    - Replace all event handler registrations with fluent API initialization
    - Define custom property handler for ColorKey to update icon fill
    - Define dialog factory lambda for OutputNodeDialog
    - Remove all helper methods (moved to BaseNodeControlHelper)
    - Verify file size reduced from ~500 lines to ~150 lines
    - _Requirements: 1.11, 18.1, 18.2, 18.3, 18.4, 18.5, 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8, 21.1, 22.1, 22.2, 22.3, 22.4_
  
  - [ ] 9.2 Test OutputNodeControl refactored implementation
    - Manually test title visibility in Hidden, Hover, Always modes
    - Test hover behavior (MouseEnter/MouseLeave)
    - Test keyboard port positioning with arrow keys
    - Test right-click dialog opening
    - Test zoom behavior (title hides during zoom)
    - Test cleanup (no memory leaks when node unloaded)
    - Verify visual appearance matches original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 10. Pilot migration - CodeNodeControl
  - [ ] 10.1 Refactor CodeNodeControl to use BaseNodeControlHelper
    - Keep UI element creation code (grid, icon, title, border)
    - Replace all event handler registrations with fluent API initialization
    - Define custom property handler for ColorKey to update icon fill
    - Define dialog factory lambda for CodeNodeDialog
    - Remove all helper methods (moved to BaseNodeControlHelper)
    - Verify file size reduced from ~400 lines to ~120 lines
    - _Requirements: 1.11, 18.1, 18.2, 18.3, 18.4, 18.5, 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8, 21.2, 22.1, 22.2, 22.3, 22.4_
  
  - [ ] 10.2 Test CodeNodeControl refactored implementation
    - Manually test title visibility in Hidden, Hover, Always modes
    - Test hover behavior (MouseEnter/MouseLeave)
    - Test keyboard port positioning with arrow keys
    - Test right-click dialog opening
    - Test zoom behavior (title hides during zoom)
    - Test cleanup (no memory leaks when node unloaded)
    - Verify visual appearance matches original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 11. Pilot migration - ImageProcessingNodeControl
  - [ ] 11.1 Refactor ImageProcessingNodeControl to use BaseNodeControlHelper
    - Keep UI element creation code (grid, icon, title, border, resize handles)
    - Replace all event handler registrations with fluent API initialization
    - Define custom property handler for ColorKey to update icon fill
    - Define dialog factory lambda for ImageProcessingNodeDialog
    - Keep resize handle logic (node-specific)
    - Remove all helper methods (moved to BaseNodeControlHelper)
    - Verify file size reduced from ~450 lines to ~135 lines
    - _Requirements: 1.11, 18.1, 18.2, 18.3, 18.4, 18.5, 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8, 21.3, 22.1, 22.2, 22.3, 22.4_
  
  - [ ] 11.2 Test ImageProcessingNodeControl refactored implementation
    - Manually test title visibility in Hidden, Hover, Always modes
    - Test hover behavior (MouseEnter/MouseLeave)
    - Test keyboard port positioning with arrow keys
    - Test right-click dialog opening
    - Test zoom behavior (title hides during zoom)
    - Test cleanup (no memory leaks when node unloaded)
    - Test resize handles (node-specific feature)
    - Verify visual appearance matches original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 12. Checkpoint - Review pilot migration results
  - Ensure all tests pass, ask the user if questions arise.
  - Gather feedback on fluent API usability
  - Verify code reduction metrics (60-70%)
  - Confirm backward compatibility

- [ ] 13. Incremental migration - Batch 1 (5 NodeControl classes)
  - [ ] 13.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select 5 NodeControl classes from remaining 37+ classes
    - Apply same refactoring pattern as pilot classes
    - Define custom property handlers for node-specific properties
    - Define dialog factory lambdas
    - Remove helper methods
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 13.2 Test Batch 1 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - Check for memory leaks
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 14. Incremental migration - Batch 2 (5 NodeControl classes)
  - [ ] 14.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 14.2 Test Batch 2 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 15. Incremental migration - Batch 3 (5 NodeControl classes)
  - [ ] 15.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 15.2 Test Batch 3 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 16. Incremental migration - Batch 4 (5 NodeControl classes)
  - [ ] 16.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 16.2 Test Batch 4 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 17. Incremental migration - Batch 5 (5 NodeControl classes)
  - [ ] 17.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 17.2 Test Batch 5 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 18. Incremental migration - Batch 6 (5 NodeControl classes)
  - [ ] 18.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 18.2 Test Batch 6 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 19. Incremental migration - Batch 7 (5 NodeControl classes)
  - [ ] 19.1 Migrate 5 NodeControl classes to use BaseNodeControlHelper
    - Select next 5 NodeControl classes
    - Apply same refactoring pattern
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 19.2 Test Batch 7 NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 20. Incremental migration - Remaining NodeControl classes
  - [ ] 20.1 Migrate all remaining NodeControl classes to use BaseNodeControlHelper
    - Identify remaining unmigrated NodeControl classes (approximately 7+ classes)
    - Apply same refactoring pattern to each
    - _Requirements: 21.4, 21.5, 21.6, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23.1, 23.2, 23.3_
  
  - [ ] 20.2 Test remaining NodeControl classes
    - Manually test each migrated class
    - Verify visual appearance and behavior match original
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_

- [ ] 21. Final checkpoint and documentation
  - Ensure all tests pass, ask the user if questions arise.
  - Verify all 40+ NodeControl classes have been migrated
  - Confirm code reduction metrics achieved (60-70%)
  - Document migration pattern for future NodeControl classes

## Notes

- Tasks marked with `*` are optional testing tasks and can be skipped for faster implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation and allow for user feedback
- The migration is designed to be incremental, allowing rollback of individual classes if issues arise
- Custom property handlers and dialog factories are the primary node-specific customization points
- The fluent API reduces typical NodeControl setup from 400+ lines to a single initialization chain
- Thread safety is handled automatically by BaseNodeControlHelper using Dispatcher marshaling
- All event handler cleanup is managed by NodeControlContext.Dispose to prevent memory leaks

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8"] },
    { "id": 3, "tasks": ["4.1", "4.5"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "5.1", "5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5", "6.6", "6.7"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 7, "tasks": ["9.1"] },
    { "id": 8, "tasks": ["9.2", "10.1"] },
    { "id": 9, "tasks": ["10.2", "11.1"] },
    { "id": 10, "tasks": ["11.2"] },
    { "id": 11, "tasks": ["13.1"] },
    { "id": 12, "tasks": ["13.2", "14.1"] },
    { "id": 13, "tasks": ["14.2", "15.1"] },
    { "id": 14, "tasks": ["15.2", "16.1"] },
    { "id": 15, "tasks": ["16.2", "17.1"] },
    { "id": 16, "tasks": ["17.2", "18.1"] },
    { "id": 17, "tasks": ["18.2", "19.1"] },
    { "id": 18, "tasks": ["19.2", "20.1"] },
    { "id": 19, "tasks": ["20.2"] }
  ]
}
```
