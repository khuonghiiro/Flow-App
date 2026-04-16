using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        /// <summary>
        /// Lấy màu từ brush (hỗ trợ nhiều loại brush) - dùng cho node text contrast, connection color, etc.
        /// </summary>
        private Color GetColorFromBrush(Brush? brush)
        {
            if (brush == null) return Colors.LimeGreen;

            if (brush is SolidColorBrush solidBrush)
            {
                return solidBrush.Color;
            }
            else if (brush is LinearGradientBrush linearGradient)
            {
                if (linearGradient.GradientStops.Count > 0)
                {
                    return linearGradient.GradientStops[0].Color;
                }
            }
            else if (brush is RadialGradientBrush radialGradient)
            {
                if (radialGradient.GradientStops.Count > 0)
                {
                    return radialGradient.GradientStops[0].Color;
                }
            }
            else if (brush is DrawingBrush drawingBrush && drawingBrush.Drawing is GeometryDrawing geometryDrawing)
            {
                if (geometryDrawing.Brush is SolidColorBrush drawingSolidBrush)
                {
                    return drawingSolidBrush.Color;
                }
            }

            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, 0, 1, 1));
                }
                var rtb = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                var pixels = new byte[4];
                rtb.CopyPixels(pixels, 4, 0);
                return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
            }
            catch
            {
                return Colors.LimeGreen;
            }
        }
    }
}

