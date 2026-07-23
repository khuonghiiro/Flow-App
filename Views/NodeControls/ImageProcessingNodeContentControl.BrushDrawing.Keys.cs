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
                                var localGeom = _moveInitialGeometry.Clone();
                                localGeom.Transform = new TranslateTransform(-activeLayer.OffsetX, -activeLayer.OffsetY);
                                var flatGeom = PathGeometry.CreateFromGeometry(localGeom);
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
                PixelEditCommand moveCmd;

                if (isSelectionLayer)
                {
                    moveCmd = new PixelEditCommand(targetLayer, _moveInitialFullPixels, _moveInitialFullPixels,
                        targetLayer.LayerScaleX, targetLayer.LayerScaleY, targetLayer.LayerAngle,
                        targetLayer.LayerTranslateX, targetLayer.LayerTranslateY, targetLayer.OriginalTransformBitmap);

                    oldBounds = targetLayer.ContentBounds;
                    targetLayer.OffsetX += dxLayer;
                    targetLayer.OffsetY += dyLayer;
                    newBounds = new Rect(0, 0, targetLayer.Width, targetLayer.Height);
                    targetLayer.ContentBounds = newBounds;
                    targetLayer.ImageContentBounds = newBounds;

                    if (targetLayer.ContentGeometry != null)
                    {
                        var transform = new TranslateTransform(dxLayer, dyLayer);
                        targetLayer.ContentGeometry = Geometry.Combine(targetLayer.ContentGeometry, Geometry.Empty, GeometryCombineMode.Union, transform);
                    }

                    moveCmd.KeepOriginalTransformBitmap = true;
                    moveCmd.HasCustomBounds = true;
                    moveCmd.CustomOldContentBounds = oldBounds;
                    moveCmd.CustomNewContentBounds = newBounds;
                    moveCmd.CaptureNewTransformState();
                }
                else
                {
                    // Apply shift once
                    ShiftBitmapPixels(targetLayer, _moveInitialFullPixels, dxLayer, dyLayer);

                    int moveStride = targetLayer.Width * 4;
                    var finalPixels = new byte[moveStride * targetLayer.Height];
                    targetLayer.Bitmap.CopyPixels(finalPixels, moveStride, 0);

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

            // 10. Brush sizing shortcuts: Ctrl + and Ctrl -
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                bool isMinus = (e.Key == Key.OemMinus || e.Key == Key.Subtract);
                bool isPlus = (e.Key == Key.OemPlus || e.Key == Key.Add);
                if (isMinus || isPlus)
                {
                    string tool = EditorPanel.ActiveToolName;
                    if (tool == "Brush" || tool == "Eraser")
                        return true;
                }
            }

            return false;
        }

        public void HandleShortcutKey(KeyEventArgs e)
        {
            ImageProcessingNodeContentControl_PreviewKeyDown(this, e);
        }

        public void HandleShortcutKeyUp(KeyEventArgs e)
        {
            ImageProcessingNodeContentControl_PreviewKeyUp(this, e);
        }

        public void CommitBrushDrawingSession()
        {
            if (_brushOverlayBitmap == null)
            {
                return;
            }

            var targetLayer = _brushSessionLayer ?? _node.EditorDoc?.ActiveLayer;
            if (targetLayer == null) return;

            int layerW = targetLayer.Width;
            int layerH = targetLayer.Height;
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
            targetLayer.Bitmap.CopyPixels(dirtyRect, oldRegionPixels, regionStride, 0);

            // 3. Draw overlay onto layer (WITH lock only during drawing)
            targetLayer.Bitmap.Lock();
            try
            {
                var layerInfo = new SkiaSharp.SKImageInfo(layerW, layerH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(layerInfo, targetLayer.Bitmap.BackBuffer, targetLayer.Bitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    if (targetLayer.ContentGeometry != null)
                    {
                        using (var clipPath = ConvertGeometryToSKPath(targetLayer.ContentGeometry, -targetLayer.OffsetX, -targetLayer.OffsetY))
                        {
                            if (clipPath != null)
                            {
                                canvas.ClipPath(clipPath, SkiaSharp.SKClipOperation.Intersect, true);
                            }
                        }
                    }
                    else if (_activeSelectionGeometry != null)
                    {
                        using (var clipPath = ConvertGeometryToSKPath(_activeSelectionGeometry, -targetLayer.OffsetX, -targetLayer.OffsetY))
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
                
                // 4. Mark dirty
                targetLayer.Bitmap.AddDirtyRect(dirtyRect);
            }
            finally
            {
                targetLayer.Bitmap.Unlock();
            }

            // 5. Copy new pixels (WITHOUT lock)
            targetLayer.Bitmap.CopyPixels(dirtyRect, newRegionPixels, regionStride, 0);

            // 6. Execute region command
            var cmd = new PixelRegionEditCommand(targetLayer, dirtyRect, oldRegionPixels, newRegionPixels);
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
            _brushOverlayBitmap = null;
            _brushSessionLayer = null;

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
