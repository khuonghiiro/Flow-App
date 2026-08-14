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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

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
            AllowsTransparency = false;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e293b"));
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

            IWorkflowEditorHost? host = null;
            if (System.Windows.Application.Current != null)
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is IWorkflowEditorHost weHost)
                    {
                        host = weHost;
                        break;
                    }
                }
            }

            _sciterControl = new SciterEmbeddedControl(node, host);
            
            _sciterControl.FormSubmitted += (s, outputs) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[ShowInputMsg] Form submitted!");
                    _isShownAndActive = false;
                    RestorePreviousForegroundWindow();
                    Hide();
                    _tcs?.TrySetResult(true);
                }));
            };

            _sciterControl.FormCancelled += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[ShowInputMsg] Form cancelled!");
                    _isShownAndActive = false;
                    RestorePreviousForegroundWindow();
                    Hide();
                    _tcs?.TrySetResult(false);
                }));
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

            Deactivated += (s, e) =>
            {
                if (!_isShownAndActive) return;
                if ((DateTime.UtcNow - _showTime).TotalMilliseconds < 300) return;

                if (ShouldHideOnDeactivation())
                {
                    System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Deactivated event -> Hiding window. Id={_node?.Id}");
                    _isShownAndActive = false;
                    RestorePreviousForegroundWindow();
                    Hide();
                    _sciterControl.SubmitFormFromDom();
                    _tcs?.TrySetResult(true);
                }
            };

            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible == false)
                {
                    RestorePreviousForegroundWindow();
                }
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndSourceHook);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private bool IsCursorInsidePopupWindow()
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero && GetWindowRect(helper.Handle, out RECT rect))
                {
                    GetCursorPos(out var pt);
                    return pt.X >= rect.Left - 5 && pt.X <= rect.Right + 5 &&
                           pt.Y >= rect.Top - 5 && pt.Y <= rect.Bottom + 5;
                }
            }
            catch { }
            return false;
        }

        private bool ShouldHideOnDeactivation()
        {
            try
            {
                if (IsCursorInsidePopupWindow())
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return !IsCursorInsidePopupWindow();
            }
        }

        private const int WM_ACTIVATE = 0x0006;
        private const int WM_NCACTIVATE = 0x0086;
        private const int WM_KILLFOCUS = 0x0008;
        private const int WA_INACTIVE = 0;

        private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_ACTIVATE || msg == WM_NCACTIVATE || msg == WM_KILLFOCUS)
            {
                bool isDeactivating = false;
                if (msg == WM_ACTIVATE)
                {
                    var activeState = wParam.ToInt32() & 0xFFFF;
                    if (activeState == WA_INACTIVE) isDeactivating = true;
                }
                else if (msg == WM_NCACTIVATE)
                {
                    if (wParam == IntPtr.Zero) isDeactivating = true;
                }
                else if (msg == WM_KILLFOCUS)
                {
                    isDeactivating = true;
                }

                if (isDeactivating && _isShownAndActive)
                {
                    if ((DateTime.UtcNow - _showTime).TotalMilliseconds < 300)
                    {
                        return IntPtr.Zero;
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_isShownAndActive) return;

                        if (ShouldHideOnDeactivation())
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Deactivation message (0x{msg:X4}) -> Hiding window. Id={_node?.Id}");
                            _isShownAndActive = false;
                            RestorePreviousForegroundWindow();
                            Hide();
                            _sciterControl.SubmitFormFromDom();
                            _tcs?.TrySetResult(true);
                        }
                    }));
                }
            }
            return IntPtr.Zero;
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

        private void SubscribeOutsideClicks(MouseButtonEventHandler handler)
        {
            try
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win != this)
                    {
                        win.PreviewMouseDown -= handler;
                        win.PreviewMouseDown += handler;
                    }
                }
            }
            catch { }
        }

        private void UnsubscribeOutsideClicks(MouseButtonEventHandler handler)
        {
            try
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win != this)
                    {
                        win.PreviewMouseDown -= handler;
                    }
                }
            }
            catch { }
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_NCLBUTTONDOWN = 0x00A1;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private LowLevelMouseProc? _mouseProc;

        private void InstallMouseHook()
        {
            try
            {
                if (_mouseHookHandle != IntPtr.Zero) return;
                _mouseProc = HookCallback;
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    IntPtr hMod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
                    _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] InstallMouseHook Error: {ex.Message}");
            }
        }

        private void UninstallMouseHook()
        {
            try
            {
                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                    _mouseProc = null;
                }
            }
            catch { }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isShownAndActive)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_NCLBUTTONDOWN)
                {
                    if ((DateTime.UtcNow - _showTime).TotalMilliseconds >= 300)
                    {
                        try
                        {
                            var hookStruct = System.Runtime.InteropServices.Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            int clickX = hookStruct.pt.X;
                            int clickY = hookStruct.pt.Y;

                            var helper = new System.Windows.Interop.WindowInteropHelper(this);
                            if (helper.Handle != IntPtr.Zero && GetWindowRect(helper.Handle, out RECT rect))
                            {
                                bool isInside = clickX >= rect.Left - 5 && clickX <= rect.Right + 5 &&
                                                clickY >= rect.Top - 5 && clickY <= rect.Bottom + 5;
                                if (!isInside)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] Global mouse hook -> Click outside at ({clickX},{clickY}). Hiding window.");
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (!_isShownAndActive) return;
                                        _isShownAndActive = false;
                                        UninstallMouseHook();
                                        RestorePreviousForegroundWindow();
                                        Hide();
                                        _sciterControl.SubmitFormFromDom();
                                        _tcs?.TrySetResult(true);
                                    }));
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        public async Task<bool> PrepareAndShowAsync(string htmlContent)
        {
            _tcs = new TaskCompletionSource<bool>();
            _isShownAndActive = false;

            MouseButtonEventHandler? outsideClickHandler = null;

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

                outsideClickHandler = (s, e) =>
                {
                    if (!_isShownAndActive) return;
                    if ((DateTime.UtcNow - _showTime).TotalMilliseconds < 300) return;

                    if (!IsCursorInsidePopupWindow())
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] PreviewMouseDown -> Click outside detected. Hiding window. Id={_node?.Id}");
                        _isShownAndActive = false;
                        UninstallMouseHook();
                        UnsubscribeOutsideClicks(outsideClickHandler);
                        RestorePreviousForegroundWindow();
                        Hide();
                        _sciterControl.SubmitFormFromDom();
                        _tcs?.TrySetResult(true);
                    }
                };

                SubscribeOutsideClicks(outsideClickHandler);
                InstallMouseHook();

                Show();
                Activate();

                _sciterControl.LoadHtml(htmlContent);
                _sciterControl.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowInputMsg] PrepareAndShowAsync Error: {ex}");
                UninstallMouseHook();
                _tcs.TrySetResult(false);
            }

            var result = await _tcs.Task;

            UninstallMouseHook();
            if (outsideClickHandler != null)
            {
                UnsubscribeOutsideClicks(outsideClickHandler);
            }

            return result;
        }
    }
}
