using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Undoable command to rotate the entire canvas and all layers (including text, original transforms, bounds)
    /// by a multiple of 90 degrees.
    /// </summary>
    public sealed class RotateCanvasCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly double _angle; // 90, -90/270, 180
        private readonly int _oldWidth;
        private readonly int _oldHeight;
        private readonly int _newWidth;
        private readonly int _newHeight;

        // State storage for Undo
        private readonly List<LayerState> _layerStates = new();

        private class LayerState
        {
            public EditorLayer Layer { get; }
            public WriteableBitmap OldBitmap { get; }
            public WriteableBitmap? OldOriginalBmp { get; }
            public Rect OldContentBounds { get; }
            public int OldWidth { get; }
            public int OldHeight { get; }

            // Text layer properties
            public double OldTextX { get; }
            public double OldTextY { get; }
            public double OldTextWidth { get; }
            public double OldTextHeight { get; }

            public LayerState(EditorLayer layer)
            {
                Layer = layer;
                OldBitmap = layer.Bitmap.Clone();
                OldOriginalBmp = layer.OriginalTransformBitmap?.Clone();
                OldContentBounds = layer.ContentBounds;
                OldWidth = layer.Width;
                OldHeight = layer.Height;

                OldTextX = layer.TextX;
                OldTextY = layer.TextY;
                OldTextWidth = layer.TextWidth;
                OldTextHeight = layer.TextHeight;
            }
        }

        public RotateCanvasCommand(EditorDocument doc, double angle, int oldW, int oldH, int newW, int newH)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _angle = angle;
            _oldWidth = oldW;
            _oldHeight = oldH;
            _newWidth = newW;
            _newHeight = newH;

            // Capture initial state of all layers
            CaptureState(_doc.Layers);
        }

        private void CaptureState(IEnumerable<EditorLayer> layers)
        {
            foreach (var l in layers)
            {
                _layerStates.Add(new LayerState(l));
                if (l.ChildLayers.Count > 0)
                {
                    CaptureState(l.ChildLayers);
                }
            }
        }

        public string Description => $"Rotate Canvas {_angle}°";

        public void Execute()
        {
            // Swap doc dimensions
            _doc.Width = _newWidth;
            _doc.Height = _newHeight;

            var transform = new RotateTransform(_angle);
            RotateLayers(_doc.Layers, transform, _oldWidth, _oldHeight, _newWidth, _newHeight, _angle);
        }

        public void Undo()
        {
            // Restore doc dimensions
            _doc.Width = _oldWidth;
            _doc.Height = _oldHeight;

            // Restore initial layer states
            foreach (var state in _layerStates)
            {
                state.Layer.Width = state.OldWidth;
                state.Layer.Height = state.OldHeight;
                state.Layer.Bitmap = state.OldBitmap;
                state.Layer.OriginalTransformBitmap = state.OldOriginalBmp;
                state.Layer.ContentBounds = state.OldContentBounds;

                state.Layer.TextX = state.OldTextX;
                state.Layer.TextY = state.OldTextY;
                state.Layer.TextWidth = state.OldTextWidth;
                state.Layer.TextHeight = state.OldTextHeight;

                state.Layer.InvalidateThumbnail();
            }
        }

        private void RotateLayers(IEnumerable<EditorLayer> layers, Transform transform, double oldW, double oldH, double newW, double newH, double angle)
        {
            foreach (var l in layers)
            {
                // Rotate layer bitmap using SkiaSharp to prevent cropping or distortion
                l.ApplyTransform(transform);

                // Rotate OriginalTransformBitmap if present
                if (l.OriginalTransformBitmap != null)
                {
                    var transformedOrig = new TransformedBitmap(l.OriginalTransformBitmap, transform);
                    var convertedOrig = new FormatConvertedBitmap(transformedOrig, PixelFormats.Bgra32, null, 0);
                    l.OriginalTransformBitmap = new WriteableBitmap(convertedOrig);
                }

                // Rotate ContentBounds Rect
                if (!l.ContentBounds.IsEmpty && l.ContentBounds.Width > 0 && l.ContentBounds.Height > 0)
                {
                    l.ContentBounds = RotateRect(l.ContentBounds, oldW, oldH, newW, newH, angle);
                }

                // If text layer, rotate position and text box dimensions
                if (l.IsTextLayer)
                {
                    // Rotate the top-left corner of the text box
                    Point textTL = new Point(l.TextX, l.TextY);
                    Point textBR = new Point(l.TextX + l.TextWidth, l.TextY + l.TextHeight);
                    
                    Point rotatedTL = RotatePoint(textTL, oldW, oldH, newW, newH, angle);
                    Point rotatedBR = RotatePoint(textBR, oldW, oldH, newW, newH, angle);

                    double x1 = Math.Min(rotatedTL.X, rotatedBR.X);
                    double x2 = Math.Max(rotatedTL.X, rotatedBR.X);
                    double y1 = Math.Min(rotatedTL.Y, rotatedBR.Y);
                    double y2 = Math.Max(rotatedTL.Y, rotatedBR.Y);

                    l.TextX = x1;
                    l.TextY = y1;
                    l.TextWidth = Math.Max(1, x2 - x1);
                    l.TextHeight = Math.Max(1, y2 - y1);
                }

                l.InvalidateThumbnail();

                // Recursively rotate children
                if (l.ChildLayers.Count > 0)
                {
                    RotateLayers(l.ChildLayers, transform, oldW, oldH, newW, newH, angle);
                }
            }
        }

        private Rect RotateRect(Rect rect, double oldW, double oldH, double newW, double newH, double angle)
        {
            Point p1 = rect.TopLeft;
            Point p2 = rect.TopRight;
            Point p3 = rect.BottomLeft;
            Point p4 = rect.BottomRight;

            Point r1 = RotatePoint(p1, oldW, oldH, newW, newH, angle);
            Point r2 = RotatePoint(p2, oldW, oldH, newW, newH, angle);
            Point r3 = RotatePoint(p3, oldW, oldH, newW, newH, angle);
            Point r4 = RotatePoint(p4, oldW, oldH, newW, newH, angle);

            double minX = Math.Min(Math.Min(r1.X, r2.X), Math.Min(r3.X, r4.X));
            double maxX = Math.Max(Math.Max(r1.X, r2.X), Math.Max(r3.X, r4.X));
            double minY = Math.Min(Math.Min(r1.Y, r2.Y), Math.Min(r3.Y, r4.Y));
            double maxY = Math.Max(Math.Max(r1.Y, r2.Y), Math.Max(r3.Y, r4.Y));

            return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }

        private Point RotatePoint(Point p, double oldW, double oldH, double newW, double newH, double angle)
        {
            double cxOld = oldW / 2.0;
            double cyOld = oldH / 2.0;
            double cxNew = newW / 2.0;
            double cyNew = newH / 2.0;

            double dx = p.X - cxOld;
            double dy = p.Y - cyOld;

            double rad = angle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double rx = dx * cos - dy * sin;
            double ry = dx * sin + dy * cos;

            return new Point(rx + cxNew, ry + cyNew);
        }
    }
}
