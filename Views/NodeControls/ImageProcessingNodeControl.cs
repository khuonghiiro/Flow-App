// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Globalization;
using System.Collections.Specialized;
using System.Text.Json;
using WinForms = System.Windows.Forms;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static partial class ImageProcessingNodeControl
    {
        private enum ResizeDirection { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }

        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _previewVersion = new();
        internal static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, ImageCropRegion?> _activeCropRegion = new();
        internal static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _activeCropColorIndex = new();
        private static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, int> _cropOrderCounter = new();
        internal static readonly System.Collections.Generic.Dictionary<ImageProcessingNode, ImageCropRegion?> _currentCropRegionForIp = new();
        // Lưu polygon overlay trên imageGrid theo từng vùng crop để xoá/cập nhật đúng khi cần
        internal static readonly System.Collections.Generic.Dictionary<ImageCropRegion, System.Windows.Shapes.Polygon> _polygonMap = new();

        internal static void DetachCropPolygon(ImageCropRegion reg, Panel? searchPanel = null)
        {
            if (_polygonMap.TryGetValue(reg, out var poly))
            {
                if (poly.Parent is Panel p)
                    p.Children.Remove(poly);
                _polygonMap.Remove(reg);
                return;
            }

            if (searchPanel == null) return;
            for (int i = searchPanel.Children.Count - 1; i >= 0; i--)
            {
                if (searchPanel.Children[i] is Polygon pg && ReferenceEquals(pg.Tag, reg))
                {
                    searchPanel.Children.RemoveAt(i);
                    return;
                }
            }
        }

        internal static readonly Color[] _cropColors = new[]
        {
            Colors.Gold,
            Colors.DeepSkyBlue,
            Colors.LimeGreen,
            Colors.OrangeRed,
            Colors.MediumOrchid
        };

        /// <summary>Min kích thước node ảnh (canvas + floating widget); đồng bộ khi chỉnh min trong dialog widget.</summary>
        public const double ImageNodeMinWidthPx = 480;
        /// <summary>Min chiều cao node ảnh (canvas + floating widget).</summary>
        public const double ImageNodeMinHeightPx = 360;
        private const double ImageNodeDefaultWidthPx = 800;
        private const double ImageNodeDefaultHeightPx = 600;

        public static Border CreateBorder(ImageProcessingNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // Đảm bảo node có Width/Height hợp lệ (không NaN) để Grid Star columns tính đúng
            double initW = double.IsNaN(node.Width) ? ImageNodeDefaultWidthPx : Math.Max(node.Width, ImageNodeMinWidthPx);
            double initH = double.IsNaN(node.Height) ? ImageNodeDefaultHeightPx : Math.Max(node.Height, ImageNodeMinHeightPx);

            // Khởi tạo bộ đếm Order từ các crop đã load sẵn (để crop mới tiếp nối đúng số)
            if (!_cropOrderCounter.ContainsKey(node) && node.Crops.Count > 0)
            {
                _cropOrderCounter[node] = node.Crops.Max(c => c.Order);
            }

            // --- Create UI elements (node-specific) ---

            // Viền ngoài không gắn Effect — DropShadow trên cùng visual với nội dung làm mờ toàn node.
            // Bóng chỉ trên shadowPlate (nền); grid nội dung là lớp trên, vẽ sắc hơn.
            var border = new Border
            {
                Width = initW,
                Height = initH,
                MinWidth = ImageNodeMinWidthPx,
                MinHeight = ImageNodeMinHeightPx,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = null,
                Tag = node,
                CacheMode = null
            };

            var shadowPlate = new Border
            {
                Background = node.NodeBrush,
                CornerRadius = new CornerRadius(8),
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                ClipToBounds = false
            };
            GpuOptimizationHelper.ApplyToElement(shadowPlate);

            // Force layout refresh on Loaded to ensure Grid Star columns render correctly
            border.Loaded += (s, e) =>
            {
                border.InvalidateMeasure();
                border.InvalidateArrange();
                border.UpdateLayout();
            };

            bool isResizing = false;

            var handleOverlay = new Grid();
            var imageContent = new ImageProcessingNodeContentControl(node, host, border, ownerWindow, handleOverlay, () => isResizing);
            AddResizeHandle(handleOverlay, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 2, 0));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 2, 2));
            AddResizeHandle(handleOverlay, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 2, 0, 0));

            var outerGrid = new Grid();
            outerGrid.Children.Add(imageContent);
            outerGrid.Children.Add(handleOverlay);
            Panel.SetZIndex(outerGrid, 1);
            GpuOptimizationHelper.ApplyToElement(outerGrid);

            var chromeFillGrid = new Grid();
            chromeFillGrid.Children.Add(shadowPlate);
            chromeFillGrid.Children.Add(outerGrid);
            border.Child = chromeFillGrid;

            // --- Resize handle logic (node-specific) ---
            ResizeDirection currentDir = ResizeDirection.None;
            Point resizeStart = default;
            double origW = 0, origH = 0, origX = 0, origY = 0;

            border.PreviewMouseDown += (s, e) =>
            {
                if (e.OriginalSource is Ellipse handle && handle.Tag is ResizeDirection dir)
                {
                    isResizing = true;
                    currentDir = dir;
                    resizeStart = e.GetPosition(border.Parent as UIElement);
                    origW = border.ActualWidth;
                    origH = border.ActualHeight;
                    origX = node.X;
                    origY = node.Y;
                    border.CaptureMouse();
                    e.Handled = true;
                }
            };

            border.PreviewMouseMove += (s, e) =>
            {
                if (!isResizing) return;
                var pos = e.GetPosition(border.Parent as UIElement);
                var dx = pos.X - resizeStart.X;
                var dy = pos.Y - resizeStart.Y;
                double newX = origX, newY = origY, newW = origW, newH = origH;

                var minW = border.MinWidth > 0 ? border.MinWidth : 260;
                var minH = border.MinHeight > 0 ? border.MinHeight : 200;

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
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, origH - dy);
                        newY = origY + (origH - newH);
                        break;
                }

                node.Width = newW;
                node.Height = newH;
                node.X = newX;
                node.Y = newY;
                border.Width = newW;
                border.Height = newH;
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

            // --- Create title TextBlock (node-specific initial text) ---
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Xử lý ảnh",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode,
                    node.TitleColorKey,
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible
                    : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // --- Node-specific custom property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                // NodeBrush: image node uses shadowPlate for background (not border.Background directly)
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = Brushes.Transparent;
                    shadowPlate.Background = node.NodeBrush;
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        BaseNodeControlHelper.GetTitleColorMode(node),
                        BaseNodeControlHelper.GetTitleColorKey(node),
                        node.NodeBrush);
                },
                // Width/Height: sync border size when changed externally (not during resize)
                [nameof(ImageProcessingNode.Width)] = ctx =>
                {
                    if (!isResizing) border.Width = node.Width;
                },
                [nameof(ImageProcessingNode.Height)] = ctx =>
                {
                    if (!isResizing) border.Height = node.Height;
                }
            };

            // --- Initialize with fluent API (replaces ~200 lines of duplicated event handler code) ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new ImageProcessingNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            return border;
        }

    }
}
