using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class DynamicUiNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }

        public static Border CreateBorder(DynamicUiNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- 1. BORDER ---
            var border = new Border
            {
                Width = Math.Max(600, node.Width),
                Height = Math.Max(600, node.Height),
                MinWidth = 600,
                MinHeight = 600,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node
            };

            if (FlowMy.Services.FloatingWidgetManager.Instance.IsWidgetOpen(node.Id))
            {
                border.Visibility = Visibility.Collapsed;
            }

            GpuOptimizationHelper.ApplyToBorder(border);

            // --- 2. GRID CONTAINER ---
            var grid = new Grid();
            GpuOptimizationHelper.ApplyToElement(grid);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Row 0: Top Bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Sciter UI
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Row 2: Bottom Bar
            border.Child = grid;

            // --- 3. SCITER EMBEDDED CONTROL ---
            var sciterControl = new SciterEmbeddedControl(node, host);
            sciterControl.FormSubmitted += (s, outputs) =>
            {
                if (outputs != null)
                {
                    foreach (var kvp in outputs)
                    {
                        node.ResolvedOutputs[kvp.Key] = kvp.Value;
                    }
                }
                host.RequestSyncDataPanels(immediate: true);
            };
            Grid.SetRow(sciterControl, 1);
            grid.Children.Add(sciterControl);

            // --- 4. FLOATING CANVAS TITLE (managed by BaseNodeControlHelper, NOT added to grid) ---
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Dynamic UI Form",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // --- 5. TOP BAR (Inside the node grid) ---
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Top
            };
            var topBarGrid = new Grid();
            var topBarText = new TextBlock
            {
                Text = node.Title ?? "Dynamic UI Form",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            topBarGrid.Children.Add(topBarText);
            topBar.Child = topBarGrid;
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // --- 6. BOTTOM BAR ---
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var bottomText = new TextBlock
            {
                Text = "Dynamic UI • Chuột phải để mở cấu hình",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            bottomBar.Child = bottomText;
            Grid.SetRow(bottomBar, 2);
            grid.Children.Add(bottomBar);

            // --- 7. RESIZE HANDLE OVERLAY ---
            var handleOverlay = new Grid { IsHitTestVisible = false };
            AddResizeHandle(handleOverlay, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(2, 2, 0, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            Grid.SetRowSpan(handleOverlay, 3);
            grid.Children.Add(handleOverlay);

            void RefreshDynamicUiScale()
            {
                var heightBaseline = border.MinHeight > 0 ? border.MinHeight : 600.0;
                var rawScale = heightBaseline > 0 ? node.Height / heightBaseline : 1.0;
                var topBottomScaleFactor = Math.Max(1.0, rawScale);

                var scaleTransform = new ScaleTransform(topBottomScaleFactor, topBottomScaleFactor);
                if (topBarGrid != null)
                    topBarGrid.LayoutTransform = scaleTransform;
                if (bottomText != null)
                    bottomText.LayoutTransform = scaleTransform;

                UpdateInteractionVisualScale(handleOverlay, node, topBottomScaleFactor);
            }

            // --- 8. RESIZING LOGIC ---
            bool isResizing = false;
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = new Point();
            double origX = 0, origY = 0, origW = 0, origH = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse el && el.Tag is ResizeDirection dir)
                {
                    isResizing = true;
                    currentDir = dir;
                    resizeStart = e.GetPosition(host.WorkflowCanvas);
                    origX = node.X;
                    origY = node.Y;
                    origW = border.ActualWidth;
                    origH = border.ActualHeight;
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var pos = e.GetPosition(host.WorkflowCanvas);
                var dx = pos.X - resizeStart.X;
                var dy = pos.Y - resizeStart.Y;
                double newX = origX, newY = origY, newW = origW, newH = origH;

                var minW = border.MinWidth;
                var minH = border.MinHeight;

                switch (currentDir)
                {
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH + dy);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(minW, origW + dx);
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH + dy);
                        newX = origX + (origW - newW);
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(minW, origW - dx);
                        newH = Math.Max(minH, origH - dy);
                        newX = origX + (origW - newW);
                        newY = origY + (origH - newH);
                        break;
                }

                node.Width = newW;
                node.Height = newH;
                node.X = newX;
                node.Y = newY;
                border.Width = newW;
                border.Height = newH;
                RefreshDynamicUiScale();

                if (host.WorkflowCanvas != null)
                {
                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);
                }
                e.Handled = true;
            };

            border.PreviewMouseUp += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    border.ReleaseMouseCapture();
                    e.Handled = true;
                }
            };

            // --- 9. CANVAS SCROLL & PAN POSITION SYNC HACK (for native HwndHost child window) ---
            bool isDisposed = false;
            EventHandler? scaleChangedHandler = null;
            EventHandler? translateChangedHandler = null;
            EventHandler? renderingHandler = null;

            double lastZoom = -1;
            double lastTranslateX = double.NaN;
            double lastTranslateY = double.NaN;

            void SyncSciterPosition()
            {
                try
                {
                    if (isDisposed) return;
                    if (sciterControl.ActualWidth <= 0 || sciterControl.ActualHeight <= 0) return;

                    var transform = host.TranslateTransform;
                    bool changed = false;
                    if (transform != null)
                    {
                        if (transform.X != lastTranslateX || transform.Y != lastTranslateY)
                        {
                            lastTranslateX = transform.X;
                            lastTranslateY = transform.Y;
                            changed = true;
                        }
                    }

                    if (host.ZoomLevel != lastZoom)
                    {
                        lastZoom = host.ZoomLevel;
                        sciterControl.SetZoom(lastZoom);
                        changed = true;
                    }

                    if (changed || host.DraggedNode == node || isResizing)
                    {
                        sciterControl.InvalidateMeasure();
                        sciterControl.InvalidateArrange();
                        sciterControl.InvalidateVisual();
                        if (sciterControl.Parent is FrameworkElement p)
                        {
                            p.InvalidateArrange();
                            p.InvalidateVisual();
                        }
                    }
                }
                catch { }
            }

            scaleChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (NodeChrome.IsZooming)
                {
                    if (sciterControl.Visibility != Visibility.Collapsed)
                        sciterControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (sciterControl.Visibility != Visibility.Visible)
                        sciterControl.Visibility = Visibility.Visible;
                    SyncSciterPosition();
                }
            };
            var scaleDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                ScaleTransform.ScaleXProperty, typeof(ScaleTransform));
            scaleDescriptor?.AddValueChanged(host.ScaleTransform, scaleChangedHandler);

            translateChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (host.IsPanning)
                {
                    if (sciterControl.Visibility != Visibility.Collapsed)
                        sciterControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (sciterControl.Visibility != Visibility.Visible)
                        sciterControl.Visibility = Visibility.Visible;
                    SyncSciterPosition();
                }
            };
            var translateXDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                TranslateTransform.XProperty, typeof(TranslateTransform));
            var translateYDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                TranslateTransform.YProperty, typeof(TranslateTransform));
            translateXDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);
            translateYDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);

            renderingHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (host.IsPanning || host.DraggedNode == node || NodeChrome.IsZooming)
                {
                    if (sciterControl.Visibility != Visibility.Collapsed)
                        sciterControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (sciterControl.Visibility != Visibility.Visible)
                    {
                        sciterControl.Visibility = Visibility.Visible;
                        SyncSciterPosition();
                    }
                }
            };
            System.Windows.Media.CompositionTarget.Rendering += renderingHandler;

            border.Unloaded += (s, e) =>
            {
                isDisposed = true;
                try
                {
                    System.Windows.Media.CompositionTarget.Rendering -= renderingHandler;
                }
                catch { }
                try
                {
                    scaleDescriptor?.RemoveValueChanged(host.ScaleTransform, scaleChangedHandler);
                }
                catch { }
                try
                {
                    translateXDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                    translateYDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                }
                catch { }
            };

            // --- 10. PROPERTY SYNCS AND PROPERTY CHANGED HANDLERS ---
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DynamicUiNode.HtmlCode) ||
                    e.PropertyName == nameof(DynamicUiNode.CssCode) ||
                    e.PropertyName == nameof(DynamicUiNode.JsCode))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        sciterControl.UpdateContent();
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Width))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!isResizing)
                            border.Width = Math.Max(600, node.Width);
                        RefreshDynamicUiScale();
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Height))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!isResizing)
                            border.Height = Math.Max(600, node.Height);
                        RefreshDynamicUiScale();
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Title))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        topBarText.Text = node.Title ?? "Dynamic UI Form";
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.PendingReadDom) && node.PendingReadDom)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            sciterControl.UpdateOutputsFromDom(node.ParamsCode, node.ResolvedOutputs);
                        }
                        finally
                        {
                            node.PendingReadDom = false;
                        }
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.InputMappings))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        sciterControl.RestartAutoRefreshTimers();
                        sciterControl.UpdateContent();
                    });
                }
            };

            border.Loaded += (s, e) =>
            {
                RefreshDynamicUiScale();
            };

            // --- 11. FLUENT API INITIALIZATION ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()      
                .WithHoverBehavior()        
                .WithKeyboardPorts()        
                .WithCleanup()             
                .WithVisibilitySync()      
                .WithCanvasIntegration()   
                .WithDialogSupport(ctx => new DynamicUiNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .Build();                  

            return border;
        }

        private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin)
        {
            var handle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Tag = direction,
                Cursor = GetCursorForResizeDirection(direction),
                IsHitTestVisible = true,
                CacheMode = null
            };
            GpuOptimizationHelper.ApplyToShape(handle);
            grid.Children.Add(handle);
        }

        private static void UpdateInteractionVisualScale(Grid handleOverlay, WorkflowNode node, double rawScale)
        {
            var visualScale = Math.Max(1.0, Math.Min(2.8, rawScale * 1.2));

            if (handleOverlay != null)
            {
                foreach (var child in handleOverlay.Children)
                {
                    if (child is Ellipse handle && handle.Tag is ResizeDirection)
                    {
                        handle.RenderTransformOrigin = new Point(0.5, 0.5);
                        handle.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }

            if (node?.Ports != null)
            {
                foreach (var p in node.Ports)
                {
                    if (p?.PortUI is FrameworkElement portUi)
                    {
                        portUi.RenderTransformOrigin = new Point(0.5, 0.5);
                        portUi.RenderTransform = new ScaleTransform(visualScale, visualScale);
                    }
                }
            }
        }

        private static Cursor GetCursorForResizeDirection(ResizeDirection direction)
        {
            return direction switch
            {
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                _ => Cursors.Arrow
            };
        }
    }
}
