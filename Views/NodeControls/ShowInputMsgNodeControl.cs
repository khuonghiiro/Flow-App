using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class ShowInputMsgNodeControl
    {
        private static readonly Dictionary<ShowInputMsgNode, Border> _activePreviews = new();
        private static readonly Dictionary<ShowInputMsgNode, WebView2> _activeWebViews = new();
        private static readonly System.Threading.SemaphoreSlim _webViewInitGate = new(1, 1);

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
                    _ = RefreshPreviewHtmlAsync(n, host);
                },
                [nameof(ShowInputMsgNode.CssCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    _ = RefreshPreviewHtmlAsync(n, host);
                },
                [nameof(ShowInputMsgNode.JsCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    _ = RefreshPreviewHtmlAsync(n, host);
                },
                [nameof(ShowInputMsgNode.ParamsCode)] = ctx =>
                {
                    var n = (ShowInputMsgNode)ctx.Node;
                    _ = RefreshPreviewHtmlAsync(n, host);
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
                    if (previewBorder.Visibility != Visibility.Visible)
                        previewBorder.Visibility = Visibility.Visible;

                    UpdatePreviewPosition(node, border, host);
                    if (_activeWebViews.TryGetValue(node, out var webView))
                    {
                        UpdateWebViewZoomForCanvasZoom(webView, host);
                    }
                }
            };

            EventHandler? translateChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (previewBorder.Visibility != Visibility.Visible)
                        previewBorder.Visibility = Visibility.Visible;

                    UpdatePreviewPosition(node, border, host);
                }
            };

            EventHandler? positionChangedHandler = (_, _) =>
            {
                if (isDisposed) return;
                if (node.IsPreviewVisible && _activePreviews.TryGetValue(node, out var previewBorder))
                {
                    UpdatePreviewPosition(node, border, host);
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

                if (_activePreviews.TryGetValue(node, out var previewBorder))
                {
                    if (host.WorkflowCanvas?.Children.Contains(previewBorder) == true)
                    {
                        host.WorkflowCanvas.Children.Remove(previewBorder);
                    }
                    _activePreviews.Remove(node);
                }

                if (_activeWebViews.TryGetValue(node, out var webView))
                {
                    try
                    {
                        webView.Dispose();
                    }
                    catch { }
                    _activeWebViews.Remove(node);
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
                _ = RefreshPreviewHtmlAsync(node, host);
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

                if (_activeWebViews.TryGetValue(node, out var webView))
                {
                    try
                    {
                        webView.Dispose();
                    }
                    catch { }
                    _activeWebViews.Remove(node);
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

            var grid = new Grid();
            var webView = new WebView2();

            grid.Children.Add(webView);
            previewBorder.Child = grid;

            // Load and navigate
            _ = InitializePreviewWebViewAsync(node, webView, host);

            return previewBorder;
        }

        private static async Task InitializePreviewWebViewAsync(ShowInputMsgNode node, WebView2 webView, IWorkflowEditorHost host)
        {
            await _webViewInitGate.WaitAsync();
            try
            {
                var env = await FlowMy.Services.Workflow.WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(env);

                _activeWebViews[node] = webView;

                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    UpdateWebViewZoomForCanvasZoom(webView, host);
                };

                await RefreshPreviewHtmlAsync(node, host);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize WebView2 preview: {ex}");
            }
            finally
            {
                _webViewInitGate.Release();
            }
        }

        private static async Task RefreshPreviewHtmlAsync(ShowInputMsgNode node, IWorkflowEditorHost host)
        {
            if (!_activeWebViews.TryGetValue(node, out var webView)) return;
            try
            {
                var core = webView.CoreWebView2;
                if (core == null) return;

                var html = BuildHtmlContent(node, host);
                await webView.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        webView.CoreWebView2.NavigateToString(html);
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reloading preview HTML: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RefreshPreviewHtml: {ex}");
            }
        }

        private static string ResolveSingleInputValue(ShowInputMsgNode node, CodeInputMapping mapping, IWorkflowEditorHost host)
        {
            if (host?.ViewModel == null) return string.Empty;
            var allNodes = host.ViewModel.Nodes;
            var connections = host.ViewModel.Connections;
            WorkflowNode? sourceNode = null;
            if (!string.IsNullOrWhiteSpace(mapping.SourceNodeId))
            {
                sourceNode = allNodes?.FirstOrDefault(n =>
                    string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (sourceNode == null && connections != null)
                {
                    var conn = connections.FirstOrDefault(c =>
                        c.ToNode == node && c.FromNode != null &&
                        string.Equals(c.FromNode.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    sourceNode = conn?.FromNode;
                }
            }
            if (sourceNode == null) return string.Empty;
            var key = string.IsNullOrWhiteSpace(mapping.SourceOutputKey) ? null : mapping.SourceOutputKey.Trim();
            if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs?.Count > 0)
                key = sourceNode.DynamicOutputs[0].Key ?? "output";
            var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
            if (string.Equals(value?.Trim(), "—", StringComparison.OrdinalIgnoreCase)) value = string.Empty;
            return value ?? string.Empty;
        }

        private static System.Collections.Generic.Dictionary<string, string> ResolveInputValues(ShowInputMsgNode node, IWorkflowEditorHost host)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>();
            if (host?.ViewModel == null) return result;
            var mappings = node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
            foreach (var m in mappings)
            {
                var value = ResolveSingleInputValue(node, m, host);
                var varName = m.EffectiveInputKey;
                if (string.IsNullOrWhiteSpace(varName)) varName = "input";
                result[varName] = value ?? string.Empty;
            }
            return result;
        }

        private static string ReplaceVariables(string text, System.Collections.Generic.Dictionary<string, string> variableValues)
        {
            if (string.IsNullOrEmpty(text) || variableValues.Count == 0) return text;
            var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
            return regex.Replace(text, match =>
            {
                var variableName = match.Groups[1].Value.Trim();
                if (variableValues.TryGetValue(variableName, out var value) && value != null)
                {
                    return value;
                }
                return match.Value;
            });
        }

        private static string BuildHtmlContent(ShowInputMsgNode node, IWorkflowEditorHost host)
        {
            var html = node.HtmlCode ?? "";
            var css = node.CssCode ?? "";
            var js = node.JsCode ?? "";

            var inputValues = ResolveInputValues(node, host);
            html = ReplaceVariables(html, inputValues);
            css = ReplaceVariables(css, inputValues);
            js = ReplaceVariables(js, inputValues);

            if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            {
                html = html.Replace("<html>", "<html>\n<head>\n    <meta charset=\"UTF-8\">\n    <title>Nhập Dữ Liệu</title>\n</head>", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(css))
            {
                var cssTag = $"\n    <style>\n{css}\n    </style>";
                if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                }
                else if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("<head>", "<head>" + cssTag, StringComparison.OrdinalIgnoreCase);
                }
            }

            var helperScript = @"
<script>
  function hostSubmit() {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ type: 'submit' });
    }
  }
  window.hostSubmit = hostSubmit;
