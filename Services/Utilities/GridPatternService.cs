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

        public void UpdatePattern(string gridType, Color gridColor)
        {
            SetGridType(gridType);

            _gridCanvas.Children.Clear();
            if (_gridType == "None") return;

            // Keep opacity from theme resource color itself to avoid double-fading.
            var gridBrush = new SolidColorBrush(gridColor);
            double gridSize = 50;

            if (_gridType == "Dots")
            {
                var drawingGroup = new DrawingGroup();
                var geometryDrawing = new GeometryDrawing
                {
                    Brush = gridBrush,
                    Geometry = new EllipseGeometry(new Point(gridSize / 2, gridSize / 2), 2.2, 2.2)
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
                    Fill = drawingBrush
                });
                return;
            }

            if (_gridType == "Lines")
            {
                var drawingGroup = new DrawingGroup();

                drawingGroup.Children.Add(new GeometryDrawing
                {
                    Brush = gridBrush,
                    Pen = new Pen(gridBrush, 1.2),
                    Geometry = new LineGeometry(new Point(0, 0), new Point(0, gridSize))
                });

                drawingGroup.Children.Add(new GeometryDrawing
                {
                    Brush = gridBrush,
                    Pen = new Pen(gridBrush, 1.2),
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
                    Fill = drawingBrush
                });
            }
        }
    }
}

