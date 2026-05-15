# Requirements Document

## Introduction

This document specifies requirements for refactoring NodeControl classes to eliminate code duplication by extracting common logic into a BaseNodeControlHelper class. The codebase currently contains 40+ NodeControl classes with significant duplicated patterns for title handling, hover logic, keyboard port positioning, property change handlers, dialog management, title visibility/positioning, throttling, zoom handling, and cleanup logic. Each NodeControl file is approximately 400-500 lines, with 60-70% of the code being duplicated across files. 

This refactoring aims to:
- **Reduce code duplication by 60-70%** through systematic extraction of common patterns
- **Improve code organization** with a clean, fluent API design
- **Enhance maintainability** by centralizing shared logic in one place
- **Ensure consistency** across all node types through shared implementations
- **Simplify NodeControl classes** to focus only on node-specific customization

## Glossary

- **NodeControl**: A static class responsible for creating and managing the UI Border element for a specific node type (e.g., OutputNodeControl, CodeNodeControl)
- **BaseNodeControlHelper**: The new helper class that will contain extracted common logic shared across all NodeControl classes
- **NodeControlContext**: A context object that encapsulates all state needed for node control operations (Border, TextBlock, WorkflowNode, IWorkflowEditorHost, hover state)
- **TitleTextBlock**: A WPF TextBlock UI element that displays the node's title above the node border
- **TitleDisplayMode**: An enumeration controlling title visibility (Hidden, Hover, Always)
- **TitleColorMode**: An enumeration controlling title color source (NodeColor, CustomColor)
- **Border**: The WPF Border UI element representing the visual node container
- **IWorkflowEditorHost**: Interface providing access to the workflow canvas, view model, and rendering services
- **WorkflowCanvas**: The WPF Canvas element containing all node borders and title text blocks
- **DispatcherTimer**: WPF timer used for throttling UI updates
- **PropertyChanged_Handler**: Event handler responding to INotifyPropertyChanged events from node models
- **Viewport_Culling**: The process of hiding UI elements when nodes are outside the visible canvas area
- **Zoom_Handling**: Logic that hides titles during zoom operations and restores them after zoom completes
- **Port_Position**: The location of a connection port on a node (Left, Top, Right, Bottom)
- **Dialog_Manager**: Service managing node configuration dialogs
- **Throttling**: Technique to limit the frequency of UI update operations
- **Thread_Safety**: Ensuring UI operations execute on the correct WPF Dispatcher thread
- **Cleanup_Logic**: Code that removes event handlers and UI elements when a node is unloaded
- **Fluent_API**: A method chaining pattern that allows configuring multiple behaviors in a single expression
- **Event_Lifecycle**: The complete lifecycle of event handler registration, execution, and cleanup

## Requirements

### Requirement 1: Fluent API Design for Helper Class

**User Story:** As a developer, I want a fluent API for configuring node controls, so that I can set up all behaviors in a clean, readable chain of method calls.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a static Initialize method that returns a builder or context object
2. THE builder object SHALL support method chaining for all configuration operations
3. THE builder object SHALL provide a WithTitleManagement method that configures title handling
4. THE builder object SHALL provide a WithHoverBehavior method that configures hover events
5. THE builder object SHALL provide a WithKeyboardPorts method that configures keyboard port positioning
6. THE builder object SHALL provide a WithPropertySync method that configures property change handling
7. THE builder object SHALL provide a WithZoomHandling method that configures zoom behavior
8. THE builder object SHALL provide a WithDialogSupport method that configures dialog management
9. THE builder object SHALL provide a WithCleanup method that configures cleanup logic
10. THE builder object SHALL provide a Build or Attach method that applies all configurations to the Border
11. WHEN a NodeControl uses the fluent API, THE code SHALL be reduced to 5-10 lines for complete setup
12. THE fluent API SHALL accept lambda expressions for node-specific customization points

### Requirement 2: Context Object for State Management

