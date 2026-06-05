using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FlowMy.Views.NodeControls
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Overlay Window – chứa EmbeddedWindowHost, track vị trí node trên canvas
    // ══════════════════════════════════════════════════════════════════════════════
    internal class NodeEmbedOverlay : Window
    {
        private readonly EmbeddedWindowHost _host;

        public NodeEmbedOverlay(Window owner)
        {
            WindowStyle      = WindowStyle.None;
            ResizeMode       = ResizeMode.NoResize;
            ShowInTaskbar    = false;
            Owner            = owner;
            Background       = Brushes.Black;
            AllowsTransparency = false;

            _host = new EmbeddedWindowHost { RemoveDecoration = true };
            Content = _host;
        }

        /// <summary>Nhúng cửa sổ app vào overlay.</summary>
        public bool EmbedWindow(IntPtr hwnd) => _host.EmbedWindow(hwnd);

        /// <summary>Cập nhật vị trí và kích thước overlay khớp với node trên màn hình.</summary>
        public void UpdateBounds(Rect screenRect)
        {
            Left   = screenRect.Left;
            Top    = screenRect.Top;
            Width  = Math.Max(screenRect.Width,  1);
            Height = Math.Max(screenRect.Height, 1);
        }

        /// <summary>An toàn: unembed trước rồi mới đóng overlay.</summary>
        public void SafeClose()
        {
            try { _host.UnembedWindow(); } catch { }
            try { Close(); }              catch { }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    public static class EmbedApplicationNodeControl
    {
        private const double MinNodeW = 300;
        private const double MinNodeH = 200;

        // Lấy Rect màn hình của một FrameworkElement (tính đủ zoom/pan canvas)
        private static Rect GetScreenRect(FrameworkElement el)
        {
            if (el.ActualWidth <= 0 || el.ActualHeight <= 0) return Rect.Empty;
            Point tl = el.PointToScreen(new Point(0, 0));
            Point br = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
            return new Rect(tl, br);
        }

        // ════════════════════════════════════════════════════════════════════════
        public static Border CreateBorder(
            EmbedApplicationNode node,
            Window? ownerWindow,
            IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // ── Placeholder ────────────────────────────────────────────────────
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri),
                "desktop-arrow-down light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var placeholderPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin = new Thickness(12)
            };
            var placeholderIcon = new SvgViewboxEx
            {
                Source = iconUri, Width = 48, Height = 48,
                Fill   = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var placeholderText = new TextBlock
            {
                Text = "Chưa chọn app\nNhấp chuột phải để chọn",
                FontSize = 11, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            placeholderText.SetResourceReference(
                TextBlock.ForegroundProperty, $"TextOn{node.ColorKey}Brush");
            placeholderPanel.Children.Add(placeholderIcon);
            placeholderPanel.Children.Add(placeholderText);

            // ── App label (hiện khi đang embed) ───────────────────────────────
            var appLabel = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(6, 2, 6, 2),
                Margin       = new Thickness(6, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top,
                Visibility   = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var appLabelText = new TextBlock { FontSize = 10, Foreground = Brushes.White };
            appLabel.Child = appLabelText;

            // ── Content grid ───────────────────────────────────────────────────
            var contentGrid = new Grid
            {
                ClipToBounds = true,
                Width  = node.HasEmbeddedWindow ? Math.Max(node.EmbeddedWidth,  MinNodeW) : MinNodeW,
                Height = node.HasEmbeddedWindow ? Math.Max(node.EmbeddedHeight, MinNodeH) : MinNodeH,
                MinWidth = MinNodeW, MinHeight = MinNodeH
            };
            contentGrid.Children.Add(placeholderPanel);
            contentGrid.Children.Add(appLabel);

            // ── Title ──────────────────────────────────────────────────────────
            var titleTextBlock = new TextBlock
            {
                Text       = node.Title ?? "Embed Application",
                FontSize   = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top,
                TextAlignment       = TextAlignment.Center,
                IsHitTestVisible    = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // ── Node border ────────────────────────────────────────────────────
            var border = new Border
            {
                Child           = contentGrid,
                Background      = node.NodeBrush,
                BorderBrush     = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(10),
                Cursor          = Cursors.Hand,
                ClipToBounds    = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node
            };

            // ── State ──────────────────────────────────────────────────────────
            NodeEmbedOverlay? overlay = null;
            DispatcherTimer?  trackTimer = null;

            // ── Timers ─────────────────────────────────────────────────────────
            void StartTracking()
            {
                if (trackTimer != null) return;
                trackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                trackTimer.Tick += (_, __) =>
                {
                    if (overlay == null) return;
                    try
                    {
                        var rect = GetScreenRect(contentGrid);
                        if (!rect.IsEmpty) overlay.UpdateBounds(rect);
                    }
                    catch { /* layout not ready */ }
                };
                trackTimer.Start();
            }

            void StopTracking()
            {
                trackTimer?.Stop();
                trackTimer = null;
            }

            // ── Embed / Unembed ────────────────────────────────────────────────
            void DoEmbed()
            {
                if (node.WindowHandle == IntPtr.Zero) return;
                if (overlay != null) return; // đã embed

                var mainWin = ownerWindow ?? Application.Current?.MainWindow;
                if (mainWin == null) return;

                Debug.WriteLine($"[EmbedNode] Creating overlay for hwnd=0x{node.WindowHandle:X}");

                // Kích thước node
                contentGrid.Width  = Math.Max(node.EmbeddedWidth,  MinNodeW);
                contentGrid.Height = Math.Max(node.EmbeddedHeight, MinNodeH);
                border.Width  = contentGrid.Width;
                border.Height = contentGrid.Height;

                // Tạo và hiện overlay – vị trí ban đầu
                overlay = new NodeEmbedOverlay(mainWin);
                var initialRect = GetScreenRect(contentGrid);
                if (!initialRect.IsEmpty) overlay.UpdateBounds(initialRect);

                overlay.Show();

                bool ok = overlay.EmbedWindow(node.WindowHandle);
                if (!ok)
                {
                    Debug.WriteLine("[EmbedNode] EmbedWindow failed");
                    overlay.SafeClose();
                    overlay = null;
                    node.HasEmbeddedWindow = false;
                    return;
                }

                // UI state
                placeholderPanel.Visibility = Visibility.Collapsed;
                appLabelText.Text           = $"📦 {node.ProcessName}";
                appLabel.Visibility         = Visibility.Visible;

                if (border.Effect is DropShadowEffect sh)
                { sh.Color = Colors.DeepSkyBlue; sh.Opacity = 0.9; }

                StartTracking();

                if (host.WorkflowCanvas != null)
                    foreach (var port in node.Ports)
                        host.UpdatePortsPositionOnSide(node, port.Position);

                Debug.WriteLine("[EmbedNode] ✅ Overlay showing app");
            }

            void DoUnembed()
            {
                StopTracking();

                if (overlay != null)
                {
                    // Unembed trên background thread để không block UI
                    var ov = overlay;
                    overlay = null;
                    Task.Run(() =>
                    {
                        try { ov.Dispatcher.Invoke(() => ov.SafeClose()); }
                        catch { }
                    });
                }

                placeholderPanel.Visibility = Visibility.Visible;
                appLabel.Visibility         = Visibility.Collapsed;

                contentGrid.Width  = MinNodeW;
                contentGrid.Height = MinNodeH;
                border.Width  = MinNodeW;
                border.Height = MinNodeH;

                if (border.Effect is DropShadowEffect sh)
                { sh.Color = Colors.Black; sh.Opacity = 0.5; }
            }

            // Khởi tạo ban đầu nếu node đã có app
            if (node.HasEmbeddedWindow && node.WindowHandle != IntPtr.Zero)
                border.Loaded += (_, __) => DoEmbed();

            // ── Property handlers ──────────────────────────────────────────────
            var handlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = _ =>
                {
                    placeholderIcon.Fill = BaseNodeControlHelper.ResolveTextOnColorBrush(node.ColorKey);
                    placeholderText.SetResourceReference(
                        TextBlock.ForegroundProperty, $"TextOn{node.ColorKey}Brush");
                },
                [nameof(WorkflowNode.NodeBrush)] = _ => border.Background = node.NodeBrush,

                [nameof(EmbedApplicationNode.HasEmbeddedWindow)] = _ =>
                {
                    if (node.HasEmbeddedWindow && node.WindowHandle != IntPtr.Zero)
                        border.Dispatcher.BeginInvoke(DoEmbed, DispatcherPriority.Loaded);
                    else
                        DoUnembed();
                },
                [nameof(EmbedApplicationNode.WindowHandle)] = _ =>
                {
                    DoUnembed();
                    if (node.HasEmbeddedWindow && node.WindowHandle != IntPtr.Zero)
                        border.Dispatcher.BeginInvoke(DoEmbed, DispatcherPriority.Loaded);
                },
                [nameof(EmbedApplicationNode.EmbeddedWidth)] = _ =>
                {
                    if (overlay == null) return;
                    contentGrid.Width = Math.Max(node.EmbeddedWidth, MinNodeW);
                    border.Width = contentGrid.Width;
                    if (host.WorkflowCanvas != null)
                        foreach (var port in node.Ports)
                            host.UpdatePortsPositionOnSide(node, port.Position);
                },
                [nameof(EmbedApplicationNode.EmbeddedHeight)] = _ =>
                {
                    if (overlay == null) return;
                    contentGrid.Height = Math.Max(node.EmbeddedHeight, MinNodeH);
                    border.Height = contentGrid.Height;
                    if (host.WorkflowCanvas != null)
                        foreach (var port in node.Ports)
                            host.UpdatePortsPositionOnSide(node, port.Position);
                },
                [nameof(EmbedApplicationNode.IsActive)] = _ =>
                {
                    if (border.Effect is DropShadowEffect sh)
                    {
                        sh.Color   = node.IsActive ? Colors.DeepSkyBlue : Colors.Black;
                        sh.Opacity = node.IsActive ? 0.9 : 0.5;
                    }
                },
                [nameof(EmbedApplicationNode.ProcessName)] = _ =>
                {
                    if (!string.IsNullOrWhiteSpace(node.ProcessName))
                    {
                        node.Title = $"Embed: {node.ProcessName}";
                        appLabelText.Text = $"📦 {node.ProcessName}";
                    }
                }
            };

            // ── Fluent build ───────────────────────────────────────────────────
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(handlers)
                .WithDialogSupport(_ => new EmbedApplicationNodeDialog(
                    node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            // Cleanup khi node bị xóa hoặc workflow đóng
            border.Unloaded += (_, __) =>
            {
                StopTracking();
                // Đóng overlay trên background thread tránh freeze UI khi shutdown
                var ov = overlay;
                overlay = null;
                if (ov != null)
                    Task.Run(() =>
                    {
                        try { ov.Dispatcher.Invoke(ov.SafeClose, TimeSpan.FromSeconds(2)); }
                        catch { }
                    });
            };

            return border;
        }
    }
}