</script>";

            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                html = html.Replace("</body>", helperScript + "\n</body>", StringComparison.OrdinalIgnoreCase);
            else
                html += helperScript;

            if (!string.IsNullOrWhiteSpace(js))
            {
                var jsTag = $"\n    <script>\n{js}\n    </script>";
                if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                }
                else if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace("</head>", jsTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    html += jsTag;
                }
            }

            // ✅ Inject offline assets (CSS trước, JS sau)
            var enabledAssets = (node.OfflineAssets ?? new System.Collections.Generic.List<FlowMy.Models.HtmlOfflineAsset>())
                .Where(a => a.IsEnabled && !string.IsNullOrWhiteSpace(a.LocalFileName));

            foreach (var asset in enabledAssets)
            {
                var content = FlowMy.Services.Utils.HtmlOfflineAssetService.GetInlineContent(asset.LocalFileName);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var safeName = System.Security.SecurityElement.Escape(asset.Title ?? asset.LocalFileName);

                if (string.Equals(asset.AssetType, "css", StringComparison.OrdinalIgnoreCase))
                {
                    var cssTag = $"\n    <style>/* [offline] {safeName} */\n{content}\n    </style>";
                    if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</head>", cssTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    else
                        html = "<style>" + content + "</style>" + html;
                }
                else // js
                {
                    var jsTag = $"\n    <script>/* [offline] {safeName} */\n{content}\n    </script>";
                    if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</body>", jsTag + "\n</body>", StringComparison.OrdinalIgnoreCase);
                    else if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        html = html.Replace("</head>", jsTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    else
                        html += jsTag;
                }
            }

            return html;
        }

        private static void UpdateWebViewZoomForCanvasZoom(WebView2 webView, IWorkflowEditorHost host)
        {
            try
            {
                var core = webView.CoreWebView2;
                if (core == null) return;

                double canvasZoom = host.ZoomLevel;
                double webViewZoom = 1.0 / Math.Max(canvasZoom, 0.0001);

                var script = $@"
                    (function() {{
                        document.body.style.zoom = '{webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}';
                        if (!document.body.style.zoom) {{
                            document.body.style.transform = 'scale({webViewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})';
                            document.body.style.transformOrigin = 'top left';
                        }}
                    }})();
                ";
                core.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating preview zoom: {ex.Message}");
            }
        }
    }
}
