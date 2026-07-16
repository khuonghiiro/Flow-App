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
        private void StartKeyMoveSession(EditorLayer activeLayer)
        {
            if (_isKeyMoving) return;

            // If mouse drag session is active, stop it
            if (_isMovingLayer)
            {
                _isMovingLayer = false;
                MainScrollViewer.ReleaseMouseCapture();
            }

            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int strideValue = w * 4;

            if (_moveInitialFullPixels == null)
            {
                _moveInitialFullPixels = new byte[strideValue * h];
                activeLayer.Bitmap.CopyPixels(_moveInitialFullPixels, strideValue, 0);
                _accumulatedMoveDx = 0;
                _accumulatedMoveDy = 0;
            }

            if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                _moveInitialGeometry = _activeSelectionGeometry.Clone();
                activeLayer.TempSelectionGeometry = _activeSelectionGeometry.Clone();
                activeLayer.TempMoveDx = 0;
                activeLayer.TempMoveDy = 0;
            }
            else
            {
                _moveInitialGeometry = null;
                activeLayer.TempSelectionGeometry = null;
                activeLayer.TempMoveDx = _accumulatedMoveDx;
                activeLayer.TempMoveDy = _accumulatedMoveDy;
            }

            _isKeyMoving = true;
            _keyDeltaX = 0;
            _keyDeltaY = 0;

            UpdatePolygonDisplay();

            if (_keyMoveCommitTimer == null)
            {
                _keyMoveCommitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _keyMoveCommitTimer.Tick += KeyMoveCommitTimer_Tick;
            }
        }

        private void KeyMoveCommitTimer_Tick(object? sender, EventArgs e)
        {
            CommitKeyMoveSession();
        }

        private void CommitKeyMoveSession()
        {
            _keyMoveCommitTimer?.Stop();
            if (!_isKeyMoving) return;

            _isKeyMoving = false;
            var activeLayer = _node?.EditorDoc?.ActiveLayer;
            if (activeLayer != null && _moveInitialFullPixels != null)
            {
                int dx = _keyDeltaX;
                int dy = _keyDeltaY;

                if (_moveInitialGeometry != null)
                {
                    activeLayer.TempMoveDx = 0;
                    activeLayer.TempMoveDy = 0;
                    activeLayer.TempSelectionGeometry = null;
                    CommitSelectionMove(activeLayer, _moveInitialFullPixels, dx, dy);
                    _moveInitialFullPixels = null;
                    _moveInitialGeometry = null;
                }
                else
                {
                    _accumulatedMoveDx += dx;
                    _accumulatedMoveDy += dy;
                    _keyDeltaX = 0;
                    _keyDeltaY = 0;
                    activeLayer.TempMoveDx = _accumulatedMoveDx;
                    activeLayer.TempMoveDy = _accumulatedMoveDy;
                }
            }
        }

        private void ApplyTemporaryKeyMove(EditorLayer activeLayer)
        {
            if (_moveInitialGeometry != null)
            {
                activeLayer.TempMoveDx = _keyDeltaX;
                activeLayer.TempMoveDy = _keyDeltaY;

                var transform = new TranslateTransform(_keyDeltaX, _keyDeltaY);
                _activeSelectionGeometry = Geometry.Combine(_moveInitialGeometry, Geometry.Empty, GeometryCombineMode.Union, transform);
                UpdatePolygonDisplay();
            }
            else
            {
                activeLayer.TempMoveDx = _accumulatedMoveDx + _keyDeltaX;
                activeLayer.TempMoveDy = _accumulatedMoveDy + _keyDeltaY;
            }

            MarkCompositeDirty();
        }

        private void DrawBitmapAt(WriteableBitmap dest, WriteableBitmap src, Rect destRect)
        {
            int dw = dest.PixelWidth;
            int dh = dest.PixelHeight;
            int sw = src.PixelWidth;
            int sh = src.PixelHeight;

            int destStride = dw * 4;
            int srcStride = sw * 4;

            byte[] destPixels = new byte[destStride * dh];
            byte[] srcPixels = new byte[srcStride * sh];

            dest.CopyPixels(destPixels, destStride, 0);
            src.CopyPixels(srcPixels, srcStride, 0);

            var destInfo = new SkiaSharp.SKImageInfo(dw, dh, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            var srcInfo = new SkiaSharp.SKImageInfo(sw, sh, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

            var gcDest = System.Runtime.InteropServices.GCHandle.Alloc(destPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            var gcSrc = System.Runtime.InteropServices.GCHandle.Alloc(srcPixels, System.Runtime.InteropServices.GCHandleType.Pinned);

            try
            {
                using (var destBmp = new SkiaSharp.SKBitmap())
                using (var srcBmp = new SkiaSharp.SKBitmap())
                {
                    destBmp.InstallPixels(destInfo, gcDest.AddrOfPinnedObject(), destStride);
                    srcBmp.InstallPixels(srcInfo, gcSrc.AddrOfPinnedObject(), srcStride);

                    using (var canvas = new SkiaSharp.SKCanvas(destBmp))
                    {
                        canvas.Clear(SkiaSharp.SKColors.Transparent);
                        var skDestRect = new SkiaSharp.SKRect((float)destRect.Left, (float)destRect.Top, (float)destRect.Right, (float)destRect.Bottom);
                        canvas.DrawBitmap(srcBmp, skDestRect);
                    }
                }
            }
            finally
            {
                gcDest.Free();
                gcSrc.Free();
            }

            dest.WritePixels(new Int32Rect(0, 0, dw, dh), destPixels, destStride, 0);
        }

        private void ShiftBitmapPixels(EditorLayer activeLayer, byte[] sourceFullPixels, int dx, int dy)
        {
            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int stride = w * 4;
            byte[] tempPixels = new byte[stride * h];

            var srcInfo = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            var gcSource = System.Runtime.InteropServices.GCHandle.Alloc(sourceFullPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            var gcDest = System.Runtime.InteropServices.GCHandle.Alloc(tempPixels, System.Runtime.InteropServices.GCHandleType.Pinned);

            try
            {
                using (var srcBmp = new SkiaSharp.SKBitmap())
                using (var destBmp = new SkiaSharp.SKBitmap())
                {
                    srcBmp.InstallPixels(srcInfo, gcSource.AddrOfPinnedObject(), stride);
                    destBmp.InstallPixels(srcInfo, gcDest.AddrOfPinnedObject(), stride);

                    using (var canvas = new SkiaSharp.SKCanvas(destBmp))
                    {
                        canvas.Clear(SkiaSharp.SKColors.Transparent);
                        if (_moveInitialGeometry != null)
                        {
                            SkiaSharp.SKPath origSelectionPath = null;
                            try
                            {
                                var flatGeom = PathGeometry.CreateFromGeometry(_moveInitialGeometry);
                                var svgData = flatGeom.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                if (svgData.StartsWith("F0") || svgData.StartsWith("F1"))
                                {
                                    svgData = svgData.Substring(2).Trim();
                                }
                                origSelectionPath = SkiaSharp.SKPath.ParseSvgPathData(svgData);
                            }
                            catch { }

                            if (origSelectionPath != null)
                            {
                                // 1. Draw background outside selection (DstOut)
                                canvas.DrawBitmap(srcBmp, 0, 0);

                                using (var paintErase = new SkiaSharp.SKPaint())
                                {
                                    paintErase.IsAntialias = true;
                                    paintErase.Style = SkiaSharp.SKPaintStyle.Fill;
                                    paintErase.Color = SkiaSharp.SKColors.Black;
                                    paintErase.BlendMode = SkiaSharp.SKBlendMode.DstOut;
                                    canvas.DrawPath(origSelectionPath, paintErase);
                                }

                                // 2. Draw selection shifted using a temporary bitmap (SrcIn)
                                using (var tempSelBmp = new SkiaSharp.SKBitmap(w, h))
                                {
                                    using (var tempCanvas = new SkiaSharp.SKCanvas(tempSelBmp))
                                    {
                                        tempCanvas.Clear(SkiaSharp.SKColors.Transparent);
                                        
                                        using (var paintMask = new SkiaSharp.SKPaint())
                                        {
                                            paintMask.IsAntialias = true;
                                            paintMask.Style = SkiaSharp.SKPaintStyle.Fill;
                                            paintMask.Color = SkiaSharp.SKColors.Black;
                                            tempCanvas.DrawPath(origSelectionPath, paintMask);
                                        }

                                        using (var paintSrc = new SkiaSharp.SKPaint())
                                        {
                                            paintSrc.BlendMode = SkiaSharp.SKBlendMode.SrcIn;
                                            tempCanvas.DrawBitmap(srcBmp, 0, 0, paintSrc);
                                        }
                                    }

                                    canvas.DrawBitmap(tempSelBmp, dx, dy);
                                }

                                origSelectionPath.Dispose();
                            }
                            else
                            {
                                canvas.DrawBitmap(srcBmp, dx, dy);
                            }
                        }
                        else
                        {
                            canvas.DrawBitmap(srcBmp, dx, dy);
                        }
                    }
                }
            }
            finally
            {
                gcSource.Free();
                gcDest.Free();
            }

            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), tempPixels, stride, 0);
        }

        private void CommitPendingMoveTranslation()
        {
            if (_isCommitingMove) return;
            _isCommitingMove = true;
            try
            {
                var targetLayer = _movingLayer ?? _node.EditorDoc?.ActiveLayer;
                if (targetLayer == null || _moveInitialFullPixels == null)
                {
                    _moveInitialFullPixels = null;
                    _movingLayer = null;
                    return;
                }

                if (_moveInitialGeometry != null)
                {
                    int dx = (int)Math.Round(targetLayer.TempMoveDx);
                    int dy = (int)Math.Round(targetLayer.TempMoveDy);

                    targetLayer.TempMoveDx = 0;
                    targetLayer.TempMoveDy = 0;
                    targetLayer.TempSelectionGeometry = null;

                    var initPixels = _moveInitialFullPixels;
                    _moveInitialFullPixels = null;
                    _moveInitialGeometry = null;
                    _movingLayer = null;

                    CommitSelectionMove(targetLayer, initPixels, dx, dy);
                    return;
                }

                if (_accumulatedMoveDx == 0 && _accumulatedMoveDy == 0)
                {
                    _moveInitialFullPixels = null;
                    _movingLayer = null;
                    return;
                }

                int dxLayer = (int)Math.Round(_accumulatedMoveDx);
                int dyLayer = (int)Math.Round(_accumulatedMoveDy);

                _accumulatedMoveDx = 0;
                _accumulatedMoveDy = 0;

                targetLayer.TempMoveDx = 0;
                targetLayer.TempMoveDy = 0;

                if (targetLayer.IsTextLayer)
                {
                    double oldX = targetLayer.TextX;
                    double oldY = targetLayer.TextY;
                    double newX = oldX + dxLayer;
                    double newY = oldY + dyLayer;

                    var cmd = new TextEditCommand(
                        targetLayer,
                        RedrawTextLayer,
                        targetLayer.TextContent, oldX, oldY, targetLayer.TextWidth, targetLayer.TextHeight, targetLayer.TextFontSize, targetLayer.TextColor, targetLayer.TextFontFamily, targetLayer.TextFontStyle, targetLayer.TextAlignment,
                        targetLayer.TextContent, newX, newY, targetLayer.TextWidth, targetLayer.TextHeight, targetLayer.TextFontSize, targetLayer.TextColor, targetLayer.TextFontFamily, targetLayer.TextFontStyle, targetLayer.TextAlignment
                    );
                    _node.EditorDoc?.History.Execute(cmd);

                    _moveInitialFullPixels = null;
                    _movingLayer = null;
                    targetLayer.InvalidateThumbnail();
                    OnEditorDocumentModified();
                    return;
                }

                Rect oldBounds = Rect.Empty;
                Rect newBounds = Rect.Empty;
                bool isSelectionLayer = targetLayer.OriginalTransformBitmap != null && !targetLayer.ContentBounds.IsEmpty;
                if (isSelectionLayer)
                {
                    oldBounds = targetLayer.ContentBounds;
                    newBounds = new Rect(oldBounds.Left + dxLayer, oldBounds.Top + dyLayer, oldBounds.Width, oldBounds.Height);
                    targetLayer.ContentBounds = newBounds;

                    // Regenerate Bitmap from OriginalTransformBitmap at newBounds
                    DrawBitmapAt(targetLayer.Bitmap, targetLayer.OriginalTransformBitmap, newBounds);
                }
                else
                {
                    // Apply shift once
                    ShiftBitmapPixels(targetLayer, _moveInitialFullPixels, dxLayer, dyLayer);
                }

                int moveStride = targetLayer.Width * 4;
                var finalPixels = new byte[moveStride * targetLayer.Height];
                targetLayer.Bitmap.CopyPixels(finalPixels, moveStride, 0);

                PixelEditCommand moveCmd;
                if (isSelectionLayer)
                {
                    moveCmd = new PixelEditCommand(targetLayer, _moveInitialFullPixels, finalPixels,
                        targetLayer.LayerScaleX, targetLayer.LayerScaleY, targetLayer.LayerAngle,
                        targetLayer.LayerTranslateX, targetLayer.LayerTranslateY, targetLayer.OriginalTransformBitmap);
                    moveCmd.KeepOriginalTransformBitmap = true;
                    moveCmd.HasCustomBounds = true;
                    moveCmd.CustomOldContentBounds = oldBounds;
                    moveCmd.CustomNewContentBounds = newBounds;
                    moveCmd.CaptureNewTransformState();
                }
                else
                {
                    moveCmd = new PixelEditCommand(targetLayer, _moveInitialFullPixels, finalPixels);
                }

                _node.EditorDoc?.History.Execute(moveCmd);

                _moveInitialFullPixels = null;
                _movingLayer = null;
                targetLayer.InvalidateThumbnail();
                OnEditorDocumentModified();
            }
            finally
            {
                _isCommitingMove = false;
            }
        }

        public bool CanHandleKey(KeyEventArgs e)
        {
            if (_node.ProcessingMode != Models.Nodes.ImageProcessingMode.Manual)
                return false;

            // Do not handle if focused on input fields
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox || 
                Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
                return false;

            var modifiers = Keyboard.Modifiers;

            // 1. Layer navigation / Move tool nudging
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                if (EditorPanel.ActiveToolName == "Move" || EditorPanel.LayersList.IsKeyboardFocusWithin)
                    return true;
            }

            // 2. Spacebar for panning
            if (e.Key == Key.Space)
                return true;

            // 3. Brush size adjustment
            if (e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Brush" || tool == "Eraser")
                    return true;
            }

            // 4. Reset/Swap colors (X / D)
            if ((e.Key == Key.X || e.Key == Key.D) && modifiers == ModifierKeys.None)
                return true;

            // 5. Deselect (Ctrl + D)
            if (e.Key == Key.D && modifiers == ModifierKeys.Control)
                return true;

            // 6. Tool selection (V, B, E, M, I, G, T, C, K)
            if (modifiers == ModifierKeys.None || (modifiers == ModifierKeys.Shift && e.Key == Key.K))
            {
                if (e.Key == Key.V || e.Key == Key.B || e.Key == Key.E || e.Key == Key.M ||
                    e.Key == Key.I || e.Key == Key.G || e.Key == Key.T || e.Key == Key.C || e.Key == Key.K)
                    return true;
            }

            // 7. Crop / Transform / PolyLasso confirmation/cancellation (Enter / Escape)
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "CropCanvas" || tool == "Move" || tool == "Transform" || tool == "PolyLasso")
                    return true;
            }

            // 8. Layer actions: Ctrl+J (duplicate), Del (delete), Ctrl+E (merge), Ctrl+A/C/V/X
            if (e.Key == Key.J && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.A && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.C && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.V && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.X && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.Delete && (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift))
                return true;
            if (e.Key == Key.E && modifiers == ModifierKeys.Control)
                return true;

            // 9. Undo/Redo: Ctrl+Z, Ctrl+Y
            if (e.Key == Key.Z && modifiers == ModifierKeys.Control)
                return true;
            if (e.Key == Key.Y && modifiers == ModifierKeys.Control)
                return true;

            return false;
        }

        public void HandleShortcutKey(KeyEventArgs e)
        {
            ImageProcessingNodeContentControl_PreviewKeyDown(this, e);
        }

        public void CommitBrushDrawingSession()
        {
            if (_oldPixelsForUndo == null || _brushOverlayBitmap == null)
            {
                return;
            }

            var targetLayer = _brushSessionLayer ?? _node.EditorDoc?.ActiveLayer;
            if (targetLayer == null) return;

            int layerW = targetLayer.Width;
            int layerH = targetLayer.Height;
            int overlayW = _brushOverlayBitmap.PixelWidth;
            int overlayH = _brushOverlayBitmap.PixelHeight;

            // Tính offset: overlay có thể nhỏ hơn layer nếu có clip bounds
            int clipOffsetX = !_brushClipRect.IsEmpty ? (int)_brushClipRect.Left : 0;
            int clipOffsetY = !_brushClipRect.IsEmpty ? (int)_brushClipRect.Top : 0;
            
            targetLayer.Bitmap.Lock();
            try
            {
                var layerInfo = new SkiaSharp.SKImageInfo(layerW, layerH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(layerInfo, targetLayer.Bitmap.BackBuffer, targetLayer.Bitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    
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
                                // Vẽ overlay tại vị trí offset trên layer canvas
                                canvas.DrawBitmap(overlaySKBitmap, clipOffsetX, clipOffsetY, paint);
                            }
                        }
                    }
                    finally
                    {
                        _brushOverlayBitmap.Unlock();
                    }
                }
                
                // Dirty rect: chuyển từ overlay coords về layer coords
                int dirtyX = _strokeMinX + clipOffsetX;
                int dirtyY = _strokeMinY + clipOffsetY;
                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    // Clamp dirty rect to layer bounds
                    dirtyX = Math.Max(0, Math.Min(dirtyX, layerW - 1));
                    dirtyY = Math.Max(0, Math.Min(dirtyY, layerH - 1));
                    dirtyW = Math.Min(dirtyW, layerW - dirtyX);
                    dirtyH = Math.Min(dirtyH, layerH - dirtyY);
                    if (dirtyW > 0 && dirtyH > 0)
                        targetLayer.Bitmap.AddDirtyRect(new Int32Rect(dirtyX, dirtyY, dirtyW, dirtyH));
                }
            }
            finally
            {
                targetLayer.Bitmap.Unlock();
            }

            int stride = layerW * 4;
            int pixelSize = stride * layerH;
            var finalNewPixels = new byte[pixelSize];
            targetLayer.Bitmap.CopyPixels(finalNewPixels, stride, 0);

            var cmd = new PixelEditCommand(targetLayer, _oldPixelsForUndo, finalNewPixels);
            _node.EditorDoc.History.Execute(cmd);

            foreach (var path in _sessionPaths)
            {
                path.Dispose();
            }
            _sessionPaths.Clear();
            _sessionPaints.Clear();
            _sessionStrokes.Clear();
            _strokeIsComplexHistory.Clear();
            if (_cachedBrushTip != null)
            {
                _cachedBrushTip.Dispose();
                _cachedBrushTip = null;
            }
            _oldPixelsForUndo = null;
            _brushOverlayBitmap = null;
            _brushSessionLayer = null;
            // NOTE: Don't hide overlay here — let OnEditorDocumentModified() hide it after composite renders
            // to avoid 1-frame flicker.

            FlushCompositeAndSync();
        }

        public void ForceClearDrawingOverlay()
        {
            CommitBrushDrawingSession();
            
            try
            {
                CommitActiveText();
            }
            catch { }

            if (_sessionPaths != null)
            {
                foreach (var path in _sessionPaths)
                {
                    path.Dispose();
                }
                _sessionPaths.Clear();
            }
            _sessionPaints?.Clear();
            _sessionStrokes.Clear();
            _strokeIsComplexHistory.Clear();
            if (_cachedBrushTip != null)
            {
                _cachedBrushTip.Dispose();
                _cachedBrushTip = null;
            }
            _oldPixelsForUndo = null;
            _brushOverlayBitmap = null;
            _brushSessionLayer = null;

            if (ActiveLayerDrawingOverlay != null)
            {
                ActiveLayerDrawingOverlay.Source = null;
                ActiveLayerDrawingOverlay.Visibility = Visibility.Collapsed;
            }

            if (EyedropperPreviewContainer != null)
            {
                EyedropperPreviewContainer.Visibility = Visibility.Collapsed;
            }
        }

    }
}