**User Story:** As a developer, I want a context object that encapsulates all node control state, so that I don't pass multiple parameters to every helper method.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL define a NodeControlContext class that encapsulates Border, TextBlock, WorkflowNode, IWorkflowEditorHost, and hover state
2. THE NodeControlContext SHALL provide properties for accessing all encapsulated objects
3. THE NodeControlContext SHALL provide a method to update hover state
4. THE NodeControlContext SHALL provide a method to check if the Border is visible
5. THE NodeControlContext SHALL provide a method to get the Dispatcher from the Border or Application.Current
6. THE helper methods SHALL accept NodeControlContext as the primary parameter instead of individual objects
7. THE NodeControlContext SHALL be created once during initialization and reused throughout the node's lifetime
8. THE NodeControlContext SHALL implement IDisposable to support cleanup of resources

### Requirement 3: Grouped Event Handler Registration

**User Story:** As a developer, I want event handlers registered in logical groups, so that related behaviors are configured together and code is more organized.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a RegisterTitleEvents method that registers all title-related events (Loaded, SizeChanged, LayoutUpdated)
2. THE BaseNodeControlHelper SHALL provide a RegisterInteractionEvents method that registers all interaction events (MouseEnter, MouseLeave, MouseRightButtonUp, PreviewKeyDown)
3. THE BaseNodeControlHelper SHALL provide a RegisterLifecycleEvents method that registers lifecycle events (Loaded, Unloaded)
4. THE BaseNodeControlHelper SHALL provide a RegisterPropertyEvents method that registers PropertyChanged handlers
5. THE BaseNodeControlHelper SHALL provide a RegisterVisibilityEvents method that registers visibility change handlers
6. WHEN a grouped registration method is called, THE BaseNodeControlHelper SHALL register all related event handlers in a single operation
7. WHEN a grouped registration method is called, THE BaseNodeControlHelper SHALL store event handler references for later cleanup
8. THE grouped registration methods SHALL accept the NodeControlContext as parameter

### Requirement 4: Centralized State Tracking

**User Story:** As a developer, I want all node control state tracked in one place, so that I can easily debug and understand the current state of any node.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL maintain a static ConcurrentDictionary mapping Border instances to NodeControlContext instances
2. THE BaseNodeControlHelper SHALL provide a GetContext method to retrieve the context for a given Border
3. THE BaseNodeControlHelper SHALL provide a RemoveContext method to clean up context when a Border is unloaded
4. WHEN a Border is initialized, THE BaseNodeControlHelper SHALL create and store a NodeControlContext in the dictionary
5. WHEN a Border is unloaded, THE BaseNodeControlHelper SHALL remove the NodeControlContext from the dictionary
6. THE NodeControlContext SHALL track hover state, zoom update state, and throttling timer state
7. THE NodeControlContext SHALL provide methods to query current state (IsHovered, IsZooming, IsThrottling)
8. THE centralized state tracking SHALL eliminate the need for multiple separate dictionaries

### Requirement 5: Extract Common Title Management Logic

**User Story:** As a developer, I want title management logic extracted to a helper class, so that title handling is consistent across all node types and bugs are fixed in one place.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a CreateTitleTextBlock method that creates a TitleTextBlock with initial visibility based on TitleDisplayMode
2. THE BaseNodeControlHelper SHALL provide an UpdateTitleVisibility method that updates TitleTextBlock visibility based on TitleDisplayMode, hover state, and Border visibility
3. THE BaseNodeControlHelper SHALL provide an UpdateTitlePosition method that calculates and sets TitleTextBlock position above the Border center
4. THE BaseNodeControlHelper SHALL provide a ScheduleThrottledTitleUpdate method that implements throttled title position updates
5. WHEN a TitleTextBlock is created, THE BaseNodeControlHelper SHALL ensure the Foreground brush is determined by TitleColorMode and TitleColorKey
6. WHEN TitleDisplayMode is Hidden, THE TitleTextBlock SHALL have Visibility set to Collapsed
7. WHEN TitleDisplayMode is Hover AND the Border is not being hovered, THE TitleTextBlock SHALL have Visibility set to Collapsed
8. WHEN TitleDisplayMode is Hover AND the Border is being hovered, THE TitleTextBlock SHALL have Visibility set to Visible
9. WHEN TitleDisplayMode is Always, THE TitleTextBlock SHALL have Visibility set to Visible
10. WHEN the Border visibility is Collapsed, THE TitleTextBlock SHALL have Visibility set to Collapsed regardless of TitleDisplayMode
11. THE title management methods SHALL use NodeControlContext to access all required state

