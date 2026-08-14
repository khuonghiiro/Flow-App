using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Utilities
{
    public sealed class GridPatternService
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private System.Windows.Controls.Canvas _gridCanvas => _hostAccessor.GetRequiredHost().GridCanvas;
        private string _gridType = "None";

        public GridPatternService(IWorkflowEditorHostAccessor hostAccessor)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
        }

        public void SetGridType(string type)
        {
            _gridType = string.IsNullOrWhiteSpace(type) ? "None" : type;
        }

        public void UpdatePattern(string gridType, Color gridColor, Color? canvasBackgroundColor = null)
        {
            SetGridType(gridType);

            _gridCanvas.Children.Clear();
            if (_gridType == "None") return;

            // React Flow-like density: visible enough for positioning without overpowering nodes.
            const double gridSize = 32;

            if (_gridType == "Dots")
            {
                var gridBrush = CreateGridBrush(gridColor, canvasBackgroundColor, isDots: true);
                var drawingGroup = new DrawingGroup();
                var geometryDrawing = new GeometryDrawing
                {
                    Brush = gridBrush,
                    Geometry = new EllipseGeometry(new Point(gridSize / 2, gridSize / 2), 1.65, 1.65)
                };
                drawingGroup.Children.Add(geometryDrawing);

                var drawingBrush = new DrawingBrush(drawingGroup)
                {
                    TileMode = TileMode.Tile,
                    Viewport = new Rect(0, 0, gridSize, gridSize),
                    ViewportUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.None
                };

                _gridCanvas.Children.Add(new Rectangle
                {
                    Width = 20000,
                    Height = 20000,
                    Fill = drawingBrush,
                    IsHitTestVisible = false
                });
                return;
            }

            if (_gridType == "Lines")
            {
                var gridBrush = CreateGridBrush(gridColor, canvasBackgroundColor, isDots: false);
                var gridPen = new Pen(gridBrush, 0.95)
                {
                    DashCap = PenLineCap.Flat,
                    StartLineCap = PenLineCap.Flat,
                    EndLineCap = PenLineCap.Flat
                };
                gridPen.Freeze();

                var drawingGroup = new DrawingGroup();

                drawingGroup.Children.Add(new GeometryDrawing
                {
                    Brush = gridBrush,
                    Pen = gridPen,
                    Geometry = new LineGeometry(new Point(0, 0), new Point(0, gridSize))
                });

                drawingGroup.Children.Add(new GeometryDrawing
                {
                    Brush = gridBrush,
                    Pen = gridPen,
                    Geometry = new LineGeometry(new Point(0, 0), new Point(gridSize, 0))
                });

                var drawingBrush = new DrawingBrush(drawingGroup)
                {
                    TileMode = TileMode.Tile,
                    Viewport = new Rect(0, 0, gridSize, gridSize),
                    ViewportUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.None
                };

                _gridCanvas.Children.Add(new Rectangle
                {
                    Width = 20000,
                    Height = 20000,
                    Fill = drawingBrush,
                    IsHitTestVisible = false
                });
            }
        }

        private static SolidColorBrush CreateGridBrush(Color themeGridColor, Color? canvasBackgroundColor, bool isDots)
        {
            var backgroundLuminance = canvasBackgroundColor.HasValue
                ? GetRelativeLuminance(canvasBackgroundColor.Value)
                : 0.5;

            var isLightCanvas = backgroundLuminance >= 0.58;
            if (isLightCanvas)
                themeGridColor = Mix(themeGridColor, Colors.Black, isDots ? 0.28 : 0.34);

            var targetAlpha = isDots
                ? (isLightCanvas ? 0xC8 : 0x96)
                : (isLightCanvas ? 0xB8 : 0x84);
            var maxAlpha = isDots
                ? (isLightCanvas ? 0xE0 : 0xAC)
                : (isLightCanvas ? 0xD0 : 0x96);

            var sourceAlpha = themeGridColor.A == 0 ? targetAlpha : themeGridColor.A;
            var alpha = (byte)Math.Min(Math.Max(sourceAlpha, targetAlpha), maxAlpha);

            var color = Color.FromArgb(alpha, themeGridColor.R, themeGridColor.G, themeGridColor.B);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Mix(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);

            static byte Lerp(byte a, byte b, double t)
                => (byte)Math.Round(a + (b - a) * t);

            return Color.FromArgb(
                from.A,
                Lerp(from.R, to.R, amount),
                Lerp(from.G, to.G, amount),
                Lerp(from.B, to.B, amount));
        }

        private static double GetRelativeLuminance(Color color)
        {
            static double ToLinear(byte channel)
            {
                var value = channel / 255.0;
                return value <= 0.03928
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * ToLinear(color.R)
                 + 0.7152 * ToLinear(color.G)
                 + 0.0722 * ToLinear(color.B);
        }
    }
}

