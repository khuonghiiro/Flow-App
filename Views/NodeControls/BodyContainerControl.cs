using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models.Nodes;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.NodeControls;

public static class BodyContainerControl
{
    public const double BodyScaleBaseWidth = 800.0;
    public const double BodyScaleBaseHeight = 400.0;

    private enum ResizeDirection
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }

    public static Border CreateBorder(BodyContainerNode node)
    {
        var border = new Border
        {
            Width = node.BodyWidth,
            Height = node.BodyHeight,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2),
            Tag = node
        };

        var root = new Grid();
        root.ClipToBounds = false;

        var fillRect = new Rectangle { RadiusX = 10, RadiusY = 10 };
        root.Children.Add(fillRect);

        var borderRect = new Rectangle
        {
            RadiusX = 10,
            RadiusY = 10,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 }
        };
        root.Children.Add(borderRect);

        var titleText = new TextBlock
        {
            Text = node.Title,
            Margin = new Thickness(0, -26, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        node.TitleTextBlockUI = titleText;
        root.Children.Add(titleText);

        var lockIcon = new SvgViewboxEx
        {
            Width = 46,
            Height = 46,
            Opacity = 0.62,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        root.Children.Add(lockIcon);

        AddResizeHandle(root, ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top);
        AddResizeHandle(root, ResizeDirection.Top, HorizontalAlignment.Center, VerticalAlignment.Top);
        AddResizeHandle(root, ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top);
        AddResizeHandle(root, ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Center);
        AddResizeHandle(root, ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom);
        AddResizeHandle(root, ResizeDirection.Bottom, HorizontalAlignment.Center, VerticalAlignment.Bottom);
        AddResizeHandle(root, ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom);
        AddResizeHandle(root, ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Center);

        border.Child = root;
        ApplyNodeVisual(node, border, fillRect, borderRect, titleText, lockIcon);
        AttachResizeLogic(border, node);
        return border;
    }

    public static void RefreshVisualFromNode(BodyContainerNode node)
    {
        if (!TryGetVisualElements(node, out _, out var fillRect, out var borderRect, out var titleText, out var lockIcon))
            return;

        ApplyNodeVisual(node, node.Border, fillRect, borderRect, titleText, lockIcon);
        UpdateTitleVisibility(node, titleText, isHovering: false);
    }

    public static void ApplyNodeVisual(
        BodyContainerNode node,
        Border border,
        Rectangle fillRect,
        Rectangle borderRect,
        TextBlock titleText,
        SvgViewboxEx lockIcon)
    {
        border.Width = node.BodyWidth;
        border.Height = node.BodyHeight;
        var visualScale = Math.Clamp(Math.Max(node.BodyWidth / BodyScaleBaseWidth, node.BodyHeight / BodyScaleBaseHeight), 1.0, 2.0);

        // bỏ giới hạn của scale title
        var titleScale = Math.Max(1.0, Math.Max(node.BodyWidth / BodyScaleBaseWidth, node.BodyHeight / BodyScaleBaseHeight));

        var borderColor = ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Color.FromRgb(107, 114, 128));
        var backgroundColor = ParseColor(node.BodyBackgroundColorHex, Color.FromRgb(107, 114, 128));
        var alpha = (byte)Math.Round(Math.Clamp(node.BackgroundOpacityPercent / 100.0, 0, 1) * 255);
        backgroundColor.A = alpha;

        fillRect.Fill = new SolidColorBrush(backgroundColor);
        borderRect.Stroke = new SolidColorBrush(borderColor);
        titleText.Text = node.Title;
        titleText.Foreground = ResolveTitleBrush(node, borderColor);
        titleText.FontSize = 12 * titleScale;
        titleText.Margin = new Thickness(0, -26 * titleScale, 0, 0);

        // Đổi icon hiển thị giữa tâm border khi check lock/unlock
        var lockIconKey = node.LockInnerNodes ? "arrow-down-up-lock duotone-light" : "unlock light";
        lockIcon.Source = null;
        lockIcon.Source = new IconKeyToPathConverter().Convert(
            null, typeof(Uri), lockIconKey, CultureInfo.CurrentCulture) as Uri;
        lockIcon.InvalidateVisual();
        lockIcon.Fill = new SolidColorBrush(Color.FromArgb(235, 17, 24, 39));
        lockIcon.Width = Math.Max(32, Math.Min(node.BodyWidth, node.BodyHeight) * 0.18);
        lockIcon.Height = lockIcon.Width;

        UpdateResizeHandleScale(border, node.BodyWidth, node.BodyHeight, borderColor, node.LockInnerNodes);
    }

    public static void UpdateTitleVisibility(BodyContainerNode node, TextBlock titleText, bool isHovering)
    {
        titleText.Visibility = node.TitleDisplayMode switch
        {
            Models.TitleDisplayMode.Hidden => Visibility.Collapsed,
            Models.TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Visible
        };
    }

    private static void AddResizeHandle(Grid grid, ResizeDirection direction, HorizontalAlignment hAlign, VerticalAlignment vAlign)
    {
        var handle = new Ellipse
        {
            Width = 12,
            Height = 12,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            HorizontalAlignment = hAlign,
            VerticalAlignment = vAlign,
            Margin = new Thickness(2),
            Tag = direction,
            Cursor = GetCursor(direction)
        };
        grid.Children.Add(handle);
    }

    private static void UpdateResizeHandleScale(Border border, double bodyWidth, double bodyHeight, Color handleColor, bool hideHandles)
    {
        var grid = GetBodyVisualGrid(border);
        if (grid == null) return;
        var scale = Math.Clamp(Math.Max(bodyWidth / BodyScaleBaseWidth, bodyHeight / BodyScaleBaseHeight), 1.0, 2.0);
        foreach (var child in grid.Children)
        {
            if (child is not Ellipse handle || handle.Tag is not ResizeDirection) continue;
            handle.Fill = new SolidColorBrush(handleColor);
            handle.RenderTransformOrigin = new Point(0.5, 0.5);
            handle.RenderTransform = new ScaleTransform(scale, scale);
            handle.Visibility = hideHandles ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public static bool TryGetVisualElements(
        BodyContainerNode node,
        out Border border,
        out Rectangle fillRect,
        out Rectangle borderRect,
        out TextBlock titleText,
        out SvgViewboxEx lockIcon)
    {
        border = node.Border!;
        fillRect = null!;
        borderRect = null!;
        titleText = null!;
        lockIcon = null!;

        if (node.Border == null) return false;
        var bodyGrid = GetBodyVisualGrid(node.Border);
        if (bodyGrid == null) return false;
        if (bodyGrid.Children.Count < 4) return false;
        if (bodyGrid.Children[0] is not Rectangle fill) return false;
        if (bodyGrid.Children[1] is not Rectangle stroke) return false;
        if (bodyGrid.Children[2] is not TextBlock title) return false;
        if (bodyGrid.Children[3] is not SvgViewboxEx icon) return false;

        border = node.Border;
        fillRect = fill;
        borderRect = stroke;
        titleText = title;
        lockIcon = icon;
        return true;
    }

    private static Grid? GetBodyVisualGrid(Border border)
    {
        if (border.Child is not Grid root) return null;
        if (LooksLikeBodyVisualGrid(root)) return root;

        return root.Children
            .OfType<Grid>()
            .FirstOrDefault(LooksLikeBodyVisualGrid);
    }

    private static bool LooksLikeBodyVisualGrid(Grid grid)
    {
        return grid.Children.Count >= 4 &&
               grid.Children[0] is Rectangle &&
               grid.Children[1] is Rectangle &&
               grid.Children[2] is TextBlock &&
               grid.Children[3] is SvgViewboxEx;
    }

    private static Cursor GetCursor(ResizeDirection direction) => direction switch
    {
        ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
        ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
        ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
        ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
        _ => Cursors.Arrow
    };

    private static void AttachResizeLogic(Border border, BodyContainerNode node)
    {
        var isResizing = false;
        var resizeDirection = ResizeDirection.None;
        var start = new Point();
        double originalX = 0, originalY = 0, originalW = 0, originalH = 0;

        border.PreviewMouseDown += (_, e) =>
        {
            if (e.OriginalSource is not Ellipse handle || handle.Tag is not ResizeDirection dir) return;
            isResizing = true;
            resizeDirection = dir;
            start = e.GetPosition(border.Parent as UIElement);
            originalX = node.X;
            originalY = node.Y;
            originalW = node.BodyWidth;
            originalH = node.BodyHeight;
            border.CaptureMouse();
            e.Handled = true;
        };

        border.PreviewMouseMove += (_, e) =>
        {
            if (!isResizing) return;
            var current = e.GetPosition(border.Parent as UIElement);
            var dx = current.X - start.X;
            var dy = current.Y - start.Y;

            var newX = originalX;
            var newY = originalY;
            var newW = originalW;
            var newH = originalH;

            if (resizeDirection is ResizeDirection.Left or ResizeDirection.TopLeft or ResizeDirection.BottomLeft)
            {
                newW = Math.Max(200, originalW - dx);
                newX = originalX + (originalW - newW);
            }
            if (resizeDirection is ResizeDirection.Right or ResizeDirection.TopRight or ResizeDirection.BottomRight)
                newW = Math.Max(200, originalW + dx);
            if (resizeDirection is ResizeDirection.Top or ResizeDirection.TopLeft or ResizeDirection.TopRight)
            {
                newH = Math.Max(200, originalH - dy);
                newY = originalY + (originalH - newH);
            }
            if (resizeDirection is ResizeDirection.Bottom or ResizeDirection.BottomLeft or ResizeDirection.BottomRight)
                newH = Math.Max(200, originalH + dy);

            node.X = newX;
            node.Y = newY;
            node.BodyWidth = newW;
            node.BodyHeight = newH;
            border.Width = newW;
            border.Height = newH;
            UpdateResizeHandleScale(border, newW, newH, ParseColor(node.UseUnifiedColors ? node.BodyBackgroundColorHex : node.BodyBorderColorHex, Color.FromRgb(107, 114, 128)), node.LockInnerNodes);

            Canvas.SetLeft(border, newX);
            Canvas.SetTop(border, newY);
            e.Handled = true;
        };

        border.PreviewMouseUp += (_, e) =>
        {
            if (!isResizing) return;
            isResizing = false;
            resizeDirection = ResizeDirection.None;
            border.ReleaseMouseCapture();
            e.Handled = true;
        };
    }

    private static Color ParseColor(string? input, Color fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            var token = input.Trim();
            if (token.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                return (Color)ColorConverter.ConvertFromString(token);

            var resource = Application.Current.TryFindResource(token);
            if (resource is SolidColorBrush sb) return sb.Color;
            if (resource is Color c) return c;

            // Named color fallback, e.g. "Red"
            return (Color)ColorConverter.ConvertFromString(token);
        }
        catch
        {
            return fallback;
        }
    }

    private static Brush ResolveTitleBrush(BodyContainerNode node, Color fallback)
    {
        if (node.TitleColorMode != Models.TitleColorMode.CustomColor ||
            string.IsNullOrWhiteSpace(node.TitleColorKey) ||
            string.Equals(node.TitleColorKey, "NodeColor", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(fallback);
        }

        if (string.Equals(node.TitleColorKey, "LimeGreen", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Colors.LimeGreen);

        var resource = Application.Current.TryFindResource(node.TitleColorKey);
        if (resource is Brush brush) return brush;
        if (resource is Color color) return new SolidColorBrush(color);
        return new SolidColorBrush(fallback);
    }
}