### Requirement 6: Extract Common Hover Event Handling

**User Story:** As a developer, I want hover event handling extracted to a helper class, so that hover behavior is consistent and I don't duplicate MouseEnter/MouseLeave logic.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleMouseEnter method that processes MouseEnter events
2. THE BaseNodeControlHelper SHALL provide a HandleMouseLeave method that processes MouseLeave events
3. WHEN HandleMouseEnter is called, THE BaseNodeControlHelper SHALL update the context hover state to true
4. WHEN HandleMouseEnter is called AND the Border is visible, THE BaseNodeControlHelper SHALL update TitleTextBlock visibility based on TitleDisplayMode
5. WHEN HandleMouseEnter is called AND the Border is visible, THE BaseNodeControlHelper SHALL update TitleTextBlock position
6. WHEN HandleMouseEnter is called, THE BaseNodeControlHelper SHALL set focus to the Border using Dispatcher with Input priority
7. WHEN HandleMouseLeave is called, THE BaseNodeControlHelper SHALL update the context hover state to false
8. WHEN HandleMouseLeave is called AND the Border is visible, THE BaseNodeControlHelper SHALL update TitleTextBlock visibility based on TitleDisplayMode
9. THE hover event handlers SHALL use NodeControlContext to access all required state

### Requirement 7: Extract Common Keyboard Port Positioning Logic

**User Story:** As a developer, I want keyboard port positioning logic extracted to a helper class, so that arrow key port positioning works consistently across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleKeyboardPortPositioning method that processes PreviewKeyDown events
2. WHEN an arrow key is pressed without Shift modifier AND the Border is hovered, THE BaseNodeControlHelper SHALL change the input port position to the corresponding direction
3. WHEN an arrow key is pressed with Shift modifier AND the Border is hovered, THE BaseNodeControlHelper SHALL change the output port position to the corresponding direction
4. WHEN Left arrow is pressed, THE BaseNodeControlHelper SHALL set the port position to Left
5. WHEN Up arrow is pressed, THE BaseNodeControlHelper SHALL set the port position to Top
6. WHEN Right arrow is pressed, THE BaseNodeControlHelper SHALL set the port position to Right
7. WHEN Down arrow is pressed, THE BaseNodeControlHelper SHALL set the port position to Bottom
8. WHEN a port position is changed, THE BaseNodeControlHelper SHALL update connection paths and animations through IWorkflowEditorHost
9. THE keyboard port positioning logic SHALL use NodeControlContext to access all required state

### Requirement 8: Extract Common Property Change Handling

