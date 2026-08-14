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

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {

        private bool _isDrawingPixels;
        private int _prevSegmentMinX;
        private int _prevSegmentMinY;
        private int _prevSegmentMaxX;
        private int _prevSegmentMaxY;
        private readonly List<Point> _strokePoints = new();
        private Point _lastDrawingPixelPoint;
        private BrushPreset _currentBrushPreset = BrushPreset.RoundHard;
        private readonly Random _brushRng = new();
        private int _strokeMinX;
        private int _strokeMinY;
        private int _strokeMaxX;
        private int _strokeMaxY;

        private WriteableBitmap? _brushOverlayBitmap;
        private readonly List<SkiaSharp.SKPath> _sessionPaths = new();
        private readonly List<SkiaSharp.SKPaint> _sessionPaints = new();
        private SkiaSharp.SKPath? _currentStrokePath;
        private SkiaSharp.SKPaint? _currentStrokePaint;
        private EditorLayer? _brushSessionLayer;
        private SkiaSharp.SKPath? _activeSelectionPathSK;
        private bool _isCommitingMove;

        private class BrushStrokeInfo
        {
            public List<Point> Points { get; set; } = new();
            public BrushPreset Preset { get; set; }
            public double Radius { get; set; }
            public double Hardness { get; set; }
            public double Flow { get; set; }
            public Color Color { get; set; }
            public bool IsEraser { get; set; }
        }

        private readonly List<BrushStrokeInfo> _sessionStrokes = new();
        private BrushStrokeInfo? _currentStrokeInfo;
        private readonly List<bool> _strokeIsComplexHistory = new();
        private double _brushDistanceAccumulator = 0;
        private SkiaSharp.SKBitmap? _cachedBrushTip;
        private SkiaSharp.SKPaint? _cachedEraserPaint;
        private float _cachedEraserBlurSigma = -1;
        private SkiaSharp.SKPath? _localSelectionClipPath;
        private SkiaSharp.SKBitmap? _moveBgPlateSK;
        private SkiaSharp.SKBitmap? _moveFgPlateSK;
        private SkiaSharp.SKBitmap? _moveActiveLayerSK;

        // ── SkiaSharp cached surfaces for eraser performance ──
        // Pre-composited background (all non-active layers) cached at mouse-down.
        // During eraser moves, we composite only: bg plate + active layer = 2-layer op instead of N-layer.
        private WriteableBitmap? _eraserBgPlate;
        private SkiaSharp.SKBitmap? _eraserBgPlateSK;

        // ── Batched Lock/Unlock: lock bitmap once at MouseDown, keep locked, unlock at MouseUp ──
        private bool _eraserBitmapLocked;
        private SkiaSharp.SKSurface? _eraserLockedSurface;

        private static readonly (double x, double y, double size)[] ChalkPresetOffsets = new (double x, double y, double size)[]
        {
            (-0.4, -0.3, 1.8),
            (0.3, -0.6, 2.2),
            (-0.1, 0.5, 1.5),
            (0.5, 0.4, 2.5),
            (-0.6, 0.2, 2.0),
            (0.2, 0.1, 1.6),
            (-0.3, -0.7, 2.1),
            (0.6, -0.2, 1.9),
            (-0.2, 0.3, 2.3),
            (0.1, -0.4, 1.7),
            (0.4, 0.6, 2.4),
            (-0.5, -0.5, 1.5),
            (0.0, 0.0, 2.0),
            (0.3, 0.3, 1.8),
            (-0.1, -0.2, 2.2)
        };

        private static readonly (double x, double y)[] SprayPresetOffsets = new (double x, double y)[]
        {
            (-0.2, 0.1), (0.3, -0.4), (-0.1, 0.5), (0.6, 0.2), (-0.5, -0.3),
            (0.1, 0.2), (-0.3, -0.6), (0.4, 0.5), (-0.2, -0.1), (0.5, -0.3),
            (-0.4, 0.4), (0.2, -0.2), (-0.6, -0.1), (0.3, 0.3), (-0.1, -0.4),
            (0.1, 0.7), (-0.3, 0.1), (0.4, -0.7), (-0.5, 0.5), (0.2, 0.4),
            (-0.2, -0.5), (0.7, -0.1), (-0.4, -0.4), (0.1, -0.1), (-0.1, 0.3),
            (0.5, 0.5), (-0.3, 0.3), (0.3, -0.5), (-0.5, 0.2), (0.0, 0.0)
        };

        private static readonly (double x, double y, double scale)[] ScatterPresetOffsets = new (double x, double y, double scale)[]
        {
            (-0.3, -0.2, 0.3),
            (0.4, -0.5, 0.45),
            (-0.1, 0.6, 0.35),
            (0.5, 0.3, 0.5),
            (-0.6, 0.4, 0.4)
        };

        private static readonly (double x, double y, double size, double opacity)[] SplatterPresetOffsets = new (double x, double y, double size, double opacity)[]
        {
            (0.0, 0.0, 1.0, 1.0),      // Central main droplet
            (0.15, -0.1, 0.35, 0.8),   // Medium droplets close to center
            (-0.12, 0.18, 0.3, 0.8),
            (0.35, 0.25, 0.2, 0.6),    // Splash droplets further away
            (-0.3, -0.4, 0.25, 0.7),
            (0.5, -0.3, 0.15, 0.5),    // Tiny outer flecks
            (-0.45, -0.15, 0.12, 0.4),
            (0.2, 0.5, 0.18, 0.5),
            (-0.1, -0.6, 0.1, 0.4),
            (0.6, 0.4, 0.08, 0.3),
        };

        private static readonly (double x, double y, double size, double opacity)[] CharcoalPresetOffsets = new (double x, double y, double size, double opacity)[]
        {
            (0.0, 0.0, 0.6, 0.8),
            (-0.2, -0.1, 0.4, 0.6),
            (0.2, 0.1, 0.4, 0.6),
            (-0.1, 0.3, 0.5, 0.7),
            (0.1, -0.3, 0.5, 0.7),
            (-0.4, -0.3, 0.3, 0.4),
            (0.4, 0.3, 0.3, 0.4),
            (-0.3, 0.2, 0.3, 0.4),
            (0.3, -0.2, 0.3, 0.4),
            (-0.5, 0.0, 0.2, 0.3),
            (0.5, 0.0, 0.2, 0.3)
        };

        private static readonly (double x, double y, double size)[] OilBrushPresetOffsets = new (double x, double y, double size)[]
        {
            (-0.6, 0.0, 0.5), (-0.4, -0.1, 0.6), (-0.2, 0.0, 0.7), (0.0, 0.1, 0.8), (0.2, 0.0, 0.7), (0.4, -0.1, 0.6), (0.6, 0.0, 0.5),
            (-0.5, 0.2, 0.4), (-0.3, 0.1, 0.5), (-0.1, 0.2, 0.6), (0.1, 0.2, 0.6), (0.3, 0.1, 0.5), (0.5, 0.2, 0.4)
        };

        // ── Throttled composite during drawing (avoid lag) ──
        private DispatcherTimer? _compositeTimer;
        private bool _compositeDirty;

        private bool _isKeyMoving;
        private int _keyDeltaX;
        private int _keyDeltaY;
        private DispatcherTimer? _keyMoveCommitTimer;

        // ── Selection marching ants animation (WPF composition thread) ──
        private DoubleAnimation? _marchingAntsAnimation;

        /// <summary>Đánh dấu cần composite lại — timer sẽ xử lý ở tick tiếp theo (~30fps).</summary>
        private void MarkCompositeDirty()
        {
            bool useDirectSource = (_node?.EditorDoc != null && 
                                    _node.EditorDoc.Layers.Count == 1 &&
                                    _node.EditorDoc.ActiveLayer != null &&
                                    _node.EditorDoc.ActiveLayer.Opacity >= 0.99 &&
                                    _node.EditorDoc.ActiveLayer.BlendMode == BlendMode.Normal);

            if (_isDrawingPixels)
            {
                if (useDirectSource)
                {
                    // Nếu dùng DirectSource, WPF tự cập nhật qua AddDirtyRect cực mượt, không cần composite chậm chạp
                    return;
                }

                if (EditorPanel?.ActiveToolName != "Eraser" || _eraserBgPlateSK == null)
                {
                    // Brush or overlay-based Eraser: updates via GPU Overlay
                    return;
                }

                // For multi-layer eraser: use fast 2-layer composite with cached background plate
                if (EditorPanel?.ActiveToolName == "Eraser" && _eraserBgPlateSK != null)
                {
                    _compositeDirty = true;
                    if (_compositeTimer == null)
                    {
                        _compositeTimer = new DispatcherTimer(DispatcherPriority.Render)
                        {
                            Interval = TimeSpan.FromMilliseconds(16)
                        };
                        _compositeTimer.Tick += CompositeTimer_Tick;
                    }
                    if (!_compositeTimer.IsEnabled)
                        _compositeTimer.Start();
                    return;
                }
            }

            _compositeDirty = true;
            if (_compositeTimer == null)
            {
                _compositeTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(16) // 60 FPS for silky-smooth drawing/erasing preview
                };
                _compositeTimer.Tick += CompositeTimer_Tick;
            }
            if (!_compositeTimer.IsEnabled)
                _compositeTimer.Start();
        }

        private void CompositeTimer_Tick(object? sender, EventArgs e)
        {
            if (!_compositeDirty)
            {
                _compositeTimer?.Stop();
                return;
            }
            _compositeDirty = false;
            DoLightweightComposite();
        }

        /// <summary>Composite nhẹ chỉ để hiển thị — không update thumbnail, không fire event.</summary>
        private void DoLightweightComposite()
        {
            if (_node?.EditorDoc == null) return;
            try
            {
                var activeLayer = _node.EditorDoc.ActiveLayer;

                // Fast path: if eraser with cached background plate, do 2-layer composite
                if (_isDrawingPixels && _eraserBgPlateSK != null && activeLayer != null)
                {
                    DoFastEraserComposite();
                    return;
                }

                // Fast path: if moving a layer, do fast Move composite using pre-composited plates
                if (_isMovingLayer && activeLayer != null)
                {
                    DoFastMoveComposite(activeLayer);
                    return;
                }

                // Fast path: dirty region composite khi đang vẽ (brush/eraser/move)
                if (_isDrawingPixels && activeLayer != null && _strokeMaxX >= _strokeMinX && _strokeMaxY >= _strokeMinY)
                {
                    int margin = 4; // padding nhỏ để tránh artifact ở cạnh
                    int w = _node.EditorDoc.Width;
                    int h = _node.EditorDoc.Height;

                    // Offset dirty region từ local layer coords về document coords
                    int clipOffX = activeLayer.OffsetX;
                    int clipOffY = activeLayer.OffsetY;

                    int rx = Math.Max(0, _prevSegmentMinX + clipOffX - margin);
                    int ry = Math.Max(0, _prevSegmentMinY + clipOffY - margin);
                    int rr = Math.Min(w, _prevSegmentMaxX + clipOffX + margin + 1);
                    int rb = Math.Min(h, _prevSegmentMaxY + clipOffY + margin + 1);
                    if (rr > rx && rb > ry)
                    {
                        var dirtyRect = new Int32Rect(rx, ry, rr - rx, rb - ry);
                        var composite = _node.EditorDoc.CompositeRegion(dirtyRect);
                        MainImage.Source = composite;
                        return;
                    }
                }

                var fullComposite = _node.EditorDoc.Composite();
                MainImage.Source = fullComposite;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("=== DoLightweightComposite EXCEPTION ===\n" + ex);
            }
        }

        /// <summary>Fast 2-layer composite: cached bg plate + active layer. Avoids full N-layer composite.</summary>
        private void DoFastEraserComposite()
        {
            var activeLayer = _node.EditorDoc!.ActiveLayer!;
            int w = _node.EditorDoc.Width;
            int h = _node.EditorDoc.Height;

            if (_eraserBgPlate == null || _eraserBgPlate.PixelWidth != w || _eraserBgPlate.PixelHeight != h)
                return;

            // We'll render into the existing cached CPU render target
            var target = _node.EditorDoc.GetCachedCpuRenderTarget();
            if (target == null) return;

            target.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, target.BackBuffer, target.BackBufferStride))
                {
                    if (surface == null) return;
                    var canvas = surface.Canvas;

                    // 1. Draw cached background plate (all non-active layers)
                    canvas.DrawBitmap(_eraserBgPlateSK!, 0, 0);

                    // 2. Draw active layer on top
                    activeLayer.Bitmap.Lock();
                    try
                    {
                        var activeInfo = new SkiaSharp.SKImageInfo(activeLayer.Width, activeLayer.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var activeSK = new SkiaSharp.SKBitmap())
                        {
                            activeSK.InstallPixels(activeInfo, activeLayer.Bitmap.BackBuffer, activeLayer.Bitmap.BackBufferStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.Color = new SkiaSharp.SKColor(255, 255, 255, (byte)Math.Clamp(activeLayer.Opacity * 255, 0, 255));
                                paint.BlendMode = activeLayer.BlendMode switch
                                {
                                    BlendMode.Multiply => SkiaSharp.SKBlendMode.Multiply,
                                    BlendMode.Screen => SkiaSharp.SKBlendMode.Screen,
                                    BlendMode.Overlay => SkiaSharp.SKBlendMode.Overlay,
                                    BlendMode.Darken => SkiaSharp.SKBlendMode.Darken,
                                    BlendMode.Lighten => SkiaSharp.SKBlendMode.Lighten,
                                    _ => SkiaSharp.SKBlendMode.SrcOver
                                };
                                canvas.DrawBitmap(activeSK, activeLayer.OffsetX, activeLayer.OffsetY, paint);
                            }
                        }
                    }
                    finally
                    {
                        activeLayer.Bitmap.Unlock();
                    }
                }

                target.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally
            {
                target.Unlock();
            }

            MainImage.Source = target;
        }

        /// <summary>Pre-composite all layers except activeLayer into a cached SKBitmap for fast eraser preview.</summary>
        private void BuildEraserBackgroundPlate(EditorLayer activeLayer)
        {
            if (_node?.EditorDoc == null) return;
            int w = _node.EditorDoc.Width;
            int h = _node.EditorDoc.Height;

            // Dispose previous
            _eraserBgPlateSK?.Dispose();
            _eraserBgPlateSK = null;

            var bgBitmap = new SkiaSharp.SKBitmap(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using (var bgCanvas = new SkiaSharp.SKCanvas(bgBitmap))
            {
                bgCanvas.Clear(SkiaSharp.SKColors.Transparent);

                foreach (var layer in _node.EditorDoc.Layers)
                {
                    if (layer == activeLayer) continue; // skip active layer
                    if (!layer.IsVisible || layer.Opacity <= 0 || layer.IsTempHidden) continue;

                    var srcBmp = layer.Bitmap;
                    srcBmp.Lock();
                    try
                    {
                        var srcInfo = new SkiaSharp.SKImageInfo(srcBmp.PixelWidth, srcBmp.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var skBmp = new SkiaSharp.SKBitmap())
                        {
                            skBmp.InstallPixels(srcInfo, srcBmp.BackBuffer, srcBmp.BackBufferStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.Color = new SkiaSharp.SKColor(255, 255, 255, (byte)Math.Clamp(layer.Opacity * 255, 0, 255));
                                paint.BlendMode = layer.BlendMode switch
                                {
                                    BlendMode.Multiply => SkiaSharp.SKBlendMode.Multiply,
                                    BlendMode.Screen => SkiaSharp.SKBlendMode.Screen,
                                    BlendMode.Overlay => SkiaSharp.SKBlendMode.Overlay,
                                    BlendMode.Darken => SkiaSharp.SKBlendMode.Darken,
                                    BlendMode.Lighten => SkiaSharp.SKBlendMode.Lighten,
                                    _ => SkiaSharp.SKBlendMode.SrcOver
                                };
                                bgCanvas.DrawBitmap(skBmp, layer.OffsetX, layer.OffsetY, paint);
                            }
                        }
                    }
                    finally
                    {
                        srcBmp.Unlock();
                    }
                }
            }

            _eraserBgPlateSK = bgBitmap;
            // Keep a WPF reference for size checking
            _eraserBgPlate = new WriteableBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
        }

        /// <summary>Dừng timer, flush composite cuối cùng + sync thumbnail + fire event.</summary>
        private void FlushCompositeAndSync()
        {
            _compositeTimer?.Stop();
            _compositeDirty = false;

            // Sync thumbnail cho active layer
            _node?.EditorDoc?.ActiveLayer?.InvalidateThumbnail();

            // Full composite + fire event
            OnEditorDocumentModified();
        }

        public enum SelectionMode
        {
            New,
            Add,
            Subtract
        }
        private SelectionMode _currentSelectionMode = SelectionMode.New;
        private Geometry? _activeSelectionGeometry;

        private bool _isSelecting;
        private Point _selectionStartPoint;
        private Rect? _selectionRect;
        private readonly System.Collections.Generic.List<Point> _selectionPoints = new();
        private bool[,]? _cachedSelectionMask;
        private int _cachedSelectionStartX;
        private int _cachedSelectionStartY;
        private int _cachedSelectionEndX;
        private int _cachedSelectionEndY;
        private bool _hasCachedSelectionMask;

        private bool _isMovingLayer;
        private Point _moveStartMousePos;
        private Geometry? _moveInitialGeometry;
        private byte[]? _moveInitialFullPixels;
        private EditorLayer? _movingLayer;
        private double _accumulatedMoveDx = 0;
        private double _accumulatedMoveDy = 0;

        private void DoFastMoveComposite(EditorLayer activeLayer)
        {
            if (_node?.EditorDoc == null) return;
            int w = _node.EditorDoc.Width;
            int h = _node.EditorDoc.Height;

            var target = _node.EditorDoc.GetCachedCpuRenderTarget();
            if (target == null) return;

            target.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, target.BackBuffer, target.BackBufferStride))
                {
                    if (surface != null)
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SkiaSharp.SKColors.Transparent);

                        // 1. Draw cached background plate (all layers below active layer)
                        if (_moveBgPlateSK != null)
                        {
                            canvas.DrawBitmap(_moveBgPlateSK, 0, 0);
                        }

                        // 2. Draw active layer on top
                        if (activeLayer.IsVisible && activeLayer.Opacity > 0 && !activeLayer.IsTempHidden)
                        {
                            if (activeLayer.IsTextLayer)
                            {
                                _node.EditorDoc.DrawTextLayerToCanvas(canvas, activeLayer);
                            }
                            else if (_moveActiveLayerSK != null)
                            {
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.IsAntialias = true;
                                    paint.Color = new SkiaSharp.SKColor(255, 255, 255, (byte)Math.Clamp(activeLayer.Opacity * 255, 0, 255));
                                    paint.BlendMode = activeLayer.BlendMode switch
                                    {
                                        BlendMode.Multiply => SkiaSharp.SKBlendMode.Multiply,
                                        BlendMode.Screen => SkiaSharp.SKBlendMode.Screen,
                                        BlendMode.Overlay => SkiaSharp.SKBlendMode.Overlay,
                                        BlendMode.Darken => SkiaSharp.SKBlendMode.Darken,
                                        BlendMode.Lighten => SkiaSharp.SKBlendMode.Lighten,
                                        _ => SkiaSharp.SKBlendMode.SrcOver
                                    };

                                    canvas.Save();

                                    // Handle selection clipping for move preview if selection layer
                                    if (activeLayer.TempSelectionGeometry != null && activeLayer.TempSelectionPath != null)
                                    {
                                        // Standard selection layer move clipping
                                        // Draw unshifted layer outside selection first
                                        using (var clipPath = ConvertGeometryToSKPath(activeLayer.TempSelectionGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY))
                                        {
                                            if (clipPath != null)
                                            {
                                                canvas.Save();
                                                canvas.ClipPath(clipPath, SkiaSharp.SKClipOperation.Difference, true);
                                                canvas.DrawBitmap(_moveActiveLayerSK, activeLayer.OffsetX, activeLayer.OffsetY, paint);
                                                canvas.Restore();
                                            }
                                        }

                                        // Draw shifted layer inside selection
                                        canvas.ClipPath(activeLayer.TempSelectionPath, SkiaSharp.SKClipOperation.Intersect, true);
                                        double totalDx = activeLayer.OffsetX + activeLayer.TempMoveDx;
                                        double totalDy = activeLayer.OffsetY + activeLayer.TempMoveDy;
                                        canvas.DrawBitmap(_moveActiveLayerSK, (float)totalDx, (float)totalDy, paint);
                                    }
                                    else
                                    {
                                        double totalDx = activeLayer.OffsetX + activeLayer.TempMoveDx;
                                        double totalDy = activeLayer.OffsetY + activeLayer.TempMoveDy;
                                        canvas.DrawBitmap(_moveActiveLayerSK, (float)totalDx, (float)totalDy, paint);
                                    }

                                    canvas.Restore();
                                }
                            }
                        }

                        // 3. Draw cached foreground plate (all layers above active layer)
                        if (_moveFgPlateSK != null)
                        {
                            canvas.DrawBitmap(_moveFgPlateSK, 0, 0);
                        }
                    }
                }

                target.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally
            {
                target.Unlock();
            }

            MainImage.Source = target;
        }
    }
}
