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
        /// <summary>Clip rect for brush/eraser drawing, based on layer's ContentBounds at draw start.
        /// Empty means no clipping (draw on entire layer).</summary>
        private Rect _brushClipRect = Rect.Empty;
        /// <summary>Layer mà _brushClipRect thuộc về, dùng để detect đổi layer.</summary>
        private EditorLayer? _brushClipLayer;

        private void HandleManualEditorMouseDown(MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked || !activeLayer.IsVisible) return;

            string tool = EditorPanel.ActiveToolName;
            var clickPos = e.GetPosition(MainImage);

            if (tool != "Move" && tool != "Transform")
            {
                if (activeLayer.OriginalTransformBitmap != null || activeLayer.ContentGeometry != null)
                {
                    int stride = activeLayer.Width * 4;
                    byte[] pixels = new byte[stride * activeLayer.Height];
                    activeLayer.Bitmap.CopyPixels(pixels, stride, 0);

                    var rasterizeCmd = new PixelEditCommand(activeLayer, pixels, pixels);
                    rasterizeCmd.KeepOriginalTransformBitmap = false;
                    _node.EditorDoc.History.Execute(rasterizeCmd);
                }

                // Luôn dùng ImageContentBounds (persistent, không bị clear bởi commands)
                _brushClipRect = activeLayer.ImageContentBounds;
                _brushClipLayer = activeLayer;
            }

            if (tool != "Text" && TextMoveContainer != null && TextMoveContainer.Visibility == Visibility.Visible)
            {
                CommitActiveText();
            }

            // Allow selection tools to click outside the image bounds
            bool isSelectionTool = (tool == "Selection" || tool == "Lasso" || tool == "PolyLasso" || tool == "Slice");
            if (!isSelectionTool)
            {
                if (clickPos.X < 0 || clickPos.X > MainImage.ActualWidth ||
                    clickPos.Y < 0 || clickPos.Y > MainImage.ActualHeight)
                    return;
            }

            double scaleX = _node.EditorDoc.Width / MainImage.ActualWidth;
            double scaleY = _node.EditorDoc.Height / MainImage.ActualHeight;
            int rawPx = (int)(clickPos.X * scaleX);
            int rawPy = (int)(clickPos.Y * scaleY);

            int px = Math.Clamp(rawPx, 0, _node.EditorDoc.Width - 1);
            int py = Math.Clamp(rawPy, 0, _node.EditorDoc.Height - 1);

            if (tool == "Move")
            {
                CommitKeyMoveSession();

                // Build background and foreground plates once at mouse down
                _moveBgPlateSK?.Dispose();
                _moveBgPlateSK = null;
                _moveFgPlateSK?.Dispose();
                _moveFgPlateSK = null;
                _node.EditorDoc.BuildMovePlates(activeLayer, out _moveBgPlateSK, out _moveFgPlateSK);

                // Cache active layer bitmap once at mouse down
                _moveActiveLayerSK?.Dispose();
                _moveActiveLayerSK = null;
                if (!activeLayer.IsTextLayer)
                {
                    var activeBmp = activeLayer.ActiveChildLayer != null ? activeLayer.ActiveChildLayer.Bitmap : activeLayer.Bitmap;
                    activeBmp.Lock();
                    try
                    {
                        var info = new SkiaSharp.SKImageInfo(activeBmp.PixelWidth, activeBmp.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var tempSK = new SkiaSharp.SKBitmap())
                        {
                            tempSK.InstallPixels(info, activeBmp.BackBuffer, activeBmp.BackBufferStride);
                            
                            var copy = new SkiaSharp.SKBitmap(activeBmp.PixelWidth, activeBmp.PixelHeight);
                            using (var canvas = new SkiaSharp.SKCanvas(copy))
                            {
                                canvas.Clear(SkiaSharp.SKColors.Transparent);
                                canvas.DrawBitmap(tempSK, 0, 0);
                            }
                            _moveActiveLayerSK = copy;
                        }
                    }
                    finally
                    {
                        activeBmp.Unlock();
                    }
                }

                _isMovingLayer = true;
                _movingLayer = activeLayer;
                _moveStartMousePos = clickPos;


                int w = activeLayer.Width;
                int h = activeLayer.Height;
                int strideValue = w * 4;

                if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
                {
                    _moveInitialFullPixels = new byte[strideValue * h];
                    activeLayer.Bitmap.CopyPixels(_moveInitialFullPixels, strideValue, 0);
                    _moveInitialGeometry = _activeSelectionGeometry.Clone();
                    activeLayer.TempSelectionGeometry = _activeSelectionGeometry.Clone();
                    
                    // Convert _moveInitialGeometry to SKPath once
                    try
                    {
                        var flatGeom = PathGeometry.CreateFromGeometry(_moveInitialGeometry);
                        var svgData = flatGeom.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (svgData.StartsWith("F0") || svgData.StartsWith("F1"))
                        {
                            svgData = svgData.Substring(2).Trim();
                        }
                        activeLayer.TempSelectionPath = SkiaSharp.SKPath.ParseSvgPathData(svgData);
                    }
                    catch { }

                    activeLayer.TempMoveDx = 0;
                    activeLayer.TempMoveDy = 0;
                    _accumulatedMoveDx = 0;
                    _accumulatedMoveDy = 0;
                }
                else
                {
                    _moveInitialGeometry = null;
                    activeLayer.TempSelectionGeometry = null;

                    if (activeLayer.OriginalTransformBitmap == null)
                    {

                        var bounds = GetLayerContentBounds(activeLayer.Bitmap);

                        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                        {
                            bounds = new Rect(0, 0, w, h);
                        }

                        int startX = (int)bounds.Left;
                        int startY = (int)bounds.Top;
                        int bw = (int)bounds.Width;
                        int bh = (int)bounds.Height;

                        int tightStride = bw * 4;
                        byte[] tightPixels = new byte[tightStride * bh];


                        // CopyPixels là read-only — KHÔNG cần Lock/Unlock (Lock gây deadlock với WPF render thread)
                        activeLayer.Bitmap.CopyPixels(new Int32Rect(startX, startY, bw, bh), tightPixels, tightStride, 0);


                        var origBmp = new WriteableBitmap(bw, bh, 96, 96, PixelFormats.Bgra32, null);
                        origBmp.WritePixels(new Int32Rect(0, 0, bw, bh), tightPixels, tightStride, 0);


                        activeLayer.OriginalTransformBitmap = origBmp;
                        activeLayer.ContentBounds = bounds;

                    }

                    if (_moveInitialFullPixels == null)
                    {

                        _moveInitialFullPixels = new byte[strideValue * h];
                        activeLayer.Bitmap.CopyPixels(_moveInitialFullPixels, strideValue, 0);
                        _accumulatedMoveDx = 0;
                        _accumulatedMoveDy = 0;
                    }
                    activeLayer.TempMoveDx = _accumulatedMoveDx;
                    activeLayer.TempMoveDy = _accumulatedMoveDy;
                }


                UpdatePolygonDisplay();

                MainScrollViewer.CaptureMouse();

                return;
            }

            if (tool == "Eyedropper")
            {
                PickColorWithEyedropper(px, py);
                return;
            }



            if (tool == "Slice")
            {
                _isSelecting = true;
                _selectionStartPoint = clickPos;
                SelectionBoxRect.Visibility = Visibility.Visible;
                SelectionBoxRect.Width = 0;
                SelectionBoxRect.Height = 0;
                SelectionBoxRect.Margin = new Thickness(clickPos.X, clickPos.Y, 0, 0);
                MainScrollViewer.CaptureMouse();
                return;
            }

            if (tool == "SliceSelect")
            {
                HandleSliceSelectMouseDown(clickPos, px, py);
                return;
            }

            if (tool == "Selection" || tool == "ObjectSelection")
            {
                if (_currentSelectionMode == SelectionMode.New)
                {
                    ClearSelection();
                }
                else
                {
                    _selectionPoints.Clear();
                }
                _isSelecting = true;
                _selectionStartPoint = clickPos;
                _selectionRect = null;
                SelectionBoxRect.Visibility = Visibility.Visible;
                SelectionBoxRect.Width = 0;
                SelectionBoxRect.Height = 0;
                SelectionBoxRect.Margin = new Thickness(clickPos.X, clickPos.Y, 0, 0);
                MainScrollViewer.CaptureMouse();
                return;
            }

            if (tool == "MagicWand")
            {
                RunMagicWandSelection(px, py);
                return;
            }

            if (tool == "QuickSelection")
            {
                double radius = EditorPanel.BrushSize / 2.0;
                StartQuickSelection(px, py, radius);
                MainScrollViewer.CaptureMouse();
                return;
            }

            if (tool == "Lasso")
            {
                if (_currentSelectionMode == SelectionMode.New)
                {
                    ClearSelection();
                }
                else
                {
                    _selectionPoints.Clear();
                }
                _isSelecting = true;
                _selectionRect = null;
                _selectionPoints.Add(new Point(rawPx, rawPy));
                SelectionBoxRect.Visibility = Visibility.Collapsed;
                if (SelectionBoxRectBg != null) SelectionBoxRectBg.Visibility = Visibility.Collapsed;
                UpdateLassoPreview();
                MainScrollViewer.CaptureMouse();
                return;
            }

            if (tool == "PolyLasso")
            {
                if (_selectionPoints.Count == 0)
                {
                    if (_currentSelectionMode == SelectionMode.New)
                    {
                        ClearSelection();
                    }
                    _isSelecting = true;
                    _selectionRect = null;
                    SelectionBoxRect.Visibility = Visibility.Collapsed;
                    if (SelectionBoxRectBg != null) SelectionBoxRectBg.Visibility = Visibility.Collapsed;
                    _selectionPoints.Add(new Point(rawPx, rawPy));
                    UpdatePolyLassoPreview(clickPos);
                    MainScrollViewer.CaptureMouse();
                }
                else
                {
                    _selectionPoints.Add(new Point(rawPx, rawPy));
                    UpdatePolyLassoPreview(clickPos);
                }
                return;
            }

            if (tool == "Text")
            {
                var doc = _node.EditorDoc;

                if (TextMoveContainer.Visibility == Visibility.Visible)
                {
                    var clickPosInBBox = e.GetPosition(TextMoveContainer);
                    bool clickedInsideBBox = clickPosInBBox.X >= 0 && clickPosInBBox.X <= TextMoveContainer.ActualWidth &&
                                            clickPosInBBox.Y >= 0 && clickPosInBBox.Y <= TextMoveContainer.ActualHeight;

                    if (!clickedInsideBBox)
                    {
                        CommitActiveText();
                    }
                    else
                    {
                        return;
                    }
                }
                else if (doc != null && activeLayer != null && activeLayer.IsTextLayer)
                {
                    if (px >= activeLayer.TextX && px <= activeLayer.TextX + activeLayer.TextWidth &&
                        py >= activeLayer.TextY && py <= activeLayer.TextY + activeLayer.TextHeight)
                    {
                        EnterTextEditingMode(activeLayer);
                        return;
                    }
                }

                // Create a new text layer (Photoshop style)
                if (doc != null)
                {
                    var textLayer = new EditorLayer(doc.Width, doc.Height, "Text " + doc.GetNextLayerName());
                    textLayer.IsTextLayer = true;
                    textLayer.TextContent = "Nhập chữ...";
                    textLayer.TextX = clickPos.X;
                    textLayer.TextY = clickPos.Y;
                    textLayer.TextWidth = 150;
                    textLayer.TextHeight = 60;
                    textLayer.TextFontSize = EditorPanel.TextFontSize;
                    textLayer.TextColor = EditorPanel.TextColor;
                    textLayer.TextFontFamily = EditorPanel.TextFontFamily;
                    textLayer.TextFontStyle = EditorPanel.TextFontStyle;
                    textLayer.TextAlignment = EditorPanel.TextAlignment;

                    doc.Layers.Add(textLayer);
                    doc.ActiveLayer = textLayer;
                    EditorPanel.RefreshLayersList();
                }
                return;
            }

            if (tool == "Fill")
            {
                int stride = activeLayer.Width * 4;
                var oldPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(oldPixels, stride, 0);

                var tempPixels = new byte[stride * activeLayer.Height];
                Array.Copy(oldPixels, tempPixels, oldPixels.Length);

                FloodFill(tempPixels, activeLayer.Width, activeLayer.Height, px, py, _node.EditorDoc.ForegroundColor);

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), tempPixels, stride, 0);
                activeLayer.InvalidateThumbnail();
                var newPixels = new byte[stride * activeLayer.Height];
                activeLayer.Bitmap.CopyPixels(newPixels, stride, 0);

                var cmd = new PixelEditCommand(activeLayer, oldPixels, newPixels);
                _node.EditorDoc.History.Execute(cmd);
                OnEditorDocumentModified();
                return;
            }

            if (tool == "Brush")
            {
                _isDrawingPixels = true;

                // Xác định kích thước overlay và offset
                int overlayW = activeLayer.Width;
                int overlayH = activeLayer.Height;
                int clipOffsetX = activeLayer.OffsetX;
                int clipOffsetY = activeLayer.OffsetY;

                int stride = overlayW * 4;
                int pixelSize = stride * overlayH;

                // 1. Initialize session if not already active
                if (_brushOverlayBitmap == null || _brushOverlayBitmap.PixelWidth != overlayW || _brushOverlayBitmap.PixelHeight != overlayH)
                {
                    CommitBrushDrawingSession();

                    _brushOverlayBitmap = new WriteableBitmap(overlayW, overlayH, 96, 96, PixelFormats.Pbgra32, null);
                    _brushSessionLayer = activeLayer;
                    
                    // Build and cache plates for fast CPU composite
                    _node.EditorDoc.BuildMovePlates(activeLayer, out var bgPlate, out var fgPlate);
                    _node.EditorDoc.CachedBgPlate = bgPlate;
                    _node.EditorDoc.CachedFgPlate = fgPlate;
                    _node.EditorDoc.IsDrawingSessionActive = true;

                    _strokeMinX = overlayW - 1;
                    _strokeMaxX = 0;
                    _strokeMinY = overlayH - 1;
                    _strokeMaxY = 0;
                }

                _localSelectionClipPath?.Dispose();
                _localSelectionClipPath = null;
                if (activeLayer.ContentGeometry != null)
                {
                    _localSelectionClipPath = ConvertGeometryToSKPath(activeLayer.ContentGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY);
                }
                else if (_activeSelectionGeometry != null)
                {
                    _localSelectionClipPath = ConvertGeometryToSKPath(_activeSelectionGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY);
                }

                ActiveLayerDrawingOverlay.Source = _brushOverlayBitmap;
                ActiveLayerDrawingOverlay.Opacity = activeLayer.Opacity;
                ActiveLayerDrawingOverlay.Width = overlayW;
                ActiveLayerDrawingOverlay.Height = overlayH;
                // Đặt vị trí overlay đúng vị trí layer offset trong ImageContainer
                ActiveLayerDrawingOverlay.Margin = new Thickness(clipOffsetX, clipOffsetY, 0, 0);
                ActiveLayerDrawingOverlay.Visibility = Visibility.Visible;

                // Offset px/py sang toạ độ local của overlay
                int localPx = px - clipOffsetX;
                int localPy = py - clipOffsetY;

                _lastDrawingPixelPoint = new Point(localPx, localPy);
                _strokePoints.Clear();
                _strokePoints.Add(new Point(localPx, localPy));
                _brushDistanceAccumulator = 0;

                double rawRadius = EditorPanel.BrushSize / 2.0;
                double hardness = EditorPanel.BrushHardness;
                double flow = EditorPanel.BrushFlow;
                Color color = _node.EditorDoc.ForegroundColor;

                PreRenderBrushTip();

                bool isComplexPreset = _currentBrushPreset != BrushPreset.RoundHard &&
                                       _currentBrushPreset != BrushPreset.RoundSoft &&
                                       _currentBrushPreset != BrushPreset.Airbrush &&
                                       _currentBrushPreset != BrushPreset.Pencil;

                _currentStrokeInfo = new BrushStrokeInfo
                {
                    Preset = _currentBrushPreset,
                    Radius = rawRadius,
                    Hardness = hardness,
                    Flow = flow,
                    Color = color,
                    IsEraser = false
                };
                _currentStrokeInfo.Points.Add(new Point(localPx, localPy));

                if (!isComplexPreset)
                {
                    float blurSigma = 0;
                    if (_currentBrushPreset == BrushPreset.RoundSoft || _currentBrushPreset == BrushPreset.Airbrush)
                    {
                        blurSigma = (float)(rawRadius * 0.4);
                    }
                    else if (_currentBrushPreset == BrushPreset.RoundHard && hardness < 100)
                    {
                        blurSigma = (float)(rawRadius * (1.0 - hardness / 100.0) * 0.5);
                    }

                    double radius = Math.Max(0.1, rawRadius - blurSigma);

                    _currentStrokePaint = new SkiaSharp.SKPaint
                    {
                        Style = SkiaSharp.SKPaintStyle.Stroke,
                        StrokeWidth = (float)(radius * 2),
                        StrokeCap = SkiaSharp.SKStrokeCap.Round,
                        StrokeJoin = SkiaSharp.SKStrokeJoin.Round,
                        IsAntialias = true,
                        BlendMode = SkiaSharp.SKBlendMode.SrcOver
                    };

                    byte alpha = (byte)Math.Clamp(color.A * (flow / 100.0), 0, 255);
                    _currentStrokePaint.Color = new SkiaSharp.SKColor(color.R, color.G, color.B, alpha);

                    if (blurSigma > 0.1f)
                    {
                        _currentStrokePaint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                    }

                    _currentStrokePath = new SkiaSharp.SKPath();
                    _currentStrokePath.MoveTo((float)localPx, (float)localPy);
                    _currentStrokePath.LineTo((float)localPx + 0.01f, (float)localPy);
                }

                double extendedRadius = isComplexPreset ? (rawRadius * 5.0 + 5.0) : (rawRadius + 2.0);
                _strokeMinX = Math.Min(_strokeMinX, Math.Clamp((int)(localPx - extendedRadius), 0, overlayW - 1));
                _strokeMaxX = Math.Max(_strokeMaxX, Math.Clamp((int)(localPx + extendedRadius), 0, overlayW - 1));
                _strokeMinY = Math.Min(_strokeMinY, Math.Clamp((int)(localPy - extendedRadius), 0, overlayH - 1));
                _strokeMaxY = Math.Max(_strokeMaxY, Math.Clamp((int)(localPy + extendedRadius), 0, overlayH - 1));

                _prevSegmentMinX = _strokeMinX;
                _prevSegmentMaxX = _strokeMaxX;
                _prevSegmentMinY = _strokeMinY;
                _prevSegmentMaxY = _strokeMaxY;

                bool useDirectSource = (_node.EditorDoc.Layers.Count == 1 &&
                                         activeLayer.Opacity >= 0.99 &&
                                         activeLayer.BlendMode == BlendMode.Normal);
                 if (useDirectSource)
                 {
                     MainImage.Source = activeLayer.Bitmap;
                 }

                DrawActiveStrokeMouseDownToOverlay(localPx, localPy, rawRadius, hardness, flow, color, isComplexPreset);
                MarkCompositeDirty();
                MainScrollViewer.CaptureMouse();
            }
            else if (tool == "Eraser")
            {
                _isDrawingPixels = true;

                // Xác định kích thước overlay và offset
                int overlayW = activeLayer.Width;
                int overlayH = activeLayer.Height;
                int clipOffsetX = activeLayer.OffsetX;
                int clipOffsetY = activeLayer.OffsetY;

                int stride = overlayW * 4;
                int pixelSize = stride * overlayH;

                // 1. Initialize session if not already active
                if (_brushOverlayBitmap == null || _brushOverlayBitmap.PixelWidth != overlayW || _brushOverlayBitmap.PixelHeight != overlayH)
                {
                    CommitBrushDrawingSession();

                    _brushOverlayBitmap = new WriteableBitmap(overlayW, overlayH, 96, 96, PixelFormats.Pbgra32, null);
                    _brushSessionLayer = activeLayer;
                    
                    // Build and cache plates for fast CPU composite
                    _node.EditorDoc.BuildMovePlates(activeLayer, out var bgPlate, out var fgPlate);
                    _node.EditorDoc.CachedBgPlate = bgPlate;
                    _node.EditorDoc.CachedFgPlate = fgPlate;
                    _node.EditorDoc.IsDrawingSessionActive = true;

                    _strokeMinX = overlayW - 1;
                    _strokeMaxX = 0;
                    _strokeMinY = overlayH - 1;
                    _strokeMaxY = 0;
                }

                _localSelectionClipPath?.Dispose();
                _localSelectionClipPath = null;
                if (activeLayer.ContentGeometry != null)
                {
                    _localSelectionClipPath = ConvertGeometryToSKPath(activeLayer.ContentGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY);
                }
                else if (_activeSelectionGeometry != null)
                {
                    _localSelectionClipPath = ConvertGeometryToSKPath(_activeSelectionGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY);
                }

                ActiveLayerDrawingOverlay.Source = _brushOverlayBitmap;
                ActiveLayerDrawingOverlay.Opacity = 0.20;
                ActiveLayerDrawingOverlay.Width = overlayW;
                ActiveLayerDrawingOverlay.Height = overlayH;
                ActiveLayerDrawingOverlay.Margin = new Thickness(clipOffsetX, clipOffsetY, 0, 0);
                ActiveLayerDrawingOverlay.Visibility = Visibility.Visible;

                // Offset px/py sang toạ độ local của overlay
                int localPx = px - clipOffsetX;
                int localPy = py - clipOffsetY;

                _lastDrawingPixelPoint = new Point(localPx, localPy);
                _strokePoints.Clear();
                _strokePoints.Add(new Point(localPx, localPy));
                _brushDistanceAccumulator = 0;

                double rawRadius = EditorPanel.BrushSize / 2.0;
                double hardness = EditorPanel.BrushHardness;
                double guideFlow = 100.0;
                Color guideColor = _node.EditorDoc.ForegroundColor;

                PreRenderBrushTip();

                bool isComplexPreset = _currentBrushPreset != BrushPreset.RoundHard &&
                                       _currentBrushPreset != BrushPreset.RoundSoft &&
                                       _currentBrushPreset != BrushPreset.Airbrush &&
                                       _currentBrushPreset != BrushPreset.Pencil;

                _currentStrokeInfo = new BrushStrokeInfo
                {
                    Preset = _currentBrushPreset,
                    Radius = rawRadius,
                    Hardness = hardness,
                    Flow = guideFlow,
                    Color = guideColor,
                    IsEraser = true
                };
                _currentStrokeInfo.Points.Add(new Point(localPx, localPy));

                if (!isComplexPreset)
                {
                    float blurSigma = 0;
                    if (_currentBrushPreset == BrushPreset.RoundSoft || _currentBrushPreset == BrushPreset.Airbrush)
                    {
                        blurSigma = (float)(rawRadius * 0.4);
                    }
                    else if (_currentBrushPreset == BrushPreset.RoundHard && hardness < 100)
                    {
                        blurSigma = (float)(rawRadius * (1.0 - hardness / 100.0) * 0.5);
                    }

                    double radius = Math.Max(0.1, rawRadius - blurSigma);

                    _currentStrokePaint = new SkiaSharp.SKPaint
                    {
                        Style = SkiaSharp.SKPaintStyle.Stroke,
                        StrokeWidth = (float)(radius * 2),
                        StrokeCap = SkiaSharp.SKStrokeCap.Round,
                        StrokeJoin = SkiaSharp.SKStrokeJoin.Round,
                        IsAntialias = true,
                        BlendMode = SkiaSharp.SKBlendMode.SrcOver
                    };

                    byte alpha = (byte)Math.Clamp(255 * (guideFlow / 100.0), 0, 255);
                    _currentStrokePaint.Color = new SkiaSharp.SKColor(guideColor.R, guideColor.G, guideColor.B, alpha);

                    if (blurSigma > 0.1f)
                    {
                        _currentStrokePaint.MaskFilter = SkiaSharp.SKMaskFilter.CreateBlur(SkiaSharp.SKBlurStyle.Normal, blurSigma);
                    }

                    _currentStrokePath = new SkiaSharp.SKPath();
                    _currentStrokePath.MoveTo((float)localPx, (float)localPy);
                    _currentStrokePath.LineTo((float)localPx + 0.01f, (float)localPy);
                }

                double extendedRadius = isComplexPreset ? (rawRadius * 5.0 + 5.0) : (rawRadius + 2.0);
                _strokeMinX = Math.Min(_strokeMinX, Math.Clamp((int)(localPx - extendedRadius), 0, overlayW - 1));
                _strokeMaxX = Math.Max(_strokeMaxX, Math.Clamp((int)(localPx + extendedRadius), 0, overlayW - 1));
                _strokeMinY = Math.Min(_strokeMinY, Math.Clamp((int)(localPy - extendedRadius), 0, overlayH - 1));
                _strokeMaxY = Math.Max(_strokeMaxY, Math.Clamp((int)(localPy + extendedRadius), 0, overlayH - 1));

                _prevSegmentMinX = _strokeMinX;
                _prevSegmentMaxX = _strokeMaxX;
                _prevSegmentMinY = _strokeMinY;
                _prevSegmentMaxY = _strokeMaxY;

                DrawActiveStrokeMouseDownToOverlay(localPx, localPy, rawRadius, hardness, guideFlow, guideColor, isComplexPreset);
                _eraserBitmapLocked = false;
                _eraserLockedSurface = null;

                MarkCompositeDirty();
                MainScrollViewer.CaptureMouse();
            }
        }

        private void HandleManualEditorMouseMove(MouseEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            string tool = EditorPanel.ActiveToolName;
            var mousePos = e.GetPosition(MainImage);

            double docW = _node.EditorDoc.Width;
            double docH = _node.EditorDoc.Height;
            double scaleX = docW / MainImage.ActualWidth;
            double scaleY = docH / MainImage.ActualHeight;
            int rawPx = (int)(mousePos.X * scaleX);
            int rawPy = (int)(mousePos.Y * scaleY);
            int px = Math.Clamp(rawPx, 0, (int)docW - 1);
            int py = Math.Clamp(rawPy, 0, (int)docH - 1);

            if (_isSelecting)
            {
                if (tool == "Selection" || tool == "ObjectSelection")
                {
                    double x = Math.Min(_selectionStartPoint.X, mousePos.X);
                    double y = Math.Min(_selectionStartPoint.Y, mousePos.Y);
                    double w = Math.Abs(_selectionStartPoint.X - mousePos.X);
                    double h = Math.Abs(_selectionStartPoint.Y - mousePos.Y);

                    if (tool == "ObjectSelection")
                    {
                        x = Math.Clamp(x, 0, MainImage.ActualWidth);
                        y = Math.Clamp(y, 0, MainImage.ActualHeight);
                        w = Math.Clamp(w, 0, MainImage.ActualWidth - x);
                        h = Math.Clamp(h, 0, MainImage.ActualHeight - y);
                    }

                    SelectionBoxRect.Margin = new Thickness(x, y, 0, 0);
                    SelectionBoxRect.Width = w;
                    SelectionBoxRect.Height = h;
                    // Sync black background rect
                    if (SelectionBoxRectBg != null)
                    {
                        SelectionBoxRectBg.Margin = SelectionBoxRect.Margin;
                        SelectionBoxRectBg.Width = w;
                        SelectionBoxRectBg.Height = h;
                        SelectionBoxRectBg.Visibility = Visibility.Visible;
                    }
                    ApplyPreviewStrokeStyle();
                }
                else if (tool == "Slice")
                {
                    double x = Math.Min(_selectionStartPoint.X, mousePos.X);
                    double y = Math.Min(_selectionStartPoint.Y, mousePos.Y);
                    double w = Math.Abs(_selectionStartPoint.X - mousePos.X);
                    double h = Math.Abs(_selectionStartPoint.Y - mousePos.Y);

                    x = Math.Clamp(x, 0, MainImage.ActualWidth);
                    y = Math.Clamp(y, 0, MainImage.ActualHeight);
                    w = Math.Clamp(w, 0, MainImage.ActualWidth - x);
                    h = Math.Clamp(h, 0, MainImage.ActualHeight - y);

                    SelectionBoxRect.Margin = new Thickness(x, y, 0, 0);
                    SelectionBoxRect.Width = w;
                    SelectionBoxRect.Height = h;
                }
                else if (tool == "SliceSelect")
                {
                    HandleSliceSelectMouseMove(mousePos, px, py);
                }
                else if (tool == "Lasso")
                {
                    var newPt = new Point(rawPx, rawPy);
                    if (_selectionPoints.Count == 0 || _selectionPoints[_selectionPoints.Count - 1] != newPt)
                    {
                        _selectionPoints.Add(newPt);
                        UpdateLassoPreview();
                    }
                }
                else if (tool == "PolyLasso" && _selectionPoints.Count > 0)
                {
                    UpdatePolyLassoPreview(mousePos);
                }
                else if (tool == "QuickSelection")
                {
                    double qSelRadius = EditorPanel.BrushSize / 2.0;
                    GrowQuickSelection(px, py, qSelRadius);
                }
                return;
            }

            if (tool == "Move" && _isMovingLayer)
            {
                double docDeltaX = (mousePos.X - _moveStartMousePos.X) * scaleX;
                double docDeltaY = (mousePos.Y - _moveStartMousePos.Y) * scaleY;

                int dx = (int)Math.Round(docDeltaX);
                int dy = (int)Math.Round(docDeltaY);

                if (_moveInitialGeometry != null)
                {
                    activeLayer.TempMoveDx = dx;
                    activeLayer.TempMoveDy = dy;
                }
                else
                {
                    activeLayer.TempMoveDx = _accumulatedMoveDx + dx;
                    activeLayer.TempMoveDy = _accumulatedMoveDy + dy;
                }



                if (_moveInitialGeometry != null)
                {
                    var transform = new TranslateTransform(dx, dy);
                    _activeSelectionGeometry = Geometry.Combine(_moveInitialGeometry, Geometry.Empty, GeometryCombineMode.Union, transform);
                    UpdatePolygonDisplay();
                }


                MarkCompositeDirty();
                return;
            }

            double radius = EditorPanel.BrushSize / 2.0;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;
            Color color = _node.EditorDoc.ForegroundColor;

            // Cả Brush và Eraser đều dùng coordinates local của layer
            int clipOffX = activeLayer.OffsetX;
            int clipOffY = activeLayer.OffsetY;

            Point currentPoint = new Point(px - clipOffX, py - clipOffY);
            Point prevPoint = _lastDrawingPixelPoint;
            int activeW = activeLayer.Width;
            int activeH = activeLayer.Height;

            if (_strokePoints.Count > 0 && _strokePoints[^1] == currentPoint) return;
            _strokePoints.Add(currentPoint);
            _lastDrawingPixelPoint = currentPoint;

            bool isComplexPreset = _currentBrushPreset != BrushPreset.RoundHard &&
                                   _currentBrushPreset != BrushPreset.RoundSoft &&
                                   _currentBrushPreset != BrushPreset.Airbrush &&
                                   _currentBrushPreset != BrushPreset.Pencil;

            double extendedRadius = isComplexPreset ? (radius * 5.0 + 5.0) : (radius + 2.0);

            int segmentMinX = Math.Clamp((int)(Math.Min(prevPoint.X, currentPoint.X) - extendedRadius), 0, activeW - 1);
            int segmentMaxX = Math.Clamp((int)(Math.Max(prevPoint.X, currentPoint.X) + extendedRadius), 0, activeW - 1);
            int segmentMinY = Math.Clamp((int)(Math.Min(prevPoint.Y, currentPoint.Y) - extendedRadius), 0, activeH - 1);
            int segmentMaxY = Math.Clamp((int)(Math.Max(prevPoint.Y, currentPoint.Y) + extendedRadius), 0, activeH - 1);

            _strokeMinX = Math.Min(_strokeMinX, segmentMinX);
            _strokeMaxX = Math.Max(_strokeMaxX, segmentMaxX);
            _strokeMinY = Math.Min(_strokeMinY, segmentMinY);
            _strokeMaxY = Math.Max(_strokeMaxY, segmentMaxY);

            int dirtyMinX = Math.Min(segmentMinX, _prevSegmentMinX);
            int dirtyMaxX = Math.Max(segmentMaxX, _prevSegmentMaxX);
            int dirtyMinY = Math.Min(segmentMinY, _prevSegmentMinY);
            int dirtyMaxY = Math.Max(segmentMaxY, _prevSegmentMaxY);

            int dirtyW = dirtyMaxX - dirtyMinX + 1;
            int dirtyH = dirtyMaxY - dirtyMinY + 1;

            if (tool == "Brush")
            {
                DrawActiveStrokeSegmentToOverlay(prevPoint, currentPoint);
            }
            else if (tool == "Eraser")
            {
                DrawActiveStrokeSegmentToOverlay(prevPoint, currentPoint);
            }

            // 4. Remember the current segment box as the previous one for the next frame
            _prevSegmentMinX = segmentMinX;
            _prevSegmentMaxX = segmentMaxX;
            _prevSegmentMinY = segmentMinY;
            _prevSegmentMaxY = segmentMaxY;

            MarkCompositeDirty();
        }

        private void HandleManualEditorMouseUp()
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            string tool = EditorPanel.ActiveToolName;

            if (_isMovingLayer)
            {
                _isMovingLayer = false;
                MainScrollViewer.ReleaseMouseCapture();

                _moveBgPlateSK?.Dispose();
                _moveBgPlateSK = null;
                _moveFgPlateSK?.Dispose();
                _moveFgPlateSK = null;
                _moveActiveLayerSK?.Dispose();
                _moveActiveLayerSK = null;

                var targetLayer = _movingLayer ?? activeLayer;
                if (targetLayer != null)
                {
                    if (_moveInitialFullPixels != null && _moveInitialGeometry != null)
                    {
                        int dx = (int)Math.Round(targetLayer.TempMoveDx);
                        int dy = (int)Math.Round(targetLayer.TempMoveDy);

                        targetLayer.TempMoveDx = 0;
                        targetLayer.TempMoveDy = 0;
                        targetLayer.TempSelectionGeometry = null;

                        CommitSelectionMove(targetLayer, _moveInitialFullPixels, dx, dy);
                        _moveInitialFullPixels = null;
                        _moveInitialGeometry = null;
                        _movingLayer = null;
                    }
                    else
                    {
                        _accumulatedMoveDx = targetLayer.TempMoveDx;
                        _accumulatedMoveDy = targetLayer.TempMoveDy;
                        CommitPendingMoveTranslation();
                    }
                }
                return;
            }

            if (_isSelecting)
            {
                if (tool == "Selection" || tool == "ObjectSelection")
                {
                    _isSelecting = false;
                    MainScrollViewer.ReleaseMouseCapture();

                    if (activeLayer != null)
                    {
                        double scaleX = activeLayer.Width / MainImage.ActualWidth;
                        double scaleY = activeLayer.Height / MainImage.ActualHeight;

                        double lx = SelectionBoxRect.Margin.Left * scaleX;
                        double ly = SelectionBoxRect.Margin.Top * scaleY;
                        double lw = SelectionBoxRect.Width * scaleX;
                        double lh = SelectionBoxRect.Height * scaleY;

                        if (lw > 2 && lh > 2)
                        {
                            if (tool == "ObjectSelection")
                            {
                                int rx1 = (int)Math.Clamp(lx, 0, activeLayer.Width - 1);
                                int ry1 = (int)Math.Clamp(ly, 0, activeLayer.Height - 1);
                                int rx2 = (int)Math.Clamp(lx + lw, 0, activeLayer.Width - 1);
                                int ry2 = (int)Math.Clamp(ly + lh, 0, activeLayer.Height - 1);

                                RunObjectSelection(rx1, ry1, rx2, ry2);
                            }
                            else
                            {
                                var rectGeom = new RectangleGeometry(new Rect(lx, ly, lw, lh));
                                ApplyNewGeometry(rectGeom);
                            }
                        }
                        else
                        {
                            ClearSelection();
                        }
                    }
                }
                else if (tool == "QuickSelection")
                {
                    _isSelecting = false;
                    MainScrollViewer.ReleaseMouseCapture();
                    CommitQuickSelection();
                }
                else if (tool == "Slice")
                {
                    _isSelecting = false;
                    MainScrollViewer.ReleaseMouseCapture();
                    if (SelectionBoxRect.Width > 4 && SelectionBoxRect.Height > 4)
                    {
                        double scaleX = activeLayer.Width / MainImage.ActualWidth;
                        double scaleY = activeLayer.Height / MainImage.ActualHeight;

                        double lx = SelectionBoxRect.Margin.Left * scaleX;
                        double ly = SelectionBoxRect.Margin.Top * scaleY;
                        double lw = SelectionBoxRect.Width * scaleX;
                        double lh = SelectionBoxRect.Height * scaleY;

                        _slices.Add(new Rect(lx, ly, lw, lh));
                        _selectedSliceIndex = _slices.Count - 1;
                        UpdateSlicesDisplay();
                    }
                    SelectionBoxRect.Visibility = Visibility.Collapsed;
                    if (SelectionBoxRectBg != null) SelectionBoxRectBg.Visibility = Visibility.Collapsed;
                }
                else if (tool == "SliceSelect")
                {
                    _isSelecting = false;
                    _sliceResizeHandle = "";
                    MainScrollViewer.ReleaseMouseCapture();
                    UpdateSlicesDisplay();
                }
                else if (tool == "Lasso")
                {
                    _isSelecting = false;
                    MainScrollViewer.ReleaseMouseCapture();

                    if (_selectionPoints.Count >= 3)
                    {
                        // Auto-connect start and end points
                        _selectionPoints.Add(_selectionPoints[0]);
                        var pathGeometry = new PathGeometry();
                        var pathFigure = new PathFigure { StartPoint = _selectionPoints[0], IsClosed = true };
                        for (int i = 1; i < _selectionPoints.Count; i++)
                        {
                            pathFigure.Segments.Add(new LineSegment(_selectionPoints[i], true));
                        }
                        pathGeometry.Figures.Add(pathFigure);
                        pathGeometry.FillRule = FillRule.Nonzero;

                        ApplyNewGeometry(pathGeometry);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
                else if (tool == "PolyLasso")
                {
                    // For PolyLasso, mouse up does not end the selection.
                    // Keep _isSelecting true and mouse capture active until Enter or double click.
                }
                return;
            }

            if (!_isDrawingPixels) return;
            _isDrawingPixels = false;
            MainScrollViewer.ReleaseMouseCapture();

            if (activeLayer == null) return;

            if (tool == "Brush")
            {
                if (_brushOverlayBitmap != null)
                {
                    int layerW = activeLayer.Width;
                    int layerH = activeLayer.Height;
                    int overlayW = _brushOverlayBitmap.PixelWidth;
                    int overlayH = _brushOverlayBitmap.PixelHeight;
                    int clipOffsetX = 0;
                    int clipOffsetY = 0;

                    // 1. Calculate dirty rect region with absolute safety against exception throwing from Math.Clamp
                    int minX = Math.Clamp(_strokeMinX, 0, layerW - 1);
                    int maxX = Math.Clamp(_strokeMaxX, 0, layerW - 1);
                    int minY = Math.Clamp(_strokeMinY, 0, layerH - 1);
                    int maxY = Math.Clamp(_strokeMaxY, 0, layerH - 1);

                    if (maxX < minX)
                    {
                        var temp = minX;
                        minX = maxX;
                        maxX = temp;
                    }
                    if (maxY < minY)
                    {
                        var temp = minY;
                        minY = maxY;
                        maxY = temp;
                    }

                    int dirtyX = minX;
                    int dirtyY = minY;
                    int dirtyW = maxX - minX + 1;
                    int dirtyH = maxY - minY + 1;

                    dirtyW = Math.Max(1, Math.Min(dirtyW, layerW - dirtyX));
                    dirtyH = Math.Max(1, Math.Min(dirtyH, layerH - dirtyY));
                    var dirtyRect = new Int32Rect(dirtyX, dirtyY, dirtyW, dirtyH);

                    int regionStride = dirtyW * 4;
                    byte[] oldRegionPixels = new byte[regionStride * dirtyH];
                    byte[] newRegionPixels = new byte[regionStride * dirtyH];

                    // 2. Copy old pixels (WITHOUT lock to prevent deadlocking CopyPixels on UI thread)
                    activeLayer.Bitmap.CopyPixels(dirtyRect, oldRegionPixels, regionStride, 0);

                    // 3. Draw overlay onto active layer with geometry clipping (WITH lock only during drawing)
                    activeLayer.Bitmap.Lock();
                    try
                    {
                        var layerInfo = new SkiaSharp.SKImageInfo(layerW, layerH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var surface = SkiaSharp.SKSurface.Create(layerInfo, activeLayer.Bitmap.BackBuffer, activeLayer.Bitmap.BackBufferStride))
                        {
                            var canvas = surface.Canvas;
                            if (activeLayer.ContentGeometry != null)
                            {
                                using (var clipPath = ConvertGeometryToSKPath(activeLayer.ContentGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY))
                                {
                                    if (clipPath != null)
                                    {
                                        canvas.ClipPath(clipPath, SkiaSharp.SKClipOperation.Intersect, true);
                                    }
                                }
                            }
                            else if (_activeSelectionGeometry != null)
                            {
                                using (var clipPath = ConvertGeometryToSKPath(_activeSelectionGeometry, -activeLayer.OffsetX, -activeLayer.OffsetY))
                                {
                                    if (clipPath != null)
                                    {
                                        canvas.ClipPath(clipPath, SkiaSharp.SKClipOperation.Intersect, true);
                                    }
                                }
                            }

                            _brushOverlayBitmap.Lock();
                            try
                            {
                                var overlayInfo = new SkiaSharp.SKImageInfo(overlayW, overlayH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                                using (var overlaySKBitmap = new SkiaSharp.SKBitmap())
                                {
                                    overlaySKBitmap.InstallPixels(overlayInfo, _brushOverlayBitmap.BackBuffer, _brushOverlayBitmap.BackBufferStride);
                                    using (var paint = new SkiaSharp.SKPaint())
                                    {
                                        paint.BlendMode = SkiaSharp.SKBlendMode.SrcOver;
                                        canvas.DrawBitmap(overlaySKBitmap, clipOffsetX, clipOffsetY, paint);
                                    }
                                }
                            }
                            finally
                            {
                                _brushOverlayBitmap.Unlock();
                            }
                        }

                        // 4. Mark dirty rect
                        activeLayer.Bitmap.AddDirtyRect(dirtyRect);
                    }
                    finally
                    {
                        activeLayer.Bitmap.Unlock();
                    }

                    // 5. Copy new pixels (WITHOUT lock)
                    activeLayer.Bitmap.CopyPixels(dirtyRect, newRegionPixels, regionStride, 0);

                    // 6. Create region-based undo command
                    var cmd = new PixelRegionEditCommand(activeLayer, dirtyRect, oldRegionPixels, newRegionPixels);
                    _node.EditorDoc.History.Execute(cmd);

                    _brushOverlayBitmap = null;
                    _brushSessionLayer = null;
                    _localSelectionClipPath?.Dispose();
                    _localSelectionClipPath = null;

                    if (_currentStrokePaint != null)
                    {
                        _currentStrokePaint.Dispose();
                        _currentStrokePaint = null;
                    }
                    if (_currentStrokePath != null)
                    {
                        _currentStrokePath.Dispose();
                        _currentStrokePath = null;
                    }
                    _currentStrokeInfo = null;

                    if (_cachedBrushTip != null)
                    {
                        _cachedBrushTip.Dispose();
                        _cachedBrushTip = null;
                    }

                    FlushCompositeAndSync();
                }
            }
            else if (tool == "Eraser")
            {
                int layerW = activeLayer.Width;
                int layerH = activeLayer.Height;

                // Calculate dirty rect region with absolute safety against exception throwing from Math.Clamp
                int minX = Math.Clamp(_strokeMinX, 0, layerW - 1);
                int maxX = Math.Clamp(_strokeMaxX, 0, layerW - 1);
                int minY = Math.Clamp(_strokeMinY, 0, layerH - 1);
                int maxY = Math.Clamp(_strokeMaxY, 0, layerH - 1);

                if (maxX < minX)
                {
                    var temp = minX;
                    minX = maxX;
                    maxX = temp;
                }
                if (maxY < minY)
                {
                    var temp = minY;
                    minY = maxY;
                    maxY = temp;
                }

                int dirtyX = minX;
                int dirtyY = minY;
                int dirtyW = maxX - minX + 1;
                int dirtyH = maxY - minY + 1;

                dirtyW = Math.Max(1, Math.Min(dirtyW, layerW - dirtyX));
                dirtyH = Math.Max(1, Math.Min(dirtyH, layerH - dirtyY));
                var dirtyRect = new Int32Rect(dirtyX, dirtyY, dirtyW, dirtyH);

                int regionStride = dirtyW * 4;
                byte[] oldRegionPixels = new byte[regionStride * dirtyH];
                byte[] newRegionPixels = new byte[regionStride * dirtyH];

                // 1. Copy old pixels (WITHOUT lock to prevent deadlocking CopyPixels on UI thread)
                activeLayer.Bitmap.CopyPixels(dirtyRect, oldRegionPixels, regionStride, 0);

                // 2. Perform Eraser rendering onto layer
                RenderEraserStrokeToLayer(activeLayer);

                // 3. Copy new pixels (WITHOUT lock)
                activeLayer.Bitmap.CopyPixels(dirtyRect, newRegionPixels, regionStride, 0);

                ClearBrushOverlay();
                ActiveLayerDrawingOverlay.Source = null;
                ActiveLayerDrawingOverlay.Visibility = Visibility.Collapsed;
                _brushOverlayBitmap = null;
                _localSelectionClipPath?.Dispose();
                _localSelectionClipPath = null;

                _eraserLockedSurface?.Dispose();
                _eraserLockedSurface = null;
                _eraserBitmapLocked = false;

                // 4. Create and execute command
                var cmd = new PixelRegionEditCommand(activeLayer, dirtyRect, oldRegionPixels, newRegionPixels);
                _node.EditorDoc.History.Execute(cmd);
                _strokePoints.Clear();

                // Dispose cached eraser paint after stroke ends
                if (_cachedEraserPaint != null)
                {
                    _cachedEraserPaint.Dispose();
                    _cachedEraserPaint = null;
                    _cachedEraserBlurSigma = -1;
                }

                // Dispose cached background plate
                _eraserBgPlateSK?.Dispose();
                _eraserBgPlateSK = null;
                _eraserBgPlate = null;

                FlushCompositeAndSync();
            }
        }

    }
}