**User Story:** As a developer, I want property change handling extracted to a helper class, so that UI updates in response to model changes are consistent and thread-safe.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandlePropertyChanged method that processes PropertyChanged events
2. THE BaseNodeControlHelper SHALL provide a RegisterPropertyHandler method that maps property names to update actions
3. WHEN the ColorKey property changes, THE BaseNodeControlHelper SHALL invoke the registered icon color update action
4. WHEN the NodeBrush property changes, THE BaseNodeControlHelper SHALL invoke the registered border and title brush update action
5. WHEN the TitleColorMode property changes, THE BaseNodeControlHelper SHALL invoke the registered title brush update action
6. WHEN the TitleColorKey property changes, THE BaseNodeControlHelper SHALL invoke the registered title brush update action
7. WHEN the Title property changes, THE BaseNodeControlHelper SHALL update the TitleTextBlock Text property
8. WHEN the Title property changes AND the Border is visible, THE BaseNodeControlHelper SHALL update the TitleTextBlock position
9. WHEN the TitleDisplayMode property changes AND the Border is visible, THE BaseNodeControlHelper SHALL update the TitleTextBlock visibility
10. WHEN a PropertyChanged event is raised from a non-UI thread, THE BaseNodeControlHelper SHALL use Dispatcher.BeginInvoke to execute UI updates on the UI thread
11. THE property change handlers SHALL use NodeControlContext to access all required state
12. THE BaseNodeControlHelper SHALL support registering custom property handlers for node-specific properties

### Requirement 9: Extract Common Zoom Handling Logic

**User Story:** As a developer, I want zoom handling logic extracted to a helper class, so that titles hide during zoom and restore consistently across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleLayoutUpdated method that processes LayoutUpdated events with zoom handling
2. WHEN NodeChrome.IsZooming is true, THE BaseNodeControlHelper SHALL set TitleTextBlock Visibility to Collapsed
3. WHEN NodeChrome.IsZooming is true, THE BaseNodeControlHelper SHALL mark the Border as not updated after zoom in the context
4. WHEN NodeChrome.IsZooming transitions from true to false AND the Border is visible, THE BaseNodeControlHelper SHALL update TitleTextBlock visibility based on TitleDisplayMode
5. WHEN NodeChrome.IsZooming transitions from true to false AND the Border is visible AND TitleTextBlock is visible, THE BaseNodeControlHelper SHALL update TitleTextBlock position
6. WHEN NodeChrome.IsZooming transitions from true to false, THE BaseNodeControlHelper SHALL mark the Border as updated after zoom in the context
7. WHEN IWorkflowEditorHost.IsPanning is true, THE BaseNodeControlHelper SHALL skip throttled title position updates
8. WHEN IWorkflowEditorHost.DraggedNode equals the current node, THE BaseNodeControlHelper SHALL skip throttled title position updates
9. THE zoom handling logic SHALL use NodeControlContext to access all required state

### Requirement 10: Extract Common Cleanup Logic

**User Story:** As a developer, I want cleanup logic extracted to a helper class, so that memory leaks are prevented consistently when nodes are unloaded.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleUnloaded method that processes Unloaded events
2. WHEN a Border is unloaded, THE BaseNodeControlHelper SHALL dispose the NodeControlContext which stops and removes the associated DispatcherTimer
3. WHEN a Border is unloaded, THE BaseNodeControlHelper SHALL remove the Border from the centralized context dictionary
4. WHEN a Border is unloaded, THE BaseNodeControlHelper SHALL remove the TitleTextBlock from the WorkflowCanvas
5. WHEN a Border is unloaded AND the node's TitleTextBlockUI reference equals the TitleTextBlock, THE BaseNodeControlHelper SHALL set the node's TitleTextBlockUI reference to null
6. WHEN an exception occurs during cleanup, THE BaseNodeControlHelper SHALL suppress the exception to avoid crashing the unload path
7. THE NodeControlContext.Dispose method SHALL unregister all event handlers that were registered during initialization

### Requirement 11: Extract Common Dialog Management Logic

