using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class EmbedApplicationNodeControl
    {
        public static Border CreateBorder(EmbedApplicationNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ─── 1. GRID với 2 trạng thái ───
            var grid = new Grid();

            // ─── ICON (hiển thị khi chưa có embedded window) ───
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri),
                "desktop-arrow-down light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey),
                Visibility = node.HasEmbeddedWindow ? Visibility.Collapsed : Visibility.Visible
            };
            grid.Children.Add(iconSvg);

            // ─── EMBEDDED WINDOW HOST (hiển thị khi đã chọn app) ───
            var embeddedHost = new EmbeddedWindowHost
            {
                RemoveDecoration = node.ShowBorder,
                Visibility = node.HasEmbeddedWindow ? Visibility.Visible : Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(embeddedHost);

            // Đợi host Loaded rồi mới embed (quan trọng!)
            bool embedAttempted = false;
            embeddedHost.Loaded += (s, e) =>
            {
                if (embedAttempted) return;
                embedAttempted = true;

                // Đợi thêm 1 frame để BuildWindowCore hoàn tất
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(100); // Đợi container window khởi tạo xong
                    
                    if (node.HasEmbeddedWindow && node.WindowHandle != IntPtr.Zero)
                    {
                        try
                        {
                            bool success = embeddedHost.EmbedWindow(node.WindowHandle);
                            Debug.WriteLine($"[EmbedApplicationNodeControl] Embed on Loaded: success={success}, handle={node.WindowHandle}");
                            
                            if (success)
                            {
                                Debug.WriteLine($"[EmbedApplicationNodeControl] ✅ Successfully embedded {node.ProcessName}");
                            }
                            else
                            {
                                Debug.WriteLine($"[EmbedApplicationNodeControl] ❌ Failed to embed window");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[EmbedApplicationNodeControl] ❌ Embed exception: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[EmbedApplicationNodeControl] Skip embed: HasEmbeddedWindow={node.HasEmbeddedWindow}, Handle={node.WindowHandle}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            };

            // ─── 2. Set kích thước grid ───
            if (node.HasEmbeddedWindow)
            {
                grid.Width = node.EmbeddedWidth;
                grid.Height = node.EmbeddedHeight;
                grid.MinWidth = 200;
                grid.MinHeight = 150;
            }
            else
            {
                grid.Width = 60;
                grid.Height = 60;
                grid.MinWidth = 60;
                grid.MinHeight = 60;
            }

            // ─── 3. TITLE TEXTBLOCK ───
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Embed Application",
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
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node // ⚠️ BẮT BUỘC
            };

            // ─── 5. CUSTOM PROPERTY HANDLERS ───
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                },
                [nameof(EmbedApplicationNode.ProcessName)] = ctx =>
                {
                    // Update title khi process thay đổi
                    if (!string.IsNullOrWhiteSpace(node.ProcessName))
                    {
                        node.Title = $"Embed: {node.ProcessName}";
                    }
                },
                [nameof(EmbedApplicationNode.IsActive)] = ctx =>
                {
                    // Visual feedback khi active state thay đổi
                    if (border.Effect is DropShadowEffect shadow)
                    {
                        shadow.Color = node.IsActive ? Colors.Cyan : Colors.Black;
                        shadow.Opacity = node.IsActive ? 0.8 : 0.5;
                    }
                },
                [nameof(EmbedApplicationNode.HasEmbeddedWindow)] = ctx =>
                {
                    Debug.WriteLine($"[EmbedApplicationNodeControl] HasEmbeddedWindow changed to: {node.HasEmbeddedWindow}");
                    
                    // Chuyển đổi giữa icon và embedded window
                    if (node.HasEmbeddedWindow)
                    {
                        Debug.WriteLine($"[EmbedApplicationNodeControl] Activating embed mode - WindowHandle: {node.WindowHandle}, Size: {node.EmbeddedWidth}x{node.EmbeddedHeight}");
                        
                        // Ẩn icon, hiện embedded window
                        iconSvg.Visibility = Visibility.Collapsed;
                        embeddedHost.Visibility = Visibility.Visible;

                        // Resize node để chứa embedded window
                        grid.Width = node.EmbeddedWidth;
                        grid.Height = node.EmbeddedHeight;
                        grid.MinWidth = 200;
                        grid.MinHeight = 150;
                        
                        // QUAN TRỌNG: Update Border size để node resize trên canvas
                        border.Width = node.EmbeddedWidth;
                        border.Height = node.EmbeddedHeight;
                        border.MinWidth = 200;
                        border.MinHeight = 150;

                        // Ẩn title khi có embedded window
                        if (titleTextBlock != null)
                        {
                            titleTextBlock.Visibility = Visibility.Collapsed;
                        }

                        // QUAN TRỌNG: Đợi layout update xong rồi mới embed
                        border.UpdateLayout(); // Force layout ngay
                        grid.UpdateLayout();
                        
                        // Update ports position sau khi resize
                        if (host.WorkflowCanvas != null)
                        {
                            foreach (var port in node.Ports)
                            {
                                host.UpdatePortsPositionOnSide(node, port.Position);
                            }
                        }
                        
                        // Đợi 1 dispatcher frame
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                        {
                            // Đợi thêm để chắc chắn BuildWindowCore đã chạy
                            await System.Threading.Tasks.Task.Delay(200);
                            
                            // Embed window nếu có handle
                            if (node.WindowHandle != IntPtr.Zero)
                            {
                                try
                                {
                                    Debug.WriteLine($"[EmbedApplicationNodeControl] Calling EmbedWindow with handle {node.WindowHandle}");
                                    bool success = embeddedHost.EmbedWindow(node.WindowHandle);
                                    Debug.WriteLine($"[EmbedApplicationNodeControl] Embed result: {success}");
                                    
                                    if (success)
                                    {
                                        Debug.WriteLine($"[EmbedApplicationNodeControl] ✅ Embedded successfully!");
                                        
                                        // Resize embedded window to fill host
                                        FlowMy.Helpers.WindowHostHelper.ResizeEmbeddedWindow(
                                            node.WindowHandle, 
                                            0, 0, 
                                            (int)node.EmbeddedWidth, 
                                            (int)node.EmbeddedHeight);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[EmbedApplicationNodeControl] ❌ Embed failed");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[EmbedApplicationNodeControl] ❌ Exception: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[EmbedApplicationNodeControl] ⚠️ WindowHandle is Zero!");
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);

                        // Trigger re-render để update ports
                        border.InvalidateMeasure();
                        border.InvalidateArrange();
                        border.UpdateLayout();
                        Debug.WriteLine($"[EmbedApplicationNodeControl] Layout updated");
                    }
                    else
                    {
                        Debug.WriteLine($"[EmbedApplicationNodeControl] Deactivating embed mode");
                        
                        // Hiện icon, ẩn embedded window
                        iconSvg.Visibility = Visibility.Visible;
                        embeddedHost.Visibility = Visibility.Collapsed;

                        // Reset kích thước node
                        grid.Width = 60;
                        grid.Height = 60;
                        grid.MinWidth = 60;
                        grid.MinHeight = 60;
                        
                        border.Width = 60;
                        border.Height = 60;
                        border.MinWidth = 60;
                        border.MinHeight = 60;

                        // Unembed window nếu có
                        embeddedHost.UnembedWindow();
                        
                        // Update ports position
                        if (host.WorkflowCanvas != null)
                        {
                            foreach (var port in node.Ports)
                            {
                                host.UpdatePortsPositionOnSide(node, port.Position);
                            }
                        }
                    }
                },
                [nameof(EmbedApplicationNode.EmbeddedWidth)] = ctx =>
                {
                    if (node.HasEmbeddedWindow)
                    {
                        grid.Width = node.EmbeddedWidth;
                        border.InvalidateMeasure();
                    }
                },
                [nameof(EmbedApplicationNode.EmbeddedHeight)] = ctx =>
                {
                    if (node.HasEmbeddedWindow)
                    {
                        grid.Height = node.EmbeddedHeight;
                        border.InvalidateMeasure();
                    }
                }
            };

            // ─── 6. FLUENT API ───
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new EmbedApplicationNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }
    }
}
