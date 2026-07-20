using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using EmptyFlow.SciterAPI;
using FlowMy.Helpers;
using FlowMy.Services.Workflow.NodeExecutors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls
{
    public static class ShowInputMsgNodeControl
    {
        private static readonly Dictionary<ShowInputMsgNode, Border> _activePreviews = new();
        private static readonly Dictionary<ShowInputMsgNode, SciterEmbeddedControl> _activeSciters = new();

        public static Border CreateBorder(ShowInputMsgNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            bool isDisposed = false;

            // ─── 1. ICON ───
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri),
                "user-message regular",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey)
            };

            // ─── 2. GRID ───
            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };
            grid.Children.Add(iconSvg);

            // ─── Eye Toggle Button ───
            var previewToggleButton = new ToggleButton
            {
                Width = 22,
                Height = 22,
                Cursor = Cursors.Hand,
                IsChecked = node.IsPreviewVisible,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -6, -6, 0), // Floating overlapping style
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)), // Slate 100 default
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 2,
                    BlurRadius = 4,
                    Opacity = 0.3
                }
            };

            var toggleIcon = new SvgViewboxEx
            {
                Width = 11, Height = 11
            };

            // Dynamically bind toggleIcon's Fill to ToggleButton's Foreground
            var fillBinding = new System.Windows.Data.Binding
            {
                Path = new PropertyPath(Control.ForegroundProperty),
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ToggleButton), 1)
            };
            toggleIcon.SetBinding(SvgViewboxEx.FillProperty, fillBinding);

            void UpdateToggleIconSource(bool isChecked)
            {
                var iconKey = isChecked ? "eye-slash regular" : "eye regular";
                var iconUriTemp = iconConverter.Convert(null, typeof(Uri), iconKey, System.Globalization.CultureInfo.CurrentCulture) as Uri;
                toggleIcon.Source = iconUriTemp;
            }

            UpdateToggleIconSource(node.IsPreviewVisible);
            previewToggleButton.Content = toggleIcon;

            // Custom template for circular ToggleButton with hover and active states
            var template = new ControlTemplate(typeof(ToggleButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "ButtonBorder";
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(220, 30, 41, 59))); // Slate 800 with transparency
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105))); // Slate 600
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11)); // Circular shape
            
            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenterFactory);
            
            template.VisualTree = borderFactory;

            // Hover trigger (Slate 700)
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85)), "ButtonBorder"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184)), "ButtonBorder"));
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(hoverTrigger);

            // Checked (Active) trigger (Blue 600)
            var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(37, 99, 235)), "ButtonBorder"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(96, 165, 250)), "ButtonBorder"));
            checkedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(checkedTrigger);

            // Checked + Hover trigger (Blue 500)
            var checkedHoverTrigger = new MultiTrigger();
            checkedHoverTrigger.Conditions.Add(new Condition { Property = ToggleButton.IsCheckedProperty, Value = true });
            checkedHoverTrigger.Conditions.Add(new Condition { Property = UIElement.IsMouseOverProperty, Value = true });
            checkedHoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(59, 130, 246)), "ButtonBorder"));
            checkedHoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(147, 197, 253)), "ButtonBorder"));
            checkedHoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(checkedHoverTrigger);

            // Pressed trigger (Blue 700)
            var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(29, 78, 216)), "ButtonBorder"));
            pressedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(59, 130, 246)), "ButtonBorder"));
            pressedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(pressedTrigger);

            previewToggleButton.Template = template;
            grid.Children.Add(previewToggleButton);

            // ─── 3. TITLE TEXTBLOCK ───
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Nhập dữ liệu",
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
            node.TitleTextBlockUI = titleTextBlock; // ⚠️ BẮT BUỘC

            // ─── 4. BORDER ───
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
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node // ⚠️ BẮT BUỘC
            };

            // ─── 5. CUSTOM PROPERTY HANDLERS ───
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                    toggleIcon.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                },
                [nameof(ShowInputMsgNode.IsPreviewVisible)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    previewToggleButton.IsChecked = n.IsPreviewVisible;
                    UpdateToggleIconSource(n.IsPreviewVisible);
                    SyncPreviewState(n, border, host);
                },
                [nameof(ShowInputMsgNode.Width)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    if (_activePreviews.TryGetValue(n, out var prevBorder))
                    {
                        prevBorder.Width = n.Width;
                        UpdatePreviewPosition(n, border, host);
                    }
                },
                [nameof(ShowInputMsgNode.Height)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    if (_activePreviews.TryGetValue(n, out var prevBorder))
                    {
                        prevBorder.Height = n.Height;
                        UpdatePreviewPosition(n, border, host);
                    }
                },
                [nameof(ShowInputMsgNode.HtmlCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    RefreshPreviewHtml(n);
                },
                [nameof(ShowInputMsgNode.CssCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    RefreshPreviewHtml(n);
                },
                [nameof(ShowInputMsgNode.JsCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    RefreshPreviewHtml(n);
                },
                [nameof(ShowInputMsgNode.ParamsCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    RefreshPreviewHtml(n);
                }
            };

            // ─── 6. EVENT BINDINGS ───
            previewToggleButton.Checked += (s, e) =>
            {
                node.IsPreviewVisible = true;
            };

            previewToggleButton.Unchecked += (s, e) =>
            {
                node.IsPreviewVisible = false;
            };

            // ─── Event handlers for pan/zoom/drag position updates ───
            EventHandler? scaleChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (_activeSciters.TryGetValue(node, out var sciterControl))
                    {
                        sciterControl.SetZoom(host.ZoomLevel);
                        sciterControl.ForceMoveWindow();
                    }
                    UpdatePreviewPosition(node, border, host);
                }
            };

            EventHandler? translateChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (_activeSciters.TryGetValue(node, out var sciterControl))
                    {
                        sciterControl.ForceMoveWindow();
                    }
                    UpdatePreviewPosition(node, border, host);
                }
            };

            EventHandler? positionChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (_activeSciters.TryGetValue(node, out var sciterControl))
                    {
                        sciterControl.ForceMoveWindow();
                    }
                    UpdatePreviewPosition(node, border, host);
                }
            };

            EventHandler? renderingHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (_activeSciters.TryGetValue(node, out var sciterControl))
                    {
                        sciterControl.ForceMoveWindow();
                    }
                }
            };

            border.SizeChanged += (s, e) => UpdatePreviewPosition(node, border, host);

            border.Loaded += (s, e) =>
            {
                isDisposed = false;
                var scaleDescriptor = DependencyPropertyDescriptor.FromProperty(ScaleTransform.ScaleXProperty, typeof(ScaleTransform));
                scaleDescriptor?.AddValueChanged(host.ScaleTransform, scaleChangedHandler);

                var translateXDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.XProperty, typeof(TranslateTransform));
                var translateYDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.YProperty, typeof(TranslateTransform));
                translateXDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);
                translateYDescriptor?.AddValueChanged(host.TranslateTransform, translateChangedHandler);

                var leftDescriptor = DependencyPropertyDescriptor.FromProperty(Canvas.LeftProperty, typeof(Border));
                var topDescriptor = DependencyPropertyDescriptor.FromProperty(Canvas.TopProperty, typeof(Border));
                leftDescriptor?.AddValueChanged(border, positionChangedHandler);
                topDescriptor?.AddValueChanged(border, positionChangedHandler);

                System.Windows.Media.CompositionTarget.Rendering += renderingHandler;

                SyncPreviewState(node, border, host);
            };

            border.Unloaded += (s, e) =>
            {
                isDisposed = true;

                var scaleDescriptor = DependencyPropertyDescriptor.FromProperty(ScaleTransform.ScaleXProperty, typeof(ScaleTransform));
                scaleDescriptor?.RemoveValueChanged(host.ScaleTransform, scaleChangedHandler);

                var translateXDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.XProperty, typeof(TranslateTransform));
                var translateYDescriptor = DependencyPropertyDescriptor.FromProperty(TranslateTransform.YProperty, typeof(TranslateTransform));
                translateXDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);
                translateYDescriptor?.RemoveValueChanged(host.TranslateTransform, translateChangedHandler);

                var leftDescriptor = DependencyPropertyDescriptor.FromProperty(Canvas.LeftProperty, typeof(Border));
                var topDescriptor = DependencyPropertyDescriptor.FromProperty(Canvas.TopProperty, typeof(Border));
                leftDescriptor?.RemoveValueChanged(border, positionChangedHandler);
                topDescriptor?.RemoveValueChanged(border, positionChangedHandler);

                try
                {
                    System.Windows.Media.CompositionTarget.Rendering -= renderingHandler;
                }
                catch { }

                if (_activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (host.WorkflowCanvas?.Children.Contains(previewBorder) == true)
                    {
                        host.WorkflowCanvas.Children.Remove(previewBorder);
                    }
                    _activePreviews.Remove(node);
                }

                if (_activeSciters.TryGetValue(node, out var sciterControl))
                {
                    try
                    {
                        sciterControl.Dispose();
                    }
                    catch { }
                    _activeSciters.Remove(node);
                }
            };

            // ─── 7. FLUENT API ───
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new ShowInputMsgNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

        private static void SyncPreviewState(ShowInputMsgNode node, Border border, IWorkflowEditorHost host)
        {
            var isHeadless = false;
            if (host is WorkflowEditorWindow we)
            {
                isHeadless = we.IsHeadlessMode;
            }
            if (isHeadless) return;

            if (host.WorkflowCanvas == null) return;

            if (node.IsPreviewVisible)
            {
                if (!_activePreviews.TryGetValue(node, out var previewBorder))
                {
                    previewBorder = CreatePreviewBorder(node, host);
                    _activePreviews[node] = previewBorder;
                }

                if (!host.WorkflowCanvas.Children.Contains(previewBorder))
                {
                    host.WorkflowCanvas.Children.Add(previewBorder);
                }

                UpdatePreviewPosition(node, border, host);
                RefreshPreviewHtml(node);
            }
            else
            {
                if (_activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (host.WorkflowCanvas.Children.Contains(previewBorder))
                    {
                        host.WorkflowCanvas.Children.Remove(previewBorder);
                    }
                    _activePreviews.Remove(node);
                }

                if (_activeSciters.TryGetValue(node, out var sciterControl))
                {
                    try
                    {
                        sciterControl.Dispose();
                    }
                    catch { }
                    _activeSciters.Remove(node);
                }
            }
        }

        private static void UpdatePreviewPosition(ShowInputMsgNode node, Border border, IWorkflowEditorHost host)
        {
            if (!_activePreviews.TryGetValue(node, out var previewBorder)) return;

            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);

            if (double.IsNaN(left) || double.IsInfinity(left)) left = node.X;
            if (double.IsNaN(top) || double.IsInfinity(top)) top = node.Y;

            // Render side-by-side: 12px to the right of the node
            var previewLeft = left + border.ActualWidth + 12;
            var previewTop = top;

            // Avoid infinite layout loop by checking if values have changed before setting them
            var curLeft = Canvas.GetLeft(previewBorder);
            var curTop = Canvas.GetTop(previewBorder);

            if (double.IsNaN(curLeft) || Math.Abs(curLeft - previewLeft) > 0.01)
            {
                Canvas.SetLeft(previewBorder, previewLeft);
            }
            if (double.IsNaN(curTop) || Math.Abs(curTop - previewTop) > 0.01)
            {
                Canvas.SetTop(previewBorder, previewTop);
            }

            if (Panel.GetZIndex(previewBorder) != 1000)
            {
                Panel.SetZIndex(previewBorder, 1000); // Overlay level
            }
        }

        private static Border CreatePreviewBorder(ShowInputMsgNode node, IWorkflowEditorHost host)
        {
            var previewBorder = new Border
            {
                Width = node.Width,
                Height = node.Height,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // Slate 900
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)), // Slate 700
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 12,
                    Opacity = 0.4
                },
                IsHitTestVisible = true
            };

            GpuOptimizationHelper.ApplyToBorder(previewBorder);

            var grid = new Grid();
            GpuOptimizationHelper.ApplyToElement(grid);

            var sciterControl = new SciterEmbeddedControl(node, host);
            grid.Children.Add(sciterControl);
            previewBorder.Child = grid;

            _activeSciters[node] = sciterControl;

            return previewBorder;
        }

        private static void RefreshPreviewHtml(ShowInputMsgNode node)
        {
            if (!_activeSciters.TryGetValue(node, out var sciterControl)) return;
            sciterControl.UpdateContent();
        }
    }

    public class ShowInputMsgPopupWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private readonly SciterEmbeddedControl _sciterControl;
        private TaskCompletionSource<bool>? _tcs;
        private ShowInputMsgNode _node;
        private bool _isForceClosing = false;
        private bool _isShownAndActive = false;
        private DateTime _showTime = DateTime.MinValue;
        private IntPtr _previousForegroundHwnd = IntPtr.Zero;

        public ShowInputMsgPopupWindow(ShowInputMsgNode node)
        {
            _node = node;

            Title = "Nhập Dữ Liệu";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Manual;
            Width = node.Width;
            Height = node.Height;

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e293b")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true
            };

            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 3,
                Opacity = 0.5
            };
            border.Effect = shadow;

            _sciterControl = new SciterEmbeddedControl(node);
            
            _sciterControl.FormSubmitted += (s, outputs) =>
            {
                System.Diagnostics.Debug.WriteLine("[ShowInputMsg] Form submitted!");
                _tcs?.TrySetResult(true);
                ForceClose();
            };

            _sciterControl.FormCancelled += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ShowInputMsg] Form cancelled!");
                _tcs?.TrySetResult(false);
                ForceClose();
            };

            border.Child = _sciterControl;
            Content = border;

            Activated += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Window activated. Id={_node?.Id}");
                _isShownAndActive = true;
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Escape pressed. Hiding window. Id={_node?.Id}");
                    _isShownAndActive = false;
                    RestorePreviousForegroundWindow();
                    Hide();
                    _tcs?.TrySetResult(false);
                }
            };

            Deactivated += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Window deactivated (isShownAndActive={_isShownAndActive}). Id={_node?.Id}");
                if (!_isShownAndActive) return;
                
                if ((DateTime.UtcNow - _showTime).TotalMilliseconds < 500)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Window deactivated too quickly ({(DateTime.UtcNow - _showTime).TotalMilliseconds}ms). Ignoring. Id={_node?.Id}");
                    return;
                }
                
                _isShownAndActive = false;
                RestorePreviousForegroundWindow();
                Hide();
                _tcs?.TrySetResult(false);
            };

            Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Window closed. Id={_node?.Id}");
                _isShownAndActive = false;
                _tcs?.TrySetResult(false);
                RestorePreviousForegroundWindow();
                try
                {
                    _sciterControl.Dispose();
                }
                catch { }
            };

            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible == false)
                {
                    RestorePreviousForegroundWindow();
                }
            };
        }

        public void RestorePreviousForegroundWindow()
        {
            try
            {
                if (_previousForegroundHwnd != IntPtr.Zero)
                {
                    var myHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (_previousForegroundHwnd != myHwnd)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Restoring foreground window to handle: {_previousForegroundHwnd}");
                        FlowMy.Helpers.WindowHelper.SetForegroundWindow(_previousForegroundHwnd);
                    }
                    _previousForegroundHwnd = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Error restoring foreground window: {ex.Message}");
            }
        }

        public void UpdateNode(ShowInputMsgNode node)
        {
            _node = node;
        }

        public void ForceClose()
        {
            System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] ForceClose called. Id={_node?.Id}");
            _isForceClosing = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isForceClosing)
            {
                base.OnClosing(e);
                return;
            }
            e.Cancel = true;
            System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Closing intercepted. Hiding window. Id={_node?.Id}");
            _isShownAndActive = false;
            RestorePreviousForegroundWindow();
            Hide();
            _tcs?.TrySetResult(false);
        }

        public async Task<bool> PrepareAndShowAsync(string htmlContent)
        {
            _tcs = new TaskCompletionSource<bool>();
            _isShownAndActive = false;

            try
            {
                _previousForegroundHwnd = GetForegroundWindow();

                Width = _node.Width;
                Height = _node.Height;

                var mousePos = ShowInputMsgNodeExecutor.GetMousePositionWpf();
                Left = mousePos.x;
                Top = mousePos.y;

                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                if (Left + Width > screenWidth)
                    Left = screenWidth - Width;
                if (Top + Height > screenHeight)
                    Top = screenHeight - Height;
                if (Left < 0) Left = 0;
                if (Top < 0) Top = 0;

                _showTime = DateTime.UtcNow;
                _isShownAndActive = true;

                Show();
                Activate();

                var sciterHelperScript = @"
<script type=""module"">
  function hostSubmit() {
    var data = {};
    var paramsText = `";
                
                sciterHelperScript += (_node.ParamsCode ?? "").Replace("`", "\\`").Replace("$", "\\$");
                sciterHelperScript += @"`;
    var lines = paramsText.split('\n');
    for (var i = 0; i < lines.length; i++) {
        var line = lines[i].trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) continue;
        var parts = line.includes(':') ? line.split(':') : line.split('=');
        if (parts.length < 2) continue;
        var key = parts[0].trim();
        var selector = parts[1].trim();
        try {
            var el = document.querySelector(selector);
            if (el) {
                if (typeof el.value !== 'undefined') {
                    data[key] = el.value;
                } else if (el.textContent) {
                    data[key] = el.textContent;
                }
            }
        } catch(e) {}
    }
    if (window.Window && window.Window.this) {
        window.Window.this.xcall('submitForm', data);
    } else {
        Window.this.xcall('submitForm', data);
    }
  }
  window.hostSubmit = hostSubmit;
</script>";

                var html = htmlContent;
                if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("</body>", sciterHelperScript + "\n</body>", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    html += sciterHelperScript;
                }

                _sciterControl.LoadHtml(html);
                _sciterControl.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] PrepareAndShowAsync Error: {ex}");
                _tcs.TrySetResult(false);
            }

            return await _tcs.Task;
        }
    }
}