**User Story:** As a developer, I want dialog management logic extracted to a helper class, so that right-click dialog opening is consistent across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleMouseRightButtonUp method that processes MouseRightButtonUp events
2. WHEN HandleMouseRightButtonUp is called, THE BaseNodeControlHelper SHALL mark the event as handled
3. WHEN HandleMouseRightButtonUp is called, THE BaseNodeControlHelper SHALL invoke a node-specific dialog factory delegate provided by the caller
4. WHEN a dialog is opened AND the Border has mouse capture, THE BaseNodeControlHelper SHALL release mouse capture
5. WHEN a dialog is opened, THE BaseNodeControlHelper SHALL set IWorkflowEditorHost.DraggedNode to null
6. WHEN a dialog is opened, THE BaseNodeControlHelper SHALL set IWorkflowEditorHost.ViewModel.SelectedNode to null
7. WHEN a dialog is opened AND a dialog is already open for the same node, THE BaseNodeControlHelper SHALL not open a new dialog
8. WHEN a dialog is opened AND a dialog is already open for a different node, THE BaseNodeControlHelper SHALL close the existing dialog before opening the new dialog
9. THE dialog management logic SHALL use NodeControlContext to access all required state

### Requirement 12: Extract Common Visibility Synchronization Logic

**User Story:** As a developer, I want visibility synchronization logic extracted to a helper class, so that title visibility tracks border visibility consistently for viewport culling.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleVisibilityChanged method that processes visibility change events
2. THE BaseNodeControlHelper SHALL attach visibility change handlers to a Border using DependencyPropertyDescriptor
3. WHEN the Border Visibility property changes to a value other than Visible, THE BaseNodeControlHelper SHALL set TitleTextBlock Visibility to Collapsed
4. WHEN the Border Visibility property changes to Visible, THE BaseNodeControlHelper SHALL update TitleTextBlock visibility based on TitleDisplayMode and hover state
5. WHEN a visibility change handler is invoked from a non-UI thread, THE BaseNodeControlHelper SHALL use Dispatcher.BeginInvoke to execute UI updates on the UI thread
6. THE visibility synchronization logic SHALL use NodeControlContext to access all required state

### Requirement 13: Extract Common Canvas Integration Logic

**User Story:** As a developer, I want canvas integration logic extracted to a helper class, so that title text blocks are added to the canvas consistently when borders are loaded.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleLoaded method that processes Loaded events
2. WHEN a Border is loaded AND the TitleTextBlock is not already in the WorkflowCanvas, THE BaseNodeControlHelper SHALL add the TitleTextBlock to the WorkflowCanvas
3. WHEN a TitleTextBlock is added to the WorkflowCanvas, THE BaseNodeControlHelper SHALL set the ZIndex to 20000
4. WHEN a TitleTextBlock is added to the WorkflowCanvas, THE BaseNodeControlHelper SHALL update TitleTextBlock visibility based on TitleDisplayMode and hover state
5. WHEN a TitleTextBlock is added to the WorkflowCanvas, THE BaseNodeControlHelper SHALL update TitleTextBlock position
6. THE canvas integration logic SHALL use NodeControlContext to access all required state

### Requirement 14: Extract Common Size Change Handling

**User Story:** As a developer, I want size change handling extracted to a helper class, so that title position updates when node size changes consistently.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a HandleSizeChanged method that processes SizeChanged events
2. WHEN a Border SizeChanged event is triggered, THE BaseNodeControlHelper SHALL update the TitleTextBlock position
3. THE size change handling logic SHALL use NodeControlContext to access all required state

### Requirement 15: Extract Common Brush Resolution Logic

**User Story:** As a developer, I want brush resolution logic extracted to a helper class, so that color lookups are consistent across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a ResolveTextOnColorBrush method to resolve TextOnColorBrush resources by color key
2. THE BaseNodeControlHelper SHALL provide a ResolveTitleBrush method to resolve title brushes based on TitleColorMode, TitleColorKey, and NodeBrush
3. WHEN TitleColorMode is CustomColor AND TitleColorKey is "LimeGreen", THE BaseNodeControlHelper SHALL return a SolidColorBrush with Colors.LimeGreen
4. WHEN TitleColorMode is CustomColor AND TitleColorKey is a valid resource key, THE BaseNodeControlHelper SHALL return the brush from Application.Current.TryFindResource
5. WHEN TitleColorMode is CustomColor AND TitleColorKey is not a valid resource key, THE BaseNodeControlHelper SHALL return the NodeBrush as fallback
6. WHEN TitleColorMode is NodeColor, THE BaseNodeControlHelper SHALL return the NodeBrush
7. WHEN a color resource key is not found, THE BaseNodeControlHelper SHALL return a default SolidColorBrush with RGB(148, 163, 184)

