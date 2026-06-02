using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls.Helpers
{
    /// <summary>
    /// Provides centralized helper methods and fluent API for NodeControl classes to eliminate code duplication.
    /// This class extracts common patterns for title management, hover behavior, keyboard port positioning,
    /// property change handling, zoom handling, dialog management, and cleanup logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The BaseNodeControlHelper reduces NodeControl class sizes by 60-70% by centralizing shared logic
    /// that was previously duplicated across 40+ NodeControl classes. It provides a fluent API design
    /// that allows NodeControl classes to configure all behaviors in a clean, readable chain of method calls.
    /// </para>
    /// <para>
    /// Usage Example:
    /// <code>
    /// BaseNodeControlHelper
    ///     .Initialize(border, titleTextBlock, node, host)
    ///     .WithTitleManagement()
    ///     .WithHoverBehavior()
    ///     .WithKeyboardPorts()
    ///     .WithPropertySync(customPropertyHandlers)
    ///     .WithZoomHandling()
    ///     .WithDialogSupport(ctx => new MyNodeDialog(node, host, ownerWindow))
    ///     .WithCleanup()
    ///     .WithVisibilitySync()
    ///     .WithCanvasIntegration()
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public static class BaseNodeControlHelper
    {
        #region Centralized State Tracking

        /// <summary>
        /// Centralized dictionary mapping Border instances to their NodeControlContext instances.
        /// This eliminates the need for multiple separate dictionaries in each NodeControl class.
        /// </summary>
        private static readonly ConcurrentDictionary<Border, NodeControlContext> _contexts = new();

        #endregion

        #region Public API

        /// <summary>
        /// Initializes a new fluent builder for configuring node control behaviors.
        /// </summary>
        /// <param name="border">The Border UI element representing the node.</param>
        /// <param name="titleTextBlock">The TextBlock UI element displaying the node's title.</param>
        /// <param name="node">The WorkflowNode model instance.</param>
        /// <param name="host">The IWorkflowEditorHost providing access to the workflow canvas and services.</param>
        /// <returns>A FluentBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public static FluentBuilder Initialize(
            Border border,
            TextBlock titleTextBlock,
            WorkflowNode node,
            IWorkflowEditorHost host)
        {
            if (border == null) throw new ArgumentNullException(nameof(border));
            if (titleTextBlock == null) throw new ArgumentNullException(nameof(titleTextBlock));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (host == null) throw new ArgumentNullException(nameof(host));

            var context = new NodeControlContext(border, titleTextBlock, node, host);
            return new FluentBuilder(context);
        }

        /// <summary>
        /// Retrieves the NodeControlContext for a given Border instance.
        /// </summary>
        /// <param name="border">The Border instance to look up.</param>
        /// <returns>The NodeControlContext if found; otherwise, null.</returns>
        public static NodeControlContext? GetContext(Border border)
        {
            if (border == null) return null;
            _contexts.TryGetValue(border, out var context);
            return context;
        }

        /// <summary>
        /// Resets IsZooming = false trên tất cả contexts đang active.
        /// Gọi từ ZoomPanHandler ngay khi zoom kết thúc để LayoutUpdated tiếp theo
        /// không bị stuck ở nhánh "đang zoom" và restore title ngay lập tức.
        /// </summary>
        public static void ResetZoomStateForAllContexts()
        {
            foreach (var ctx in _contexts.Values)
            {
                if (ctx.IsZooming)
                    ctx.IsZooming = false;
            }
        }

        /// <summary>
        /// Removes the NodeControlContext for a given Border instance from the centralized dictionary.
        /// </summary>
        /// <param name="border">The Border instance to remove.</param>
        internal static void RemoveContext(Border border)
        {
            if (border != null)
            {
                _contexts.TryRemove(border, out _);
            }
        }

        #endregion

        #region Reflection-Based Node Property Accessors

        // Cache PropertyInfo lookups to avoid repeated reflection overhead.
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propCache = new();

        private static PropertyInfo? GetCachedProperty(Type type, string name)
        {
            return _propCache.GetOrAdd((type, name), key =>
                key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Gets the TitleDisplayMode from a node.
        /// </summary>
        internal static TitleDisplayMode GetTitleDisplayMode(WorkflowNode node)
            => node.TitleDisplayMode;

        /// <summary>
        /// Gets the TitleColorMode from a node.
        /// </summary>
        internal static TitleColorMode GetTitleColorMode(WorkflowNode node)
            => node.TitleColorMode;

        /// <summary>
        /// Gets the TitleColorKey from a node.
        /// </summary>
        internal static string? GetTitleColorKey(WorkflowNode node)
            => node.TitleColorKey;

        #endregion

        #region Public API (continued)

        /// <summary>
        /// Creates a configured TitleTextBlock for a node with initial visibility based on TitleDisplayMode.
        /// </summary>
        /// <param name="node">The WorkflowNode whose title and color properties are used.</param>
        /// <param name="displayMode">The TitleDisplayMode controlling initial visibility.</param>
        /// <param name="isHovered">Whether the border is currently being hovered.</param>
        /// <returns>A configured TextBlock ready to be added to the canvas.</returns>
        /// <remarks>
        /// Visibility rules:
        /// - Hidden → Collapsed
        /// - Hover and not hovered → Collapsed
        /// - Hover and hovered → Visible
        /// - Always → Visible
        /// </remarks>
        public static TextBlock CreateTitleTextBlock(
            WorkflowNode node,
            TitleDisplayMode displayMode,
            bool isHovered)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            // Determine initial visibility based on TitleDisplayMode and hover state
            Visibility initialVisibility = displayMode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovered ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };

            // Resolve the foreground brush using TitleColorMode and TitleColorKey (via reflection)
            var foreground = ResolveTitleBrush(GetTitleColorMode(node), GetTitleColorKey(node), node.NodeBrush);

            return new TextBlock
            {
                Text = node.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = initialVisibility
            };
        }

        /// <summary>
        /// Resolves the title foreground brush based on TitleColorMode, TitleColorKey, and NodeBrush.
        /// </summary>
        /// <param name="mode">The TitleColorMode controlling how the brush is resolved.</param>
        /// <param name="titleColorKey">The color key used when mode is CustomColor.</param>
        /// <param name="nodeBrush">The node's brush used as fallback or when mode is NodeColor.</param>
        /// <returns>The resolved Brush for the title foreground.</returns>
        public static Brush ResolveTitleBrush(
            TitleColorMode mode,
            string? titleColorKey,
            Brush nodeBrush)
        {
            if (mode == TitleColorMode.CustomColor && titleColorKey != null)
            {
                // Special case: "LimeGreen" returns a direct SolidColorBrush
                if (titleColorKey == "LimeGreen")
                {
                    return new SolidColorBrush(Colors.LimeGreen);
                }

                // Try to find the resource by key
                var resource = Application.Current?.TryFindResource(titleColorKey);
                if (resource is Brush brush)
                {
                    return brush;
                }

                // Key not found — fall back to NodeBrush
                return nodeBrush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }

            // NodeColor mode: use the node's brush
            return nodeBrush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        /// <summary>
        /// Resolves a TextOnColor brush resource by color key.
        /// </summary>
        /// <param name="colorKey">The color key to look up (e.g., "SkyAzure").</param>
        /// <returns>The resolved Brush, or a default SolidColorBrush with RGB(148, 163, 184) if not found.</returns>
        public static Brush ResolveTextOnColorBrush(string? colorKey)
        {
            if (colorKey != null)
            {
                var resource = Application.Current?.TryFindResource($"TextOnColor_{colorKey}");
                if (resource is Brush brush)
                {
                    return brush;
                }
            }

            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        /// <summary>
        /// Calculates the title position for a TextBlock above a Border.
        /// </summary>
        /// <param name="border">The Border UI element representing the node.</param>
        /// <param name="titleTextBlock">The TextBlock UI element displaying the node's title.</param>
        /// <param name="host">The IWorkflowEditorHost providing access to the workflow canvas.</param>
        /// <returns>A tuple containing the left and top coordinates for the title.</returns>
        /// <remarks>
        /// This method calculates the horizontal center of the border and positions the title
        /// above it with a 4-pixel gap. It handles NaN values from Canvas.GetLeft/GetTop by
        /// using the WorkflowNode's X/Y properties or defaulting to 0.
        /// </remarks>
        public static (double left, double top) CalculateTitlePosition(
            Border border,
            TextBlock titleTextBlock,
            IWorkflowEditorHost host)
        {
            if (border == null) throw new ArgumentNullException(nameof(border));
            if (titleTextBlock == null) throw new ArgumentNullException(nameof(titleTextBlock));
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Get border position from canvas
            var borderLeft = Canvas.GetLeft(border);
            var borderTop = Canvas.GetTop(border);

            // Use node coordinates if canvas position is NaN
            if (double.IsNaN(borderLeft))
            {
                borderLeft = border.Tag is WorkflowNode node ? node.X : 0;
            }

            if (double.IsNaN(borderTop))
            {
                borderTop = border.Tag is WorkflowNode node ? node.Y : 0;
            }

            // Get border dimensions
            var borderWidth = border.ActualWidth;
            var borderHeight = border.ActualHeight;

            // Force layout calculation if title width is 0
            if (titleTextBlock.ActualWidth == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }

            // Get title dimensions
            var titleWidth = titleTextBlock.ActualWidth;
            var titleHeight = titleTextBlock.ActualHeight;

            // Calculate title position: center horizontally above the border
            var titleLeft = borderLeft + (borderWidth / 2) - (titleWidth / 2);
            var titleTop = borderTop - titleHeight - 4;

            return (titleLeft, titleTop);
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Fluent API builder for configuring node control behaviors through method chaining.
        /// </summary>
        public class FluentBuilder
        {
            private readonly NodeControlContext _context;
            private readonly List<Action> _eventRegistrations = new();

            /// <summary>
            /// Initializes a new instance of the FluentBuilder class.
            /// </summary>
            /// <param name="context">The NodeControlContext to configure.</param>
            internal FluentBuilder(NodeControlContext context)
            {
                _context = context ?? throw new ArgumentNullException(nameof(context));
            }

            /// <summary>
            /// Configures title management including creation, visibility, and positioning logic.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithTitleManagement()
            {
                _eventRegistrations.Add(() =>
                {
                    RegisterTitleEvents(_context);
                });
                return this;
            }

            /// <summary>
            /// Configures hover behavior including MouseEnter and MouseLeave event handlers.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithHoverBehavior()
            {
                _eventRegistrations.Add(() =>
                {
                    _context.Border.Focusable = true;
                    _context.Border.FocusVisualStyle = null;

                    _context.Border.MouseEnter += (s, e) => HandleMouseEnter(_context);
                    _context.Border.MouseLeave += (s, e) => HandleMouseLeave(_context);
                });
                return this;
            }

            /// <summary>
            /// Configures keyboard port positioning for arrow key handlers.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithKeyboardPorts()
            {
                _eventRegistrations.Add(() =>
                {
                    _context.Border.PreviewKeyDown += (s, e) => HandleKeyboardPortPositioning(_context, e);
                });
                return this;
            }

            /// <summary>
            /// Configures property change synchronization with optional custom handlers.
            /// </summary>
            /// <param name="customHandlers">Optional dictionary mapping property names to custom update actions.</param>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithPropertySync(
                Dictionary<string, Action<NodeControlContext>>? customHandlers = null)
            {
                _eventRegistrations.Add(() =>
                {
                    RegisterPropertyEvents(_context, customHandlers);
                });
                return this;
            }

            /// <summary>
            /// Configures zoom handling to hide titles during zoom operations.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithZoomHandling()
            {
                // TODO: Implement zoom handling logic
                return this;
            }

            /// <summary>
            /// Configures dialog support for right-click dialog opening.
            /// </summary>
            /// <param name="dialogFactory">Factory function that creates the dialog instance.</param>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithDialogSupport(Func<NodeControlContext, object> dialogFactory)
            {
                _eventRegistrations.Add(() =>
                {
                    _context.Border.MouseRightButtonUp += (s, e) =>
                        HandleMouseRightButtonUp(_context, e, dialogFactory);
                });
                return this;
            }

            /// <summary>
            /// Configures cleanup logic for the Unloaded event to prevent memory leaks.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithCleanup()
            {
                _eventRegistrations.Add(() =>
                {
                    _context.Border.Unloaded += (s, e) => HandleUnloaded(_context);
                });
                return this;
            }

            /// <summary>
            /// Configures visibility synchronization between Border and TitleTextBlock.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithVisibilitySync()
            {
                _eventRegistrations.Add(() =>
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(
                        UIElement.VisibilityProperty, typeof(Border));
                    if (descriptor != null)
                    {
                        EventHandler handler = (s, e) => HandleVisibilityChanged(_context);
                        descriptor.AddValueChanged(_context.Border, handler);
                        // Track for cleanup — DependencyPropertyDescriptor handlers are NOT
                        // standard CLR events and must be removed via RemoveValueChanged.
                        _context.TrackDpDescriptorHandler(descriptor, _context.Border, handler);
                    }
                });
                return this;
            }

            /// <summary>
            /// Configures canvas integration for adding TitleTextBlock to WorkflowCanvas.
            /// </summary>
            /// <returns>This FluentBuilder instance for method chaining.</returns>
            public FluentBuilder WithCanvasIntegration()
            {
                _eventRegistrations.Add(() =>
                {
                    _context.Border.Loaded += (s, e) => HandleLoaded(_context);
                });
                return this;
            }

            /// <summary>
            /// Applies all configured behaviors and stores the context in the centralized dictionary.
            /// </summary>
            /// <returns>The configured NodeControlContext instance.</returns>
            public NodeControlContext Build()
            {
                // Execute all event registrations
                foreach (var registration in _eventRegistrations)
                {
                    registration();
                }

                // Store context in centralized dictionary
                _contexts[_context.Border] = _context;

                return _context;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Registers all title-related event handlers (Loaded, SizeChanged, LayoutUpdated).
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method registers event handlers in a logical group for title management.
        /// All registered handlers are tracked in the context for later cleanup.
        /// </remarks>
        internal static void RegisterTitleEvents(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Register Loaded event handler
            RoutedEventHandler loadedHandler = (s, e) => HandleLoaded(context);
            context.Border.Loaded += loadedHandler;
            context.TrackEventHandler(context.Border, loadedHandler);

            // Register SizeChanged event handler
            SizeChangedEventHandler sizeChangedHandler = (s, e) => HandleSizeChanged(context);
            context.Border.SizeChanged += sizeChangedHandler;
            context.TrackEventHandler(context.Border, sizeChangedHandler);

            // Register LayoutUpdated event handler
            EventHandler layoutUpdatedHandler = (s, e) => HandleLayoutUpdated(context);
            context.Border.LayoutUpdated += layoutUpdatedHandler;
            context.TrackEventHandler(context.Border, layoutUpdatedHandler);
        }

        /// <summary>
        /// Registers all property change event handlers for the node.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="customHandlers">Optional dictionary mapping property names to custom update actions.</param>
        /// <remarks>
        /// This method registers event handlers in a logical group for property synchronization.
        /// All registered handlers are tracked in the context for later cleanup.
        /// </remarks>
        internal static void RegisterPropertyEvents(
            NodeControlContext context,
            Dictionary<string, Action<NodeControlContext>>? customHandlers = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // WorkflowNode implements INotifyPropertyChanged directly
            {
                var npc = context.Node; // WorkflowNode now implements INotifyPropertyChanged directly
                // Register PropertyChanged event handler
                PropertyChangedEventHandler propertyChangedHandler = (s, e) =>
                    HandlePropertyChanged(context, e.PropertyName, customHandlers);
                npc.PropertyChanged += propertyChangedHandler;
                context.TrackEventHandler(npc, propertyChangedHandler);
            }
        }

        /// <summary>
        /// Handles property change notifications from the node and updates UI accordingly.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="propertyName">The name of the property that changed.</param>
        /// <param name="customHandlers">Optional dictionary mapping property names to custom update actions.</param>
        /// <remarks>
        /// This method implements thread-safe property change handling:
        /// - Creates default handlers dictionary for common properties (ColorKey, NodeBrush, Title, etc.)
        /// - Merges custom handlers with default handlers
        /// - Looks up handler for the changed property name
        /// - Invokes the handler if found
        /// - Uses Dispatcher to marshal UI updates to the UI thread
        /// </remarks>
        internal static void HandlePropertyChanged(
            NodeControlContext context,
            string? propertyName,
            Dictionary<string, Action<NodeControlContext>>? customHandlers = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Create default handlers dictionary for common node properties.
            // Custom handlers passed by the caller will override these defaults,
            // allowing node-specific logic (e.g. updating an icon fill for ColorKey).
            var handlers = new Dictionary<string, Action<NodeControlContext>>
            {
                // ColorKey: no default UI action — node-specific code handles icon fill updates.
                // Providing an empty entry ensures the key is present so custom handlers can override it.
                [nameof(WorkflowNode.ColorKey)] = ctx => { },

                // NodeBrush: update border background AND resolve the title foreground brush.
                // The title foreground must be resolved via ResolveTitleBrush so that TitleColorMode
                // and TitleColorKey are respected rather than blindly using NodeBrush.
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    if (ctx.Border != null)
                    {
                        // Liquid Glass mode: dùng glass background thay vì solid NodeBrush
                        if (LiquidGlassHelper.IsLiquidGlassMode(ctx.Host))
                        {
                            var baseColor = LiquidGlassHelper.GetColorFromBrush(ctx.Node.NodeBrush);
                            ctx.Border.Background = LiquidGlassHelper.CreateGlassBackground(baseColor);
                        }
                        else
                        {
                            ctx.Border.Background = ctx.Node.NodeBrush;
                        }
                    }
                    if (ctx.TitleTextBlock != null)
                    {
                        if (LiquidGlassHelper.IsLiquidGlassMode(ctx.Host))
                        {
                            LiquidGlassHelper.ApplyGlassTextStyle(ctx.TitleTextBlock);
                        }
                        else
                        {
                            ctx.TitleTextBlock.Foreground = ResolveTitleBrush(
                                GetTitleColorMode(ctx.Node),
                                GetTitleColorKey(ctx.Node),
                                ctx.Node.NodeBrush);
                        }
                    }
                },

                // Title: update the displayed text and recalculate position.
                [nameof(WorkflowNode.Title)] = ctx =>
                {
                    if (ctx.TitleTextBlock != null)
                    {
                        ctx.TitleTextBlock.Text = ctx.Node.Title ?? string.Empty;
                        if (ctx.IsBorderVisible())
                        {
                            UpdateTitlePosition(ctx);
                        }
                    }
                },

                // TitleDisplayMode: update title visibility when the mode changes.
                // Only update when the border is visible (requirement 8.9).
                ["TitleDisplayMode"] = ctx =>
                {
                    if (ctx.IsBorderVisible())
                    {
                        UpdateTitleVisibility(ctx, GetTitleDisplayMode(ctx.Node), ctx.IsHovered);
                    }
                },

                // TitleColorMode: re-resolve the title foreground brush using the new mode.
                ["TitleColorMode"] = ctx =>
                {
                    if (ctx.TitleTextBlock != null)
                    {
                        ctx.TitleTextBlock.Foreground = ResolveTitleBrush(
                            GetTitleColorMode(ctx.Node),
                            GetTitleColorKey(ctx.Node),
                            ctx.Node.NodeBrush);
                    }
                },

                // TitleColorKey: re-resolve the title foreground brush using the new key.
                ["TitleColorKey"] = ctx =>
                {
                    if (ctx.TitleTextBlock != null)
                    {
                        ctx.TitleTextBlock.Foreground = ResolveTitleBrush(
                            GetTitleColorMode(ctx.Node),
                            GetTitleColorKey(ctx.Node),
                            ctx.Node.NodeBrush);
                    }
                }
            };

            // Merge custom handlers with default handlers.
            // Custom handlers take precedence — they completely replace the default for that key.
            if (customHandlers != null)
            {
                foreach (var kvp in customHandlers)
                {
                    handlers[kvp.Key] = kvp.Value;
                }
            }

            // Look up handler for propertyName and invoke if found.
            if (propertyName != null && handlers.TryGetValue(propertyName, out var handler))
            {
                // Use Dispatcher for thread safety when updating UI.
                var dispatcher = context.GetDispatcher();
                if (dispatcher != null)
                {
                    if (dispatcher.CheckAccess())
                    {
                        // Already on UI thread — execute directly.
                        handler(context);
                    }
                    else
                    {
                        // Marshal to UI thread.
                        dispatcher.BeginInvoke(() => handler(context), DispatcherPriority.Normal);
                    }
                }
                else
                {
                    // No dispatcher available — execute directly as best effort.
                    handler(context);
                }
            }
        }

        /// <summary>
        /// Handles the Loaded event to add the title to the canvas and initialize its state.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        internal static void HandleLoaded(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Add title to canvas if not already present
            if (context.Host.WorkflowCanvas?.Children.Contains(context.TitleTextBlock) != true)
            {
                context.Host.WorkflowCanvas?.Children.Add(context.TitleTextBlock);
            }

            // Set ZIndex to 20000
            Canvas.SetZIndex(context.TitleTextBlock, 20000);

            // Update title visibility based on TitleDisplayMode and hover state
            UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);

            // Update title position
            UpdateTitlePosition(context);
        }

        /// <summary>
        /// Handles the SizeChanged event to update the title position.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        internal static void HandleSizeChanged(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Update title position when the border size changes
            UpdateTitlePosition(context);
        }

        /// <summary>
        /// Handles the LayoutUpdated event with zoom handling and throttled position updates.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method implements the following logic per Requirements 9.1–9.9:
        /// 1. If border is not visible, collapse title and return early.
        /// 2. If NodeChrome.IsZooming is true: collapse title, set context.IsZooming = true,
        ///    mark TitleUpdatedAfterZoom = false, and return.
        /// 3. If zoom just ended (context.IsZooming was true, NodeChrome.IsZooming is now false):
        ///    set context.IsZooming = false, restore title visibility/position if border is visible,
        ///    mark TitleUpdatedAfterZoom = true, then fall through to pan/drag check.
        /// 4. If IsPanning or DraggedNode == current node: skip throttled update.
        /// 5. Otherwise: schedule throttled title update.
        ///
        /// Thread safety: LayoutUpdated can fire from a background thread, so all UI work
        /// is marshalled to the UI thread via Dispatcher.BeginInvoke when needed.
        /// </remarks>
        internal static void HandleLayoutUpdated(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var dispatcher = context.GetDispatcher();
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                // Marshal to UI thread and re-invoke
                dispatcher.BeginInvoke(new Action(() => HandleLayoutUpdated(context)),
                    DispatcherPriority.Normal);
                return;
            }

            // --- All code below runs on the UI thread ---

            // If border is not visible, collapse title and return
            if (context.Border?.Visibility != Visibility.Visible)
            {
                context.TitleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            bool isZooming = NodeChrome.IsZooming;

            // Requirement 9.2 & 9.3: While zooming, hide title and mark not updated after zoom.
            // Also track zoom state in context.IsZooming so we can detect the zoom-end transition.
            if (isZooming)
            {
                if (context.TitleTextBlock.Visibility != Visibility.Collapsed)
                {
                    context.TitleTextBlock.Visibility = Visibility.Collapsed;
                }
                context.TitleUpdatedAfterZoom = false;
                context.IsZooming = true;
                return;
            }

            // Requirement 9.4, 9.5, 9.6: Zoom just ended (context.IsZooming was true, now NodeChrome.IsZooming is false).
            // Restore title visibility and position, then mark zoom as complete.
            if (context.IsZooming)
            {
                context.IsZooming = false;
                if (context.IsBorderVisible())
                {
                    UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);
                    if (context.TitleTextBlock.Visibility == Visibility.Visible)
                    {
                        UpdateTitlePosition(context);
                    }
                }
                context.TitleUpdatedAfterZoom = true;
                // Fall through to check pan/drag below
            }
            else if (!context.TitleUpdatedAfterZoom)
            {
                // Fallback: handle the case where zoom ended but context.IsZooming wasn't set
                // (e.g. first LayoutUpdated after initialization). Restore title state.
                context.TitleUpdatedAfterZoom = true;
                UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);
                if (context.TitleTextBlock.Visibility == Visibility.Visible)
                {
                    UpdateTitlePosition(context);
                }
                // Fall through to check pan/drag below
            }

            // Requirement 9.7 & 9.8: Skip throttled update when panning or dragging this node
            if (context.Host.IsPanning || context.Host.DraggedNode == context.Node)
            {
                return;
            }

            // Requirement 9.9: Schedule throttled title position update
            if (context.TitleTextBlock.Visibility == Visibility.Visible)
            {
                ScheduleThrottledTitleUpdate(context);
            }
        }

        /// <summary>
        /// Handles the Unloaded event to clean up resources and prevent memory leaks.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method performs best-effort cleanup when a node's Border is unloaded:
        /// - Stops and disposes the TitleUpdateTimer to prevent further UI updates
        /// - Removes the TitleTextBlock from the WorkflowCanvas
        /// - Clears the node's TitleTextBlockUI reference if it still points to this TextBlock
        /// - Calls context.Dispose() to unregister all tracked event handlers
        /// - Removes the context from the centralized dictionary
        ///
        /// All operations are wrapped in a single try-catch to suppress exceptions and avoid
        /// crashing the unload path (Requirement 10.6).
        /// </remarks>
        internal static void HandleUnloaded(NodeControlContext context)
        {
            if (context == null) return;

            try
            {
                // Stop and dispose TitleUpdateTimer (Requirement 10.2)
                if (context.TitleUpdateTimer != null)
                {
                    context.TitleUpdateTimer.Stop();
                    context.TitleUpdateTimer = null;
                }

                // Remove TitleTextBlock from WorkflowCanvas (Requirement 10.4)
                if (context.Host?.WorkflowCanvas?.Children.Contains(context.TitleTextBlock) == true)
                {
                    context.Host.WorkflowCanvas.Children.Remove(context.TitleTextBlock);
                }

                // Clear node's TitleTextBlockUI reference if it matches (Requirement 10.5)
                if (ReferenceEquals(context.Node?.TitleTextBlockUI, context.TitleTextBlock))
                {
                    context.Node.TitleTextBlockUI = null;
                }

                // Dispose context to unregister all event handlers (Requirement 10.7)
                context.Dispose();

                // Remove context from centralized dictionary (Requirement 10.3)
                RemoveContext(context.Border);
            }
            catch
            {
                // Best-effort cleanup; suppress exceptions to avoid crashing the unload path (Requirement 10.6)
            }
        }

        /// <summary>
        /// Handles the MouseEnter event to update hover state and title visibility.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method implements the hover enter logic:
        /// - Updates the context hover state to true
        /// - If the border is visible, updates title visibility based on TitleDisplayMode
        /// - If the border is visible, updates title position
        /// - Sets focus to the border using Dispatcher with Input priority
        /// 
        /// Thread safety is ensured by using the Dispatcher to marshal UI operations.
        /// </remarks>
        internal static void HandleMouseEnter(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Update hover state to true
            context.UpdateHoverState(true);

            // If border is visible, update title visibility and position
            if (context.IsBorderVisible())
            {
                // Update title visibility based on TitleDisplayMode
                UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);

                // Update title position
                UpdateTitlePosition(context);
            }

            // Set focus to border using Dispatcher with Input priority
            var dispatcher = context.GetDispatcher();
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(() =>
                {
                    context.Border?.Focus();
                }, DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// Handles the MouseLeave event to update hover state and title visibility.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method implements the hover leave logic:
        /// - Updates the context hover state to false
        /// - If the border is visible, updates title visibility based on TitleDisplayMode
        /// 
        /// Thread safety is ensured by using the Dispatcher to marshal UI updates.
        /// </remarks>
        internal static void HandleMouseLeave(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Update hover state to false
            context.UpdateHoverState(false);

            // If border is visible, update title visibility based on TitleDisplayMode
            if (context.IsBorderVisible())
            {
                UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);
            }
        }

        /// <summary>
        /// Updates the visibility of the title text block based on the title display mode and hover state.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="mode">The title display mode (Hidden, Hover, or Always).</param>
        /// <param name="isHovered">Whether the border is currently being hovered.</param>
        /// <remarks>
        /// This method implements the following visibility logic:
        /// - Hidden: Always Collapsed
        /// - Hover: Visible only when hovered AND border is visible
        /// - Always: Visible when border is visible
        /// 
        /// If the border visibility is Collapsed, the title is always Collapsed regardless of mode.
        /// Thread safety is ensured by using the Dispatcher to marshal UI updates.
        /// </remarks>
        internal static void UpdateTitleVisibility(
            NodeControlContext context,
            TitleDisplayMode mode,
            bool isHovered)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var dispatcher = context.GetDispatcher();
            if (dispatcher == null) return;

            // Use Dispatcher to ensure thread-safe UI updates
            if (dispatcher.CheckAccess())
            {
                // Already on UI thread, execute directly
                UpdateTitleVisibilityInternal(context, mode, isHovered);
            }
            else
            {
                // Marshal to UI thread
                dispatcher.BeginInvoke(() =>
                {
                    UpdateTitleVisibilityInternal(context, mode, isHovered);
                }, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// Internal implementation of UpdateTitleVisibility that performs the actual visibility update.
        /// This method should only be called on the UI thread.
        /// </summary>
        private static void UpdateTitleVisibilityInternal(
            NodeControlContext context,
            TitleDisplayMode mode,
            bool isHovered)
        {
            // If border is not visible, always collapse the title
            if (context.Border?.Visibility != Visibility.Visible)
            {
                context.TitleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            // Determine visibility based on title display mode
            Visibility newVisibility = mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovered ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };

            context.TitleTextBlock.Visibility = newVisibility;
        }

        /// <summary>
        /// Updates the title position by calculating coordinates and setting Canvas.Left/Canvas.Top.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method uses Dispatcher for thread safety to ensure UI updates occur on the UI thread.
        /// It calls CalculateTitlePosition to get the coordinates and then sets Canvas.Left and Canvas.Top
        /// on the TitleTextBlock.
        /// </remarks>
        internal static void UpdateTitlePosition(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var dispatcher = context.GetDispatcher();
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
            {
                // Already on UI thread - update directly
                var (left, top) = CalculateTitlePosition(context.Border, context.TitleTextBlock, context.Host);
                Canvas.SetLeft(context.TitleTextBlock, left);
                Canvas.SetTop(context.TitleTextBlock, top);
            }
            else
            {
                // Marshal to UI thread
                dispatcher.BeginInvoke(() =>
                {
                    var (left, top) = CalculateTitlePosition(context.Border, context.TitleTextBlock, context.Host);
                    Canvas.SetLeft(context.TitleTextBlock, left);
                    Canvas.SetTop(context.TitleTextBlock, top);
                }, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// Schedules a throttled title position update using a DispatcherTimer.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="throttleMs">The throttle interval in milliseconds (default: 50).</param>
        /// <remarks>
        /// This method implements throttled title position updates:
        /// - Checks if a timer already exists and is running; if so, restarts it
        /// - Creates a new DispatcherTimer with the specified interval
        /// - In the timer tick handler: stops the timer and updates title position
        /// - Stores the timer in context.TitleUpdateTimer for later cleanup
        /// 
        /// Thread safety is ensured by using the Dispatcher to marshal UI operations.
        /// </remarks>
        internal static void ScheduleThrottledTitleUpdate(NodeControlContext context, int throttleMs = 50)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Check if timer already exists and is running; if so, restart it
            if (context.TitleUpdateTimer != null && context.TitleUpdateTimer.IsEnabled)
            {
                context.TitleUpdateTimer.Stop();
                context.TitleUpdateTimer.Start();
            }
            else
            {
                // Create new DispatcherTimer with specified interval
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(throttleMs)
                };

                // Store timer in context before attaching event handler
                context.TitleUpdateTimer = timer;

                // Attach tick handler
                timer.Tick += (s, e) =>
                {
                    // Stop timer
                    timer.Stop();

                    // Update title position
                    UpdateTitlePosition(context);
                };

                // Start timer
                timer.Start();
            }
        }

        /// <summary>
        /// Handles the MouseRightButtonUp event to open a node configuration dialog.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="e">The MouseButtonEventArgs containing event data.</param>
        /// <param name="dialogFactory">Factory function that creates the dialog instance.</param>
        /// <remarks>
        /// This method implements the dialog opening logic for right-click on a node:
        /// - Marks the event as handled to prevent further routing
        /// - Releases mouse capture if the border has capture
        /// - Sets DraggedNode and SelectedNode to null to avoid conflicts
        /// - Gets or creates the NodeDialogManager from the host
        /// - Checks if a dialog is already open for the same node (returns early if so)
        /// - Closes any dialog open for a different node before opening the new one
        /// - Invokes the dialog factory to create and open the dialog
        /// </remarks>
        internal static void HandleMouseRightButtonUp(
            NodeControlContext context,
            MouseButtonEventArgs e,
            Func<NodeControlContext, object> dialogFactory)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (dialogFactory == null) throw new ArgumentNullException(nameof(dialogFactory));

            // Mark event as handled to prevent further routing
            e.Handled = true;

            // Release mouse capture if the border has capture
            if (Mouse.Captured == context.Border)
            {
                Mouse.Capture(null);
            }

            // Set DraggedNode to null to avoid drag conflicts
            context.Host.DraggedNode = null;

            // Set SelectedNode to null to avoid selection conflicts
            if (context.Host.ViewModel != null)
            {
                context.Host.ViewModel.SelectedNode = null;
            }

            // Get or create the NodeDialogManager
            var dialogManager = GetOrCreateDialogManager(context.Host);

            // Check if a dialog is already open for the same node
            if (dialogManager.CurrentNode == context.Node)
            {
                // Dialog already open for this node, return early
                return;
            }

            // Check if a dialog is open for a different node
            if (dialogManager.IsDialogOpen)
            {
                // Close the existing dialog before opening the new one
                dialogManager.CloseCurrentDialog();
            }

            // Create and open the dialog using the factory
            var dialog = dialogFactory(context) as Window;
            if (dialog != null)
            {
                dialogManager.OpenDialog(context.Node, dialog, context.Host);
            }
        }

        /// <summary>
        /// Handles keyboard port positioning for arrow key events.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <param name="e">The KeyEventArgs containing the key event data.</param>
        /// <remarks>
        /// This method processes PreviewKeyDown events to change port positions using arrow keys.
        /// When Shift is not pressed, it updates the input port position.
        /// When Shift is pressed, it updates the output port position.
        /// The method only processes events when the border is currently hovered.
        /// </remarks>
        internal static void HandleKeyboardPortPositioning(NodeControlContext context, KeyEventArgs e)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Check if border is hovered; if not, return early
            if (!context.IsHovered) return;

            // Map arrow keys to port positions
            PortPosition? newPos = e.Key switch
            {
                Key.Left => PortPosition.Left,
                Key.Up => PortPosition.Top,
                Key.Right => PortPosition.Right,
                Key.Down => PortPosition.Bottom,
                _ => null
            };

            if (newPos == null) return;

            // Mark event as handled
            e.Handled = true;

            // Determine if this is for input or output port based on Shift key
            bool isInputPort = (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift;

            // Update port position and connection paths through IWorkflowEditorHost
            ChangePortPosition(context.Node, newPos.Value, isInputPort, context.Host);
        }

        /// <summary>
        /// Handles visibility change events to synchronize title visibility with border visibility.
        /// </summary>
        /// <param name="context">The NodeControlContext containing all required state.</param>
        /// <remarks>
        /// This method implements visibility synchronization logic:
        /// - When border visibility is not Visible, sets title visibility to Collapsed
        /// - When border visibility is Visible, updates title visibility based on TitleDisplayMode and hover state
        /// - Uses Dispatcher for thread safety when updating UI
        /// 
        /// Thread safety is ensured by using the Dispatcher to marshal UI operations.
        /// </remarks>
        internal static void HandleVisibilityChanged(NodeControlContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var dispatcher = context.GetDispatcher();
            if (dispatcher == null) return;

            // Use Dispatcher to ensure thread-safe UI updates
            if (dispatcher.CheckAccess())
            {
                // Already on UI thread, execute directly
                HandleVisibilityChangedInternal(context);
            }
            else
            {
                // Marshal to UI thread
                dispatcher.BeginInvoke(() =>
                {
                    HandleVisibilityChangedInternal(context);
                }, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// Internal implementation of HandleVisibilityChanged that performs the actual visibility update.
        /// This method should only be called on the UI thread.
        /// </summary>
        private static void HandleVisibilityChangedInternal(NodeControlContext context)
        {
            // If border is not visible, always collapse the title
            if (context.Border?.Visibility != Visibility.Visible)
            {
                context.TitleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            // When border is visible, update title visibility based on TitleDisplayMode and hover state
            UpdateTitleVisibility(context, GetTitleDisplayMode(context.Node), context.IsHovered);
        }

        /// <summary>
        /// Changes the port position for a node and updates connection paths.
        /// </summary>
        /// <param name="node">The WorkflowNode whose port position should be changed.</param>
        /// <param name="newPosition">The new port position.</param>
        /// <param name="isInputPort">True to change the input port position; false for output port.</param>
        /// <param name="host">The IWorkflowEditorHost used to update connection paths and animations.</param>
        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;

            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);

            if (port == null || port.Position == newPosition) return;

            port.Position = newPosition;

            // Update port positions on the specified side
            host.UpdatePortsPositionOnSide(node, newPosition);

            // Update connection paths and animations
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer?.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer?.UpdateAllConnectionAnimations(cons);
                }
                catch
                {
                    // Best-effort update; suppress exceptions to avoid crashing
                }
            }
        }

        /// <summary>
        /// Gets or creates the NodeDialogManager from the IWorkflowEditorHost.
        /// </summary>
        /// <param name="host">The IWorkflowEditorHost instance.</param>
        /// <returns>The NodeDialogManager instance.</returns>
        /// <remarks>
        /// This method attempts to retrieve the NodeDialogManager from a WorkflowEditorWindow
        /// using reflection to access the private _nodeDialogManager field. If the host is not
        /// a WorkflowEditorWindow or the field is not found, it creates and returns a new instance.
        /// </remarks>
        internal static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                // Use reflection to access the private _nodeDialogManager field
                var fieldInfo = typeof(WorkflowEditorWindow).GetField(
                    "_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (fieldInfo != null && fieldInfo.GetValue(window) is NodeDialogManager dialogManager)
                {
                    return dialogManager;
                }
            }

            // Create and return a new NodeDialogManager instance
            return new NodeDialogManager();
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Encapsulates all state needed for node control operations.
        /// This context object eliminates the need to pass multiple parameters to every helper method.
        /// </summary>
        public class NodeControlContext : IDisposable
        {
            #region Core References

            /// <summary>
            /// Gets the Border UI element representing the node.
            /// </summary>
            public Border Border { get; }

            /// <summary>
            /// Gets the TextBlock UI element displaying the node's title.
            /// </summary>
            public TextBlock TitleTextBlock { get; }

            /// <summary>
            /// Gets the WorkflowNode model instance.
            /// </summary>
            public WorkflowNode Node { get; }

            /// <summary>
            /// Gets the IWorkflowEditorHost providing access to the workflow canvas and services.
            /// </summary>
            public IWorkflowEditorHost Host { get; }

            #endregion

            #region State Tracking

            /// <summary>
            /// Gets or sets a value indicating whether the Border is currently being hovered.
            /// </summary>
            public bool IsHovered { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether a zoom operation is currently in progress.
            /// </summary>
            public bool IsZooming { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the title has been updated after a zoom operation completed.
            /// </summary>
            public bool TitleUpdatedAfterZoom { get; set; }

            /// <summary>
            /// Gets or sets the DispatcherTimer used for throttling title position updates.
            /// </summary>
            public DispatcherTimer? TitleUpdateTimer { get; set; }

            #endregion

            #region Event Handler Tracking

            /// <summary>
            /// Tracks registered event handlers for cleanup during disposal.
            /// </summary>
            private readonly List<(object target, Delegate handler)> _eventHandlers = new();

            /// <summary>
            /// Tracks DependencyPropertyDescriptor value-changed handlers for cleanup.
            /// These are NOT standard CLR events and must be removed via RemoveValueChanged.
            /// </summary>
            private readonly List<(DependencyPropertyDescriptor descriptor, DependencyObject target, EventHandler handler)> _dpDescriptorHandlers = new();

            /// <summary>
            /// Registers a DependencyPropertyDescriptor value-changed handler for later cleanup.
            /// </summary>
            internal void TrackDpDescriptorHandler(
                DependencyPropertyDescriptor descriptor,
                DependencyObject target,
                EventHandler handler)
            {
                if (descriptor != null && target != null && handler != null)
                    _dpDescriptorHandlers.Add((descriptor, target, handler));
            }

            #endregion

            #region Constructor

            /// <summary>
            /// Initializes a new instance of the NodeControlContext class.
            /// </summary>
            /// <param name="border">The Border UI element.</param>
            /// <param name="titleTextBlock">The TextBlock UI element.</param>
            /// <param name="node">The WorkflowNode model instance.</param>
            /// <param name="host">The IWorkflowEditorHost instance.</param>
            public NodeControlContext(
                Border border,
                TextBlock titleTextBlock,
                WorkflowNode node,
                IWorkflowEditorHost host)
            {
                Border = border ?? throw new ArgumentNullException(nameof(border));
                TitleTextBlock = titleTextBlock ?? throw new ArgumentNullException(nameof(titleTextBlock));
                Node = node ?? throw new ArgumentNullException(nameof(node));
                Host = host ?? throw new ArgumentNullException(nameof(host));
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Gets the Dispatcher from the Border or Application.Current.
            /// </summary>
            /// <returns>The Dispatcher instance if available; otherwise, null.</returns>
            public Dispatcher? GetDispatcher()
            {
                return Border?.Dispatcher ?? Application.Current?.Dispatcher;
            }

            /// <summary>
            /// Checks if the Border is currently visible.
            /// </summary>
            /// <returns>True if the Border visibility is Visible; otherwise, false.</returns>
            public bool IsBorderVisible()
            {
                return Border?.Visibility == Visibility.Visible;
            }

            /// <summary>
            /// Updates the hover state of the context.
            /// </summary>
            /// <param name="isHovered">The new hover state.</param>
            public void UpdateHoverState(bool isHovered)
            {
                IsHovered = isHovered;
            }

            /// <summary>
            /// Tracks an event handler for later cleanup during disposal.
            /// </summary>
            /// <param name="target">The object that the event handler is attached to.</param>
            /// <param name="handler">The event handler delegate.</param>
            public void TrackEventHandler(object target, Delegate handler)
            {
                _eventHandlers.Add((target, handler));
            }

            /// <summary>
            /// Unregisters all tracked event handlers.
            /// </summary>
            internal void UnregisterAllEventHandlers()
            {
                foreach (var (target, handler) in _eventHandlers)
                {
                    try
                    {
                        // Find the specific event that matches this handler's delegate type,
                        // then remove only from that event. Avoids ArgumentException from
                        // trying to remove a handler from an incompatible event signature.
                        var targetType = target.GetType();
                        var handlerType = handler.GetType();
                        var events = targetType.GetEvents(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance);

                        foreach (var eventInfo in events)
                        {
                            // Only attempt removal when the event's handler type is assignable
                            // from the delegate type — avoids ArgumentException on type mismatch.
                            if (eventInfo.EventHandlerType != null &&
                                eventInfo.EventHandlerType.IsAssignableFrom(handlerType))
                            {
                                try { eventInfo.RemoveEventHandler(target, handler); }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
                _eventHandlers.Clear();

                // Remove DependencyPropertyDescriptor value-changed handlers.
                // These are NOT standard CLR events — they must be removed via RemoveValueChanged,
                // otherwise they leak and fire on unloaded borders causing ArgumentException.
                foreach (var (descriptor, target, handler) in _dpDescriptorHandlers)
                {
                    try { descriptor.RemoveValueChanged(target, handler); }
                    catch { }
                }
                _dpDescriptorHandlers.Clear();
            }

            #endregion

            #region IDisposable Implementation

            private bool _disposed = false;

            /// <summary>
            /// Disposes the context and cleans up resources including timers and event handlers.
            /// </summary>
            public void Dispose()
            {
                if (_disposed) return;

                try
                {
                    // Stop and dispose timer
                    if (TitleUpdateTimer != null)
                    {
                        TitleUpdateTimer.Stop();
                        TitleUpdateTimer = null;
                    }

                    // Unregister event handlers
                    UnregisterAllEventHandlers();
                }
                catch
                {
                    // Suppress exceptions during disposal
                }

                _disposed = true;
            }

            #endregion
        }

        #endregion
    }
}
