// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        private void DrawActiveStrokeMouseDownToOverlay(double px, double py, double rawRadius, double hardness, double flow, Color color, bool isComplexPreset)
        {
            if (_brushOverlayBitmap == null) return;

            int w = _brushOverlayBitmap.PixelWidth;
            int h = _brushOverlayBitmap.PixelHeight;

            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);
                    if (_localSelectionClipPath != null)
                    {
                        canvas.ClipPath(_localSelectionClipPath, SkiaSharp.SKClipOperation.Intersect, true);
                    }

                    if (isComplexPreset)
                    {
                        if (_cachedBrushTip != null)
                        {
                            DrawCachedBrushTipStamp(canvas, px, py, false, color);
                        }
                        else
                        {
                            DrawSkiaBrushStamp(canvas, px, py, rawRadius, hardness, flow, color, _currentBrushPreset, false);
                        }
                    }
                    else
                    {
                        if (_currentStrokePaint != null)
                        {
                            canvas.DrawPoint((float)px, (float)py, _currentStrokePaint);
                        }
                    }
                }

                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    _brushOverlayBitmap.AddDirtyRect(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private void DrawActiveStrokeSegmentToOverlay(Point p1, Point p2)
        {
            if (_brushOverlayBitmap == null) return;

            int w = _brushOverlayBitmap.PixelWidth;
            int h = _brushOverlayBitmap.PixelHeight;

            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    if (_localSelectionClipPath != null)
                    {
                        canvas.ClipPath(_localSelectionClipPath, SkiaSharp.SKClipOperation.Intersect, true);
                    }

                    bool isComplexPreset = _currentBrushPreset != BrushPreset.RoundHard &&
                                           _currentBrushPreset != BrushPreset.RoundSoft &&
                                           _currentBrushPreset != BrushPreset.Airbrush &&
                                           _currentBrushPreset != BrushPreset.Pencil;

                    double radius = EditorPanel.BrushSize / 2.0;
                    double hardness = EditorPanel.BrushHardness;
                    double flow = EditorPanel.BrushFlow;
                    Color color = _node.EditorDoc.ForegroundColor;

                    if (isComplexPreset)
                    {
                        _currentStrokeInfo?.Points.Add(p2);
                        double dx = p2.X - p1.X;
                        double dy = p2.Y - p1.Y;
                        double distance = Math.Sqrt(dx * dx + dy * dy);

                        double step = GetBrushStep(_currentBrushPreset, radius);
                        double d = _brushDistanceAccumulator;

                        while (d <= distance)
                        {
                            double t = distance > 0.0001 ? d / distance : 0.0;
                            double cx = p1.X + dx * t;
                            double cy = p1.Y + dy * t;

                            if (_cachedBrushTip != null)
                            {
                                DrawCachedBrushTipStamp(canvas, cx, cy, false, color);
                            }
                            else
                            {
                                DrawSkiaBrushStamp(canvas, cx, cy, radius, hardness, flow, color, _currentBrushPreset, false);
                            }

                            d += step;
                        }
                        _brushDistanceAccumulator = d - distance;
                    }
                    else
                    {
                        if (_currentStrokePaint != null)
                        {
                            canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, _currentStrokePaint);
                            _currentStrokePath?.LineTo((float)p2.X, (float)p2.Y);
                        }
                    }
                }

                int dirtyW = _prevSegmentMaxX - _prevSegmentMinX + 1;
                int dirtyH = _prevSegmentMaxY - _prevSegmentMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    _brushOverlayBitmap.AddDirtyRect(new Int32Rect(_prevSegmentMinX, _prevSegmentMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private void RedrawBrushOverlay()
        {
            if (_brushOverlayBitmap == null) return;

            int w = _brushOverlayBitmap.PixelWidth;
            int h = _brushOverlayBitmap.PixelHeight;

            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);

                    // 1. Draw all completed stamp strokes
                    foreach (var stroke in _sessionStrokes)
                    {
                        DrawStrokeInfoToCanvas(canvas, stroke);
                    }

                    // 2. Draw active stamp stroke
                    if (_currentStrokeInfo != null)
                    {
                        DrawStrokeInfoToCanvas(canvas, _currentStrokeInfo);
                    }

                    // 3. Draw completed vector strokes
                    for (int i = 0; i < _sessionPaths.Count; i++)
                    {
                        canvas.DrawPath(_sessionPaths[i], _sessionPaints[i]);
                    }

                    // 4. Draw active vector stroke
                    if (_currentStrokePath != null && _currentStrokePaint != null)
                    {
                        canvas.DrawPath(_currentStrokePath, _currentStrokePaint);
                    }
                }

                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    _brushOverlayBitmap.AddDirtyRect(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private void DrawStrokeInfoToCanvas(SkiaSharp.SKCanvas canvas, BrushStrokeInfo stroke)
        {
            if (stroke.Points.Count == 0) return;

            bool isComplexPreset = stroke.Preset != BrushPreset.RoundHard &&
                                   stroke.Preset != BrushPreset.RoundSoft &&
                                   stroke.Preset != BrushPreset.Airbrush &&
                                   stroke.Preset != BrushPreset.Pencil;

            double distanceAccumulator = 0;
            
            var activeLayer = _node.EditorDoc?.ActiveLayer;
            double scaleX = 1.0;
            if (activeLayer != null && MainImage.ActualWidth > 0)
            {
                scaleX = activeLayer.Width / MainImage.ActualWidth;
            }

            Action<double, double> drawStamp = (cx, cy) =>
            {
                if (isComplexPreset)
                {
                    double currentRadius = EditorPanel.BrushSize / 2.0;
                    if (_cachedBrushTip != null && stroke.Preset == _currentBrushPreset && stroke.Radius == currentRadius)
                    {
                        DrawCachedBrushTipStamp(canvas, cx, cy, stroke.IsEraser, stroke.Color);
                    }
                    else
                    {
                        DrawSkiaBrushStamp(canvas, cx, cy, stroke.Radius, stroke.Hardness, stroke.Flow, stroke.Color, stroke.Preset, stroke.IsEraser);
                    }
                }
            };

            if (isComplexPreset)
            {
                drawStamp(stroke.Points[0].X, stroke.Points[0].Y);

                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    DrawStrokeSegmentToCanvasHelper(canvas, stroke.Points[i - 1], stroke.Points[i], stroke.Preset, stroke.Radius, stroke.Hardness, stroke.Flow, stroke.Color, stroke.IsEraser, ref distanceAccumulator, drawStamp);
                }
            }
        }

        private void DrawStrokeSegmentToCanvasHelper(SkiaSharp.SKCanvas canvas, Point p1, Point p2, BrushPreset preset, double radius, double hardness, double flow, Color color, bool isEraser, ref double distanceAccumulator, Action<double, double> drawStamp)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            double step = GetBrushStep(preset, radius);

            if (len == 0)
            {
                drawStamp(p1.X, p1.Y);
                return;
            }

            double d = 0;
            while (d <= len)
            {
                double remainingToStep = step - distanceAccumulator;
                if (d + remainingToStep <= len)
                {
                    d += remainingToStep;
                    double cx = p1.X + (dx * d / len);
                    double cy = p1.Y + (dy * d / len);
                    drawStamp(cx, cy);
                    distanceAccumulator = 0;
                }
                else
                {
                    distanceAccumulator += (len - d);
                    break;
                }
            }
        }

        private void PreRenderBrushTip()
        {
            if (_cachedBrushTip != null)
            {
                _cachedBrushTip.Dispose();
                _cachedBrushTip = null;
            }

            double rawRadius = EditorPanel.BrushSize / 2.0;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;

            bool isComplexPreset = _currentBrushPreset != BrushPreset.RoundHard &&
                                   _currentBrushPreset != BrushPreset.RoundSoft &&
                                   _currentBrushPreset != BrushPreset.Airbrush &&
                                   _currentBrushPreset != BrushPreset.Pencil;

            if (!isComplexPreset) return;

            double extent = rawRadius * 10.0 + 10.0;
            int S = (int)Math.Ceiling(extent);
            if (S < 4) S = 4;

            _cachedBrushTip = new SkiaSharp.SKBitmap(S, S);
            using (var canvas = new SkiaSharp.SKCanvas(_cachedBrushTip))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);

                double cx = S / 2.0;
                double cy = S / 2.0;

                DrawSkiaBrushStamp(canvas, cx, cy, rawRadius, hardness, flow, Colors.White, _currentBrushPreset, false);
            }
        }

        private void DrawCachedBrushTipStamp(SkiaSharp.SKCanvas canvas, double cx, double cy, bool isEraser, Color color)
        {
            if (_cachedBrushTip == null) return;

            using (var paint = new SkiaSharp.SKPaint())
            {
                paint.IsAntialias = true;

                if (isEraser)
                {
                    paint.BlendMode = SkiaSharp.SKBlendMode.DstOut;
                }
                else
                {
                    paint.BlendMode = SkiaSharp.SKBlendMode.SrcOver;
                    var skColor = new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
                    paint.ColorFilter = SkiaSharp.SKColorFilter.CreateBlendMode(skColor, SkiaSharp.SKBlendMode.SrcIn);
                }

                float left = (float)(cx - _cachedBrushTip.Width / 2.0);
                float top = (float)(cy - _cachedBrushTip.Height / 2.0);
                canvas.DrawBitmap(_cachedBrushTip, left, top, paint);
            }
        }

        private SkiaSharp.SKPaint CreateSkiaStampPaint(double flow, Color color, bool isEraser)
        {
            var paint = new SkiaSharp.SKPaint
            {
                Style = SkiaSharp.SKPaintStyle.Fill,
                IsAntialias = true
            };
            if (isEraser)
            {
                paint.BlendMode = SkiaSharp.SKBlendMode.DstOut;
                byte alpha = (byte)Math.Clamp(255 * (flow / 100.0), 0, 255);
                paint.Color = new SkiaSharp.SKColor(0, 0, 0, alpha);
            }
            else
            {
                paint.BlendMode = SkiaSharp.SKBlendMode.SrcOver;
                byte alpha = (byte)Math.Clamp(color.A * (flow / 100.0), 0, 255);
                paint.Color = new SkiaSharp.SKColor(color.R, color.G, color.B, alpha);
            }
            return paint;
        }

        private void DrawSkiaBrushStamp(SkiaSharp.SKCanvas canvas, double cx, double cy, double radius, double hardness, double flow, Color color, BrushPreset preset, bool isEraser)
        {
            double r = radius;
            double flowMul = flow / 100.0;

            switch (preset)
            {
                case BrushPreset.RoundSoft:
                    {
                        using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                        {
                            float blurSigma = (float)(radius * 0.4);
                            paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                            canvas.DrawCircle((float)cx, (float)cy, (float)(radius - blurSigma), paint);
                        }
                    }
                    break;

                case BrushPreset.RoundHard:
                    {
                        using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                        {
                            float blurSigma = 0;
                            if (hardness < 100)
                            {
                                blurSigma = (float)(radius * (1.0 - hardness / 100.0) * 0.5);
                            }
                            if (blurSigma > 0.1f)
                            {
                                paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                            }
                            canvas.DrawCircle((float)cx, (float)cy, (float)(radius - blurSigma), paint);
                        }
                    }
                    break;

                case BrushPreset.Flat:
                    {
                        using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                        {
                            float blurSigma = 0;
                            if (hardness < 100)
                            {
                                blurSigma = (float)(radius * (1.0 - hardness / 100.0) * 0.5);
                            }
                            if (blurSigma > 0.1f)
                            {
                                paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                            }
                            
                            double halfW = radius - blurSigma;
                            double halfH = (radius / 3.0) - (blurSigma / 3.0);
                            halfH = Math.Max(0.1, halfH);
                            halfW = Math.Max(0.1, halfW);

                            var rect = new SkiaSharp.SKRect(
                                (float)(cx - halfW),
                                (float)(cy - halfH),
                                (float)(cx + halfW),
                                (float)(cy + halfH)
                            );
                            canvas.DrawRect(rect, paint);
                        }
                    }
                    break;

                case BrushPreset.Chalk:
                    {
                        double offsetMul = r * 6.0;
                        foreach (var offset in ChalkPresetOffsets)
                        {
                            double spotCx = cx + offset.x * offsetMul;
                            double spotCy = cy + offset.y * offsetMul;
                            double spotRadius = 0.5 + (r - 0.5) * (offset.size * 0.15);

                            using (var paint = CreateSkiaStampPaint(flow * 180.0 / 255.0, color, isEraser))
                            {
                                canvas.DrawCircle((float)spotCx, (float)spotCy, (float)spotRadius, paint);
                            }
                        }
                    }
                    break;

                case BrushPreset.Spray:
                    {
                        double offsetMul = r * 6.0;
                        foreach (var offset in SprayPresetOffsets)
                        {
                            double spotCx = cx + offset.x * offsetMul;
                            double spotCy = cy + offset.y * offsetMul;
                            double spotRadius = 0.5 + (r - 0.5) * 0.25;

                            double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                            double opacityScale = Math.Max(0.0, 1.0 - distRatio * 0.5);

                            using (var paint = CreateSkiaStampPaint(flow * opacityScale, color, isEraser))
                            {
                                canvas.DrawCircle((float)spotCx, (float)spotCy, (float)spotRadius, paint);
                            }
                        }
                    }
                    break;

                case BrushPreset.Scatter:
                    {
                        double offsetMul = r * 6.0;
                        foreach (var offset in ScatterPresetOffsets)
                        {
                            double blobCx = cx + offset.x * offsetMul;
                            double blobCy = cy + offset.y * offsetMul;
                            double blobRadius = 0.5 + (r - 0.5) * offset.scale;

                            using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                            {
                                float blurSigma = (float)(blobRadius * 0.5);
                                if (blurSigma > 0.1f)
                                {
                                    paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                                }
                                canvas.DrawCircle((float)blobCx, (float)blobCy, (float)(blobRadius - blurSigma), paint);
                            }
                        }
                    }
                    break;

                case BrushPreset.Pencil:
                    {
                        double pencilRadius = Math.Min(r, Math.Max(1.5, r * 0.5));
                        using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                        {
                            canvas.DrawCircle((float)cx, (float)cy, (float)pencilRadius, paint);
                        }
                    }
                    break;

                case BrushPreset.Airbrush:
                    {
                        using (var paint = CreateSkiaStampPaint(flow, color, isEraser))
                        {
                            float blurSigma = (float)(radius * 0.5);
                            paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                            canvas.DrawCircle((float)cx, (float)cy, (float)(radius - blurSigma), paint);
                        }
                    }
                    break;

                case BrushPreset.Splatter:
                    {
                        double offsetMul = r * 6.0;
                        foreach (var offset in SplatterPresetOffsets)
                        {
                            double spotCx = cx + offset.x * offsetMul;
                            double spotCy = cy + offset.y * offsetMul;
                            double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.4;

                            using (var paint = CreateSkiaStampPaint(flow * offset.opacity, color, isEraser))
                            {
                                canvas.DrawCircle((float)spotCx, (float)spotCy, (float)spotRadius, paint);
                            }
                        }
                    }
                    break;

                case BrushPreset.Charcoal:
                    {
                        double offsetMul = r * 4.0;
                        foreach (var offset in CharcoalPresetOffsets)
                        {
                            double spotCx = cx + offset.x * offsetMul;
                            double spotCy = cy + offset.y * offsetMul;
                            double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.45;

                            using (var paint = CreateSkiaStampPaint(flow * offset.opacity, color, isEraser))
                            {
                                float blurSigma = (float)(spotRadius * 0.4);
                                if (blurSigma > 0.1f)
                                {
                                    paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                                }
                                canvas.DrawCircle((float)spotCx, (float)spotCy, (float)(spotRadius - blurSigma), paint);
                            }
                        }
                    }
                    break;

                case BrushPreset.OilBrush:
                    {
                        double offsetMul = r * 3.5;
                        foreach (var offset in OilBrushPresetOffsets)
                        {
                            double spotCx = cx + offset.x * offsetMul;
                            double spotCy = cy + offset.y * offsetMul;
                            double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.25;

                            double bristleFlow = 0.6 + 0.4 * (Math.Abs(Math.Sin(offset.x * 37.13 + offset.y * 53.45) * 1000.0) % 1.0);

                            using (var paint = CreateSkiaStampPaint(flow * 0.95 * bristleFlow, color, isEraser))
                            {
                                float blurSigma = (float)(spotRadius * 0.2);
                                if (blurSigma > 0.1f)
                                {
                                    paint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                                }
                                canvas.DrawCircle((float)spotCx, (float)spotCy, (float)(spotRadius - blurSigma), paint);
                            }
                        }
                    }
                    break;
            }
        }

        private void UndoLastBrushStroke()
        {
            if (_strokeIsComplexHistory.Count == 0 || _brushOverlayBitmap == null) return;

            int lastHistoryIdx = _strokeIsComplexHistory.Count - 1;
            bool isComplex = _strokeIsComplexHistory[lastHistoryIdx];
            _strokeIsComplexHistory.RemoveAt(lastHistoryIdx);

            if (isComplex)
            {
                if (_sessionStrokes.Count > 0)
                {
                    _sessionStrokes.RemoveAt(_sessionStrokes.Count - 1);
                }
            }
            else
            {
                if (_sessionPaths.Count > 0)
                {
                    int lastIdx = _sessionPaths.Count - 1;
                    _sessionPaths[lastIdx].Dispose();
                    _sessionPaths.RemoveAt(lastIdx);
                }
                if (_sessionPaints.Count > 0)
                {
                    _sessionPaints.RemoveAt(_sessionPaints.Count - 1);
                }
            }

            RedrawBrushOverlay();

            if (_sessionPaths.Count == 0 && _sessionStrokes.Count == 0)
            {
                _brushOverlayBitmap = null;
                ActiveLayerDrawingOverlay.Source = null;
                ActiveLayerDrawingOverlay.Visibility = Visibility.Collapsed;
            }

            MarkCompositeDirty();
        }

        private SkiaSharp.SKPaint GetCachedEraserPaint(double radius, double hardness)
        {
            if (_cachedEraserPaint == null)
            {
                _cachedEraserPaint = new SkiaSharp.SKPaint();
                _cachedEraserPaint.Style = SkiaSharp.SKPaintStyle.Stroke;
                _cachedEraserPaint.StrokeCap = SkiaSharp.SKStrokeCap.Round;
                _cachedEraserPaint.StrokeJoin = SkiaSharp.SKStrokeJoin.Round;
                _cachedEraserPaint.IsAntialias = true;
            }

            // 20% opacity white guide color
            _cachedEraserPaint.Color = new SkiaSharp.SKColor(255, 255, 255, 51);

            float blurSigma = 0;
            if (_currentBrushPreset == BrushPreset.RoundSoft || _currentBrushPreset == BrushPreset.Airbrush)
                blurSigma = (float)(radius * 0.4);
            else if (_currentBrushPreset == BrushPreset.RoundHard && hardness < 100)
                blurSigma = (float)(radius * (1.0 - hardness / 100.0) * 0.5);

            double drawRadius = Math.Max(0.1, radius - blurSigma);
            _cachedEraserPaint.StrokeWidth = (float)(drawRadius * 2);

            float newSigma = blurSigma > 0.1f ? blurSigma : 0;
            if (newSigma != _cachedEraserBlurSigma)
            {
                _cachedEraserPaint.MaskFilter?.Dispose();
                _cachedEraserPaint.MaskFilter = newSigma > 0 ? SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, newSigma) : null;
                _cachedEraserBlurSigma = newSigma;
            }

            return _cachedEraserPaint;
        }

        private void DrawEraserStampToOverlay(double cx, double cy, double rawRadius, double hardness)
        {
            if (_brushOverlayBitmap == null) return;
            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(_brushOverlayBitmap.PixelWidth, _brushOverlayBitmap.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    if (surface != null)
                    {
                        var canvas = surface.Canvas;
                        var paint = GetCachedEraserPaint(rawRadius, hardness);
                        paint.Style = SkiaSharp.SKPaintStyle.Fill;

                        float blurSigma = 0;
                        if (_currentBrushPreset == BrushPreset.RoundSoft || _currentBrushPreset == BrushPreset.Airbrush)
                            blurSigma = (float)(rawRadius * 0.4);
                        else if (_currentBrushPreset == BrushPreset.RoundHard && hardness < 100)
                            blurSigma = (float)(rawRadius * (1.0 - hardness / 100.0) * 0.5);

                        canvas.DrawCircle((float)cx, (float)cy, (float)(rawRadius - blurSigma), paint);
                    }
                }

                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    _brushOverlayBitmap.AddDirtyRect(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private void DrawEraserSegmentToOverlay(Point p1, Point p2, double radius, double hardness)
        {
            if (_brushOverlayBitmap == null) return;
            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(_brushOverlayBitmap.PixelWidth, _brushOverlayBitmap.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    if (surface != null)
                    {
                        var canvas = surface.Canvas;
                        var paint = GetCachedEraserPaint(radius, hardness);
                        paint.Style = SkiaSharp.SKPaintStyle.Stroke;

                        canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, paint);
                    }
                }

                int dirtyW = _prevSegmentMaxX - _prevSegmentMinX + 1;
                int dirtyH = _prevSegmentMaxY - _prevSegmentMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    _brushOverlayBitmap.AddDirtyRect(new Int32Rect(_prevSegmentMinX, _prevSegmentMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private void RenderEraserStrokeToLayer(EditorLayer activeLayer)
        {
            double radius = EditorPanel.BrushSize / 2.0;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;

            activeLayer.Bitmap.Lock();
            try
            {
                var surfaceInfo = new SkiaSharp.SKImageInfo(activeLayer.Width, activeLayer.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(surfaceInfo, activeLayer.Bitmap.BackBuffer, activeLayer.Bitmap.BackBufferStride))
                {
                    if (surface != null)
                    {
                        var canvas = surface.Canvas;
                        if (_localSelectionClipPath != null)
                        {
                            canvas.ClipPath(_localSelectionClipPath, SkiaSharp.SKClipOperation.Intersect, true);
                        }

                        using (var paint = new SkiaSharp.SKPaint())
                        {
                            paint.Style = SkiaSharp.SKPaintStyle.Stroke;
                            paint.StrokeCap = SkiaSharp.SKStrokeCap.Round;
                            paint.StrokeJoin = SkiaSharp.SKStrokeJoin.Round;
                            paint.IsAntialias = true;
                            paint.BlendMode = SkiaSharp.SKBlendMode.DstOut;

                            float blurSigma = 0;
                            if (_currentBrushPreset == BrushPreset.RoundSoft || _currentBrushPreset == BrushPreset.Airbrush)
                                blurSigma = (float)(radius * 0.4);
                            else if (_currentBrushPreset == BrushPreset.RoundHard && hardness < 100)
                                blurSigma = (float)(radius * (1.0 - hardness / 100.0) * 0.5);

                            double drawRadius = Math.Max(0.1, radius - blurSigma);
                            paint.StrokeWidth = (float)(drawRadius * 2);
                            byte alpha = (byte)Math.Clamp(255 * (flow / 100.0), 0, 255);
                            paint.Color = new SkiaSharp.SKColor(0, 0, 0, alpha);
                            paint.MaskFilter = blurSigma > 0.1 ? SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma) : null;

                            if (_strokePoints.Count > 0)
                            {
                                using (var fillPaint = new SkiaSharp.SKPaint())
                                {
                                    fillPaint.Style = SkiaSharp.SKPaintStyle.Fill;
                                    fillPaint.IsAntialias = true;
                                    fillPaint.BlendMode = SkiaSharp.SKBlendMode.DstOut;
                                    fillPaint.Color = paint.Color;
                                    fillPaint.MaskFilter = paint.MaskFilter;
                                    canvas.DrawCircle((float)_strokePoints[0].X, (float)_strokePoints[0].Y, (float)radius, fillPaint);
                                }
                            }

                            if (_strokePoints.Count > 1)
                            {
                                using (var path = new SkiaSharp.SKPath())
                                {
                                    path.MoveTo((float)_strokePoints[0].X, (float)_strokePoints[0].Y);
                                    for (int i = 1; i < _strokePoints.Count; i++)
                                    {
                                        path.LineTo((float)_strokePoints[i].X, (float)_strokePoints[i].Y);
                                    }
                                    canvas.DrawPath(path, paint);
                                }
                            }
                        }
                    }
                }

                // Add the entire stroke dirty rect
                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    activeLayer.Bitmap.AddDirtyRect(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH));
                }
            }
            finally
            {
                activeLayer.Bitmap.Unlock();
            }
        }

        private void ClearBrushOverlay()
        {
            if (_brushOverlayBitmap == null) return;
            _brushOverlayBitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(_brushOverlayBitmap.PixelWidth, _brushOverlayBitmap.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride))
                {
                    if (surface != null)
                    {
                        surface.Canvas.Clear(SkiaSharp.SKColors.Transparent);
                    }
                }
                _brushOverlayBitmap.AddDirtyRect(new Int32Rect(0, 0, _brushOverlayBitmap.PixelWidth, _brushOverlayBitmap.PixelHeight));
            }
            finally
            {
                _brushOverlayBitmap.Unlock();
            }
        }

        private SkiaSharp.SKPath? ConvertGeometryToSKPath(Geometry? geometry, double translateX = 0, double translateY = 0)
        {
            if (geometry == null) return null;
            try
            {
                Geometry bakedGeom = geometry;
                if (translateX != 0 || translateY != 0 || (geometry.Transform != null && !geometry.Transform.Value.IsIdentity))
                {
                    TransformGroup tg = new TransformGroup();
                    if (geometry.Transform != null) tg.Children.Add(geometry.Transform);
                    if (translateX != 0 || translateY != 0) tg.Children.Add(new TranslateTransform(translateX, translateY));
                    
                    var cloned = geometry.Clone();
                    cloned.Transform = tg;
                    bakedGeom = cloned.GetFlattenedPathGeometry();
                }
                var flatGeom = bakedGeom as PathGeometry ?? PathGeometry.CreateFromGeometry(bakedGeom);
                var svgData = flatGeom.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (svgData.StartsWith("F0") || svgData.StartsWith("F1"))
                {
                    svgData = svgData.Substring(2).Trim();
                }
                return SkiaSharp.SKPath.ParseSvgPathData(svgData);
            }
            catch
            {
                return null;
            }
        }
    }
}