### Requirement 16: Extract Common Position Calculation Logic

**User Story:** As a developer, I want position calculation logic extracted to a helper class, so that title positioning math is consistent and correct across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a CalculateTitlePosition method that calculates both Canvas.Left and Canvas.Top for the TitleTextBlock
2. WHEN Canvas.GetLeft returns NaN AND the Border Tag is a WorkflowNode, THE BaseNodeControlHelper SHALL use the WorkflowNode.X property as the left position
3. WHEN Canvas.GetTop returns NaN AND the Border Tag is a WorkflowNode, THE BaseNodeControlHelper SHALL use the WorkflowNode.Y property as the top position
4. WHEN Canvas.GetLeft returns NaN AND the Border Tag is not a WorkflowNode, THE BaseNodeControlHelper SHALL use 0 as the left position
5. WHEN Canvas.GetTop returns NaN AND the Border Tag is not a WorkflowNode, THE BaseNodeControlHelper SHALL use 0 as the top position
6. WHEN TitleTextBlock.ActualWidth is 0, THE BaseNodeControlHelper SHALL call Measure and Arrange to force layout calculation before positioning
7. THE BaseNodeControlHelper SHALL calculate titleLeft as borderLeft plus half borderWidth minus half titleWidth
8. THE BaseNodeControlHelper SHALL calculate titleTop as borderTop minus titleHeight minus 4 pixels

### Requirement 17: Extract Common Dialog Manager Resolution Logic

**User Story:** As a developer, I want dialog manager resolution logic extracted to a helper class, so that dialog manager access is consistent across all node types.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL provide a GetOrCreateDialogManager method to retrieve or create a NodeDialogManager from an IWorkflowEditorHost
2. WHEN the IWorkflowEditorHost is a WorkflowEditorWindow, THE BaseNodeControlHelper SHALL use reflection to access the private _nodeDialogManager field
3. WHEN the _nodeDialogManager field is found AND contains a NodeDialogManager instance, THE BaseNodeControlHelper SHALL return that instance
4. WHEN the IWorkflowEditorHost is not a WorkflowEditorWindow OR the _nodeDialogManager field is not found, THE BaseNodeControlHelper SHALL create and return a new NodeDialogManager instance

### Requirement 18: Preserve Node-Specific Customization

**User Story:** As a developer, I want node-specific logic to remain in individual NodeControl classes, so that each node type can customize its icon, default title, and title brush logic.

#### Acceptance Criteria

1. THE NodeControl classes SHALL retain responsibility for determining the icon key for their node type
2. THE NodeControl classes SHALL retain responsibility for determining the default title text for their node type
3. THE NodeControl classes SHALL retain responsibility for implementing node-specific title brush logic based on TitleColorMode and TitleColorKey
4. THE NodeControl classes SHALL retain responsibility for creating the dialog instance for their node type
5. THE BaseNodeControlHelper SHALL accept delegates or callbacks for node-specific customization points through the fluent API

### Requirement 19: Maintain Thread Safety

**User Story:** As a developer, I want all UI operations to be thread-safe, so that the application does not crash when property changes occur on background threads.

#### Acceptance Criteria

1. WHEN a UI update method is called from a non-UI thread, THE BaseNodeControlHelper SHALL use Dispatcher.CheckAccess to detect the thread context
2. WHEN Dispatcher.CheckAccess returns false, THE BaseNodeControlHelper SHALL use Dispatcher.BeginInvoke with Normal priority to marshal the operation to the UI thread
3. WHEN Dispatcher.CheckAccess returns true, THE BaseNodeControlHelper SHALL execute the UI update directly without marshaling
4. WHEN a Dispatcher is not available from the Border or Application.Current, THE BaseNodeControlHelper SHALL return early without performing the UI update
5. THE NodeControlContext.GetDispatcher method SHALL encapsulate dispatcher resolution logic

### Requirement 20: Maintain Backward Compatibility

**User Story:** As a developer, I want the refactoring to preserve all existing functionality, so that no breaking changes are introduced to node behavior.

#### Acceptance Criteria

1. THE refactored NodeControl classes SHALL produce Border elements with identical visual appearance to the original implementation
2. THE refactored NodeControl classes SHALL produce Border elements with identical event handling behavior to the original implementation
3. THE refactored NodeControl classes SHALL produce Border elements with identical title positioning behavior to the original implementation
4. THE refactored NodeControl classes SHALL produce Border elements with identical title visibility behavior to the original implementation
5. THE refactored NodeControl classes SHALL produce Border elements with identical zoom handling behavior to the original implementation
6. THE refactored NodeControl classes SHALL produce Border elements with identical cleanup behavior to the original implementation
7. THE refactored NodeControl classes SHALL produce Border elements with identical dialog opening behavior to the original implementation
8. THE refactored NodeControl classes SHALL produce Border elements with identical port positioning behavior to the original implementation

### Requirement 21: Reduce Code Duplication

**User Story:** As a developer, I want NodeControl class file sizes reduced by 60-70%, so that the codebase is easier to maintain and understand.

#### Acceptance Criteria

1. WHEN the refactoring is complete, THE OutputNodeControl class SHALL be reduced from approximately 500 lines to approximately 150-200 lines
2. WHEN the refactoring is complete, THE CodeNodeControl class SHALL be reduced from approximately 400 lines to approximately 120-160 lines
3. WHEN the refactoring is complete, THE ImageProcessingNodeControl class SHALL be reduced from approximately 450 lines to approximately 135-180 lines
4. WHEN the refactoring is complete, THE BaseNodeControlHelper class SHALL contain all extracted common logic
5. WHEN a bug is fixed in title handling, THE fix SHALL automatically apply to all NodeControl classes that use BaseNodeControlHelper
6. WHEN a new NodeControl class is created, THE developer SHALL be able to implement it in 100-150 lines using the fluent API

### Requirement 22: Simplify NodeControl Implementation Pattern

**User Story:** As a developer, I want a clear, simple pattern for implementing NodeControl classes, so that new node types can be added quickly with minimal boilerplate.

#### Acceptance Criteria

1. THE BaseNodeControlHelper fluent API SHALL reduce NodeControl setup code to a single initialization chain
2. THE NodeControl classes SHALL follow a consistent pattern: create Border, create icon, create title, initialize helper with fluent API
3. THE fluent API initialization SHALL be readable and self-documenting without requiring extensive comments
4. WHEN a developer creates a new NodeControl class, THE implementation SHALL require only node-specific customization (icon, dialog, custom properties)
5. THE BaseNodeControlHelper SHALL provide clear examples or documentation for the recommended implementation pattern
6. THE fluent API SHALL support optional configuration methods, allowing developers to omit features not needed for specific node types

### Requirement 23: Support Incremental Migration

**User Story:** As a developer, I want to migrate NodeControl classes incrementally, so that I can refactor one class at a time without breaking the entire codebase.

#### Acceptance Criteria

1. THE BaseNodeControlHelper SHALL be designed to work alongside existing NodeControl implementations
2. THE BaseNodeControlHelper SHALL not require changes to WorkflowNode, IWorkflowEditorHost, or other shared interfaces
3. WHEN one NodeControl class is migrated to use BaseNodeControlHelper, THE other NodeControl classes SHALL continue to function without modification
4. THE BaseNodeControlHelper SHALL not introduce global state that affects non-migrated NodeControl classes
5. THE migration process SHALL be reversible if issues are discovered during testing
