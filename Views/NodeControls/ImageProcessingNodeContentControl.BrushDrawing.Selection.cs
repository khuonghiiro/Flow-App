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
        internal void ClearSelection()
        {
            _selectionRect = null;
            _selectionPoints.Clear();
            _isSelecting = false;
            _hasCachedSelectionMask = false;
            _cachedSelectionMask = null;
            _activeSelectionGeometry = null;
            if (SelectionBoxRect != null)
            {
                SelectionBoxRect.Visibility = Visibility.Collapsed;
            }
            if (SelectionBoxRectBg != null)
            {
                SelectionBoxRectBg.Visibility = Visibility.Collapsed;
            }
            if (SelectionPolygon != null)
            {
                SelectionPolygon.Visibility = Visibility.Collapsed;
                SelectionPolygon.Data = null;
            }
            if (SelectionPolygonBg != null)
            {
                SelectionPolygonBg.Visibility = Visibility.Collapsed;
                SelectionPolygonBg.Data = null;
            }
            if (SelectionPreviewPolygon != null)
            {
                SelectionPreviewPolygon.Visibility = Visibility.Collapsed;
                SelectionPreviewPolygon.Data = null;
            }
            if (SelectionPreviewPolygonBg != null)
            {
                SelectionPreviewPolygonBg.Visibility = Visibility.Collapsed;
                SelectionPreviewPolygonBg.Data = null;
            }

            var activeLayer = _node?.EditorDoc?.ActiveLayer;
            if (activeLayer != null)
            {
                activeLayer.TempSelectionGeometry = null;
                if (activeLayer.TempSelectionPath != null)
                {
                    activeLayer.TempSelectionPath.Dispose();
                    activeLayer.TempSelectionPath = null;
                }
            }

            _activeSelectionPathSK?.Dispose();
            _activeSelectionPathSK = null;
        }

        private void CommitSelectionMove(EditorLayer originalLayer, byte[] sourceFullPixels, int dx, int dy)
        {
            if (_node.EditorDoc == null || _moveInitialGeometry == null)
                return;

            int w = originalLayer.Width;
            int h = originalLayer.Height;
            int stride = w * 4;

            // 1. Convert WPF _moveInitialGeometry to Skia SKPath (shifted to originalLayer's local space)
            SkiaSharp.SKPath origSelectionPath = null;
            try
            {
                var localGeom = _moveInitialGeometry.Clone();
                localGeom.Transform = new TranslateTransform(-originalLayer.OffsetX, -originalLayer.OffsetY);
                var flatGeom = PathGeometry.CreateFromGeometry(localGeom);
                var svgData = flatGeom.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (svgData.StartsWith("F0") || svgData.StartsWith("F1"))
                {
                    svgData = svgData.Substring(2).Trim();
                }
                origSelectionPath = SkiaSharp.SKPath.ParseSvgPathData(svgData);
            }
            catch { }

            if (origSelectionPath == null)
                return;

            // 2. Prepare Skia bitmaps and perform clipping using blend modes (safe from StackOverflowException)
            var srcInfo = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            
            byte[] basePixels = new byte[stride * h];
            byte[] newLayerPixels = new byte[stride * h];

            var gcSource = System.Runtime.InteropServices.GCHandle.Alloc(sourceFullPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            var gcBase = System.Runtime.InteropServices.GCHandle.Alloc(basePixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            var gcNew = System.Runtime.InteropServices.GCHandle.Alloc(newLayerPixels, System.Runtime.InteropServices.GCHandleType.Pinned);

            try
            {
                using (var srcBmp = new SkiaSharp.SKBitmap())
                using (var baseBmp = new SkiaSharp.SKBitmap())
                using (var newBmp = new SkiaSharp.SKBitmap())
                {
                    srcBmp.InstallPixels(srcInfo, gcSource.AddrOfPinnedObject(), stride);
                    baseBmp.InstallPixels(srcInfo, gcBase.AddrOfPinnedObject(), stride);
                    newBmp.InstallPixels(srcInfo, gcNew.AddrOfPinnedObject(), stride);

                    // Draw base layer (subtract selection area)
                    using (var canvasBase = new SkiaSharp.SKCanvas(baseBmp))
                    {
                        canvasBase.Clear(SkiaSharp.SKColors.Transparent);
                        canvasBase.DrawBitmap(srcBmp, 0, 0);

                        using (var paint = new SkiaSharp.SKPaint())
                        {
                            paint.IsAntialias = true;
                            paint.Style = SkiaSharp.SKPaintStyle.Fill;
                            paint.Color = SkiaSharp.SKColors.Black;
                            paint.BlendMode = SkiaSharp.SKBlendMode.DstOut;
                            canvasBase.DrawPath(origSelectionPath, paint);
                        }
                    }

                    // Draw new layer (shifted selection area)
                    using (var canvasNew = new SkiaSharp.SKCanvas(newBmp))
                    {
                        canvasNew.Clear(SkiaSharp.SKColors.Transparent);
                        canvasNew.Save();
                        canvasNew.Translate(dx, dy);

                        using (var paintMask = new SkiaSharp.SKPaint())
                        {
                            paintMask.IsAntialias = true;
                            paintMask.Style = SkiaSharp.SKPaintStyle.Fill;
                            paintMask.Color = SkiaSharp.SKColors.Black;
                            canvasNew.DrawPath(origSelectionPath, paintMask);
                        }

                        using (var paintSrc = new SkiaSharp.SKPaint())
                        {
                            paintSrc.BlendMode = SkiaSharp.SKBlendMode.SrcIn;
                            canvasNew.DrawBitmap(srcBmp, 0, 0, paintSrc);
                        }

                        canvasNew.Restore();
                    }
                }
            }
            finally
            {
                gcSource.Free();
                gcBase.Free();
                gcNew.Free();
            }

            // Setup the tight bounding box and original transform bitmap for the new layer using blend modes
            var localGeomForBounds = _moveInitialGeometry.Clone();
            localGeomForBounds.Transform = new TranslateTransform(-originalLayer.OffsetX, -originalLayer.OffsetY);
            var bounds = localGeomForBounds.Bounds;
            int startX = Math.Max(0, (int)Math.Floor(bounds.Left));
            int startY = Math.Max(0, (int)Math.Floor(bounds.Top));
            int endX = Math.Min(w - 1, (int)Math.Ceiling(bounds.Right));
            int endY = Math.Min(h - 1, (int)Math.Ceiling(bounds.Bottom));
            int bw = endX - startX + 1;
            int bh = endY - startY + 1;

            string newLayerName = $"{originalLayer.Name} Selection";
            EditorLayer newLayer;

            if (bw > 0 && bh > 0)
            {
                newLayer = new EditorLayer(bw, bh, newLayerName);
                newLayer.OffsetX = originalLayer.OffsetX + startX + (int)dx;
                newLayer.OffsetY = originalLayer.OffsetY + startY + (int)dy;

                int tightStride = bw * 4;
                var tightPixels = new byte[tightStride * bh];
                
                var gcTight = System.Runtime.InteropServices.GCHandle.Alloc(tightPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                var gcSourceForTight = System.Runtime.InteropServices.GCHandle.Alloc(sourceFullPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    var tightInfo = new SkiaSharp.SKImageInfo(bw, bh, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var srcBmp = new SkiaSharp.SKBitmap())
                    using (var tightBmp = new SkiaSharp.SKBitmap())
                    {
                        srcBmp.InstallPixels(srcInfo, gcSourceForTight.AddrOfPinnedObject(), stride);
                        tightBmp.InstallPixels(tightInfo, gcTight.AddrOfPinnedObject(), tightStride);

                        using (var canvasTight = new SkiaSharp.SKCanvas(tightBmp))
                        {
                            canvasTight.Clear(SkiaSharp.SKColors.Transparent);
                            canvasTight.Save();
                            canvasTight.Translate(-startX, -startY);

                            using (var paintMask = new SkiaSharp.SKPaint())
                            {
                                paintMask.IsAntialias = true;
                                paintMask.Style = SkiaSharp.SKPaintStyle.Fill;
                                paintMask.Color = SkiaSharp.SKColors.Black;
                                canvasTight.DrawPath(origSelectionPath, paintMask);
                            }

                            using (var paintSrc = new SkiaSharp.SKPaint())
                            {
                                paintSrc.BlendMode = SkiaSharp.SKBlendMode.SrcIn;
                                canvasTight.DrawBitmap(srcBmp, 0, 0, paintSrc);
                            }

                            canvasTight.Restore();
                        }
                    }
                }
                finally
                {
                    gcTight.Free();
                    gcSourceForTight.Free();
                }

                newLayer.Bitmap.WritePixels(new Int32Rect(0, 0, bw, bh), tightPixels, tightStride, 0);

                var origBmp = new WriteableBitmap(bw, bh, 96, 96, PixelFormats.Bgra32, null);
                origBmp.WritePixels(new Int32Rect(0, 0, bw, bh), tightPixels, tightStride, 0);
                newLayer.OriginalTransformBitmap = origBmp;
                newLayer.ContentBounds = new Rect(0, 0, bw, bh);
                newLayer.ImageContentBounds = newLayer.ContentBounds;
            }
            else
            {
                newLayer = new EditorLayer(1, 1, newLayerName);
            }

            if (_activeSelectionGeometry != null)
            {
                newLayer.ContentGeometry = _activeSelectionGeometry.Clone();
            }

            newLayer.InvalidateThumbnail();

            // Insert new layer above originalLayer
            int activeIndex = _node.EditorDoc.Layers.IndexOf(originalLayer);
            int insertIndex = activeIndex >= 0 ? activeIndex + 1 : _node.EditorDoc.Layers.Count;

            // Create the CutToNewLayerCommand (which does originalLayer pixel update & newLayer addition)
            var cutCmd = new CutToNewLayerCommand(
                _node.EditorDoc,
                originalLayer,
                sourceFullPixels,
                basePixels,
                newLayer,
                insertIndex
            );

            _node.EditorDoc.History.Execute(cutCmd);

            ClearSelection();
            EditorPanel.RefreshLayersList();
            OnEditorDocumentModified();
            origSelectionPath.Dispose();
        }

        private void ApplyNewGeometry(Geometry newGeometry)
        {
            if (newGeometry == null || newGeometry.IsEmpty())
                return;

            // 1. Convert newGeometry to SKPath
            SkiaSharp.SKPath newPath = null;
            try
            {
                var svg = newGeometry.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (svg.StartsWith("F0") || svg.StartsWith("F1"))
                {
                    svg = svg.Substring(2).Trim();
                }
                newPath = SkiaSharp.SKPath.ParseSvgPathData(svg);
            }
            catch { }

            if (newPath != null)
            {
                if (_activeSelectionPathSK == null || _currentSelectionMode == SelectionMode.New)
                {
                    _activeSelectionPathSK?.Dispose();
                    _activeSelectionPathSK = new SkiaSharp.SKPath(newPath);
                }
                else
                {
                    var combined = new SkiaSharp.SKPath();
                    if (_currentSelectionMode == SelectionMode.Add)
                    {
                        _activeSelectionPathSK.Op(newPath, SkiaSharp.SKPathOp.Union, combined);
                    }
                    else if (_currentSelectionMode == SelectionMode.Subtract)
                    {
                        _activeSelectionPathSK.Op(newPath, SkiaSharp.SKPathOp.Difference, combined);
                    }
                    _activeSelectionPathSK.Dispose();
                    _activeSelectionPathSK = combined;
                }
                newPath.Dispose();
            }

            // Clip selection to document/image bounds!
            if (_activeSelectionPathSK != null && !_activeSelectionPathSK.IsEmpty && _node.EditorDoc != null)
            {
                using (var imgBoundsPath = new SkiaSharp.SKPath())
                {
                    imgBoundsPath.AddRect(new SkiaSharp.SKRect(0, 0, _node.EditorDoc.Width, _node.EditorDoc.Height));
                    var clipped = new SkiaSharp.SKPath();
                    if (_activeSelectionPathSK.Op(imgBoundsPath, SkiaSharp.SKPathOp.Intersect, clipped))
                    {
                        _activeSelectionPathSK.Dispose();
                        _activeSelectionPathSK = clipped;
                    }
                    else
                    {
                        clipped.Dispose();
                    }
                }
            }

            // 2. Generate flat, clean _activeSelectionGeometry from _activeSelectionPathSK
            if (_activeSelectionPathSK != null && !_activeSelectionPathSK.IsEmpty)
            {
                try
                {
                    string svgData = _activeSelectionPathSK.ToSvgPathData();
                    _activeSelectionGeometry = Geometry.Parse(svgData);
                }
                catch
                {
                    _activeSelectionGeometry = null;
                }
            }
            else
            {
                ClearSelection();
            }

            if (_activeSelectionGeometry != null)
            {
                var bounds = _activeSelectionGeometry.Bounds;
                _selectionRect = bounds;
            }
            else
            {
                _selectionRect = null;
            }

            BuildSelectionMask();
            UpdatePolygonDisplay();

            _selectionPoints.Clear();
            if (SelectionBoxRect != null) SelectionBoxRect.Visibility = Visibility.Collapsed;
            if (SelectionBoxRectBg != null) SelectionBoxRectBg.Visibility = Visibility.Collapsed;
            if (SelectionPreviewPolygon != null)
            {
                SelectionPreviewPolygon.Visibility = Visibility.Collapsed;
                SelectionPreviewPolygon.Data = null;
            }
            if (SelectionPreviewPolygonBg != null)
            {
                SelectionPreviewPolygonBg.Visibility = Visibility.Collapsed;
                SelectionPreviewPolygonBg.Data = null;
            }
        }

        private static bool GetLineIntersection(Point A, Point B, Point C, Point D, out Point intersection)
        {
            intersection = new Point();

            double rxs = (B.X - A.X) * (D.Y - C.Y) - (B.Y - A.Y) * (D.X - C.X);
            if (Math.Abs(rxs) < 1e-9)
            {
                // Parallel or collinear
                return false;
            }

            double t = ((C.X - A.X) * (D.Y - C.Y) - (C.Y - A.Y) * (D.X - C.X)) / rxs;
            double u = ((C.X - A.X) * (B.Y - A.Y) - (C.Y - A.Y) * (B.X - A.X)) / rxs;

            if (t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0)
            {
                intersection.X = A.X + t * (B.X - A.X);
                intersection.Y = A.Y + t * (B.Y - A.Y);
                return true;
            }

            return false;
        }

        private void UpdatePolygonDisplay()
        {
            if (_node.EditorDoc == null || SelectionPolygon == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            if (_isMovingLayer || _isKeyMoving)
            {
                SelectionPolygon.Visibility = Visibility.Collapsed;
                if (SelectionPolygonBg != null)
                {
                    SelectionPolygonBg.Visibility = Visibility.Collapsed;
                }
                return;
            }

            if (_activeSelectionGeometry != null)
            {
                double scaleX = MainImage.ActualWidth / _node.EditorDoc.Width;
                double scaleY = MainImage.ActualHeight / _node.EditorDoc.Height;

                var scaledGeometry = _activeSelectionGeometry.Clone();
                scaledGeometry.Transform = new ScaleTransform(scaleX, scaleY);

                // Scale stroke thickness inversely with zoom — ~1px on screen
                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;
                double strokeW = 1.0 / zoom;

                SelectionPolygon.Data = scaledGeometry;
                SelectionPolygon.StrokeThickness = strokeW;
                SelectionPolygon.StrokeDashArray = new DoubleCollection { 2.0, 2.0 };
                SelectionPolygon.Visibility = Visibility.Visible;

                if (SelectionPolygonBg != null)
                {
                    SelectionPolygonBg.Data = scaledGeometry;
                    SelectionPolygonBg.StrokeThickness = strokeW;
                    SelectionPolygonBg.Visibility = Visibility.Visible;
                }

                // Start color-swap animation timer
                StartMarchingAnts();
            }
            else
            {
                SelectionPolygon.Data = null;
                SelectionPolygon.Visibility = Visibility.Collapsed;

                if (SelectionPolygonBg != null)
                {
                    SelectionPolygonBg.Data = null;
                    SelectionPolygonBg.Visibility = Visibility.Collapsed;
                }

                // Stop color-swap animation timer
                StopMarchingAnts();
            }
        }

        private void StartMarchingAnts()
        {
            if (SelectionPolygon == null || _marchingAntsAnimation != null) return;

            // WPF BeginAnimation runs on the composition thread — hardware accelerated, no jitter
            _marchingAntsAnimation = new DoubleAnimation
            {
                From = 0,
                To = 4,               // dash total = 2+2 = 4 → seamless loop
                Duration = new Duration(TimeSpan.FromSeconds(0.6)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            SelectionPolygon.BeginAnimation(System.Windows.Shapes.Path.StrokeDashOffsetProperty, _marchingAntsAnimation);
        }

        private void StopMarchingAnts()
        {
            if (SelectionPolygon != null)
            {
                SelectionPolygon.BeginAnimation(System.Windows.Shapes.Path.StrokeDashOffsetProperty, null);
            }
            _marchingAntsAnimation = null;
        }

        private void ClosePolyLassoSelection()
        {
            _isSelecting = false;
            MainScrollViewer.ReleaseMouseCapture();

            if (_selectionPoints.Count >= 3)
            {
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

        private void UpdatePolyLassoPreview(Point currentMousePos)
        {
            if (_node.EditorDoc == null || SelectionPreviewPolygon == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            double scaleX = MainImage.ActualWidth / _node.EditorDoc.Width;
            double scaleY = MainImage.ActualHeight / _node.EditorDoc.Height;

            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();
            if (_selectionPoints.Count > 0)
            {
                pathFigure.StartPoint = new Point(_selectionPoints[0].X * scaleX, _selectionPoints[0].Y * scaleY);
                for (int i = 1; i < _selectionPoints.Count; i++)
                {
                    pathFigure.Segments.Add(new LineSegment(new Point(_selectionPoints[i].X * scaleX, _selectionPoints[i].Y * scaleY), true));
                }
                pathFigure.Segments.Add(new LineSegment(currentMousePos, true));
                pathGeometry.Figures.Add(pathFigure);
                SelectionPreviewPolygon.Data = pathGeometry;
            }
            ApplyPreviewStrokeStyle();
            SelectionPreviewPolygon.Visibility = Visibility.Visible;
        }

        private void UpdateLassoPreview()
        {
            if (_node.EditorDoc == null || SelectionPreviewPolygon == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            double scaleX = MainImage.ActualWidth / _node.EditorDoc.Width;
            double scaleY = MainImage.ActualHeight / _node.EditorDoc.Height;

            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();
            if (_selectionPoints.Count > 0)
            {
                pathFigure.StartPoint = new Point(_selectionPoints[0].X * scaleX, _selectionPoints[0].Y * scaleY);
                for (int i = 1; i < _selectionPoints.Count; i++)
                {
                    pathFigure.Segments.Add(new LineSegment(new Point(_selectionPoints[i].X * scaleX, _selectionPoints[i].Y * scaleY), true));
                }
                pathGeometry.Figures.Add(pathFigure);
                SelectionPreviewPolygon.Data = pathGeometry;
            }
            ApplyPreviewStrokeStyle();
            SelectionPreviewPolygon.Visibility = Visibility.Visible;
        }

        /// <summary>Apply Photoshop-style black+white stroke with zoom scaling to preview paths.</summary>
        private void ApplyPreviewStrokeStyle()
        {
            double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
            if (zoom <= 0) zoom = 1.0;
            // Photoshop always shows ~1px stroke on screen
            double strokeW = 1.0 / zoom;

            if (SelectionPreviewPolygon != null)
            {
                SelectionPreviewPolygon.StrokeThickness = strokeW;
                SelectionPreviewPolygon.StrokeDashArray = new DoubleCollection { 2.0, 2.0 };
            }
            if (SelectionPreviewPolygonBg != null)
            {
                SelectionPreviewPolygonBg.Data = SelectionPreviewPolygon?.Data;
                SelectionPreviewPolygonBg.StrokeThickness = strokeW;
                SelectionPreviewPolygonBg.Visibility = Visibility.Visible;
            }
            // Also scale SelectionBoxRect (marquee) if it's visible
            if (SelectionBoxRect != null && SelectionBoxRect.Visibility == Visibility.Visible)
            {
                SelectionBoxRect.StrokeThickness = strokeW;
                SelectionBoxRect.StrokeDashArray = new DoubleCollection { 2.0, 2.0 };
            }
            // Sync black background for marquee rect
            if (SelectionBoxRectBg != null && SelectionBoxRect != null && SelectionBoxRect.Visibility == Visibility.Visible)
            {
                SelectionBoxRectBg.StrokeThickness = strokeW;
                SelectionBoxRectBg.Margin = SelectionBoxRect.Margin;
                SelectionBoxRectBg.Width = SelectionBoxRect.Width;
                SelectionBoxRectBg.Height = SelectionBoxRect.Height;
                SelectionBoxRectBg.Visibility = Visibility.Visible;
            }
        }


        private void ImageProcessingNodeContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Do not intercept if mouse is outside and not in FloatingWidgetWindow
            bool isInWidget = _ownerWindow is FlowMy.Views.Overlays.FloatingWidgetWindow;
            if (!isInWidget && !this.IsMouseOver && !this.IsKeyboardFocusWithin)
            {
                return;
            }

            // Do not intercept if typing in a TextBox/ComboBox or renaming layer
            if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox)
            {
                // But handle Escape key inside TextEditorBox to cancel
                if (e.Key == Key.Escape && e.OriginalSource == TextEditorBox)
                {
                    CancelActiveText();
                    e.Handled = true;
                }
                return;
            }

            if (_isKeyMoving && e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.LeftShift && e.Key != Key.RightShift)
            {
                CommitKeyMoveSession();
            }

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                // Enter key to crop active selection to new layer
                if (e.Key == Key.Enter)
                {
                    if (_activeSelectionGeometry != null && _hasCachedSelectionMask)
                    {
                        CopyActiveSelection();
                        PasteSelectionAsLayer();
                        ClearSelection();
                        e.Handled = true;
                        return;
                    }
                }

                // Arrow keys nudging for Move tool
                string activeTool = EditorPanel.ActiveToolName;
                if (activeTool == "Move")
                {
                    if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
                    {
                        var activeLayer = _node.EditorDoc?.ActiveLayer;
                        if (activeLayer != null && !activeLayer.IsLocked && activeLayer.IsVisible)
                        {
                            StartKeyMoveSession(activeLayer);
                            _keyMoveCommitTimer?.Stop();
                            _keyMoveCommitTimer?.Start();

                            int delta = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
                            int dx = 0;
                            int dy = 0;
                            if (e.Key == Key.Left) dx = -delta;
                            else if (e.Key == Key.Right) dx = delta;
                            else if (e.Key == Key.Up) dy = -delta;
                            else if (e.Key == Key.Down) dy = delta;

                            _keyDeltaX += dx;
                            _keyDeltaY += dy;

                            ApplyTemporaryKeyMove(activeLayer);
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // Spacebar panning key down
                if (e.Key == Key.Space && !e.IsRepeat)
                {
                    _isSpacePressed = true;
                    MainScrollViewer.Cursor = Cursors.Hand;
                    e.Handled = true;
                    return;
                }

                // Brush sizing keys: [ and ]
                if (e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets)
                {
                    string tool = EditorPanel.ActiveToolName;
                    if (tool == "Brush" || tool == "Eraser")
                    {
                        double curSize = EditorPanel.SliderBrushSize.Value;
                        double change = e.Key == Key.OemOpenBrackets ? -Math.Max(1, Math.Round(curSize * 0.1)) : Math.Max(1, Math.Round(curSize * 0.1));
                        double newSize = Math.Clamp(curSize + change, 1, 200);
                        EditorPanel.SliderBrushSize.Value = newSize;
                        e.Handled = true;
                        return;
                    }
                }

                // Swapping / resetting color shortcuts: X and D
                if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
                {
                    if (_node.EditorDoc != null)
                    {
                        var temp = _node.EditorDoc.ForegroundColor;
                        _node.EditorDoc.ForegroundColor = _node.EditorDoc.BackgroundColor;
                        _node.EditorDoc.BackgroundColor = temp;
                        SyncToolboxColors();
                        e.Handled = true;
                        return;
                    }
                }
                if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
                {
                    if (_node.EditorDoc != null)
                    {
                        _node.EditorDoc.ForegroundColor = Colors.Black;
                        _node.EditorDoc.BackgroundColor = Colors.White;
                        SyncToolboxColors();
                        e.Handled = true;
                        return;
                    }
                }

                // Ctrl+S: Save Project
                if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    SaveProjectZip(BtnSaveProjectQuick);
                    e.Handled = true;
                    return;
                }

                // Ctrl+D: Deselect
                if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    ClearSelection();
                    e.Handled = true;
                    return;
                }

                // Ctrl+A: Select All — create selection encompassing the active layer's content bounds
                if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (_node.EditorDoc != null)
                    {
                        var activeLayer = _node.EditorDoc.ActiveLayer;
                        if (activeLayer != null)
                        {
                            if (activeLayer.ContentGeometry != null)
                            {
                                _activeSelectionGeometry = activeLayer.ContentGeometry.Clone();
                                _selectionRect = activeLayer.ContentGeometry.Bounds;
                            }
                            else
                            {
                                var bounds = activeLayer.ContentBounds;
                                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                                {
                                    bounds = EditorLayer.CalculateContentBounds(activeLayer.Bitmap);
                                }
                                var fullRect = new RectangleGeometry(bounds);
                                _activeSelectionGeometry = fullRect;
                                _selectionRect = bounds;
                            }
                            BuildSelectionMask();
                            UpdatePolygonDisplay();
                        }
                    }
                    e.Handled = true;
                    return;
                }

                // Ctrl+J: Duplicate Layer(s)
                if (e.Key == Key.J && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    EditorPanel.DuplicateSelectedLayers();
                    e.Handled = true;
                    return;
                }

                // Delete: Delete Layer(s)
                if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    EditorPanel.DeleteSelectedLayers();
                    e.Handled = true;
                    return;
                }

                // Ctrl+E: Merge Layer(s)
                if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    EditorPanel.MergeSelectedLayers();
                    e.Handled = true;
                    return;
                }

                // Ctrl+Z: Undo in manual editor
                if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (_sessionPaths.Count > 0)
                    {
                        UndoLastBrushStroke();
                    }
                    else
                    {
                        CommitBrushDrawingSession();
                        EditorPanel.UndoAction();
                    }
                    e.Handled = true;
                    return;
                }

                // Ctrl+Y: Redo in manual editor
                if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    EditorPanel.RedoAction();
                    e.Handled = true;
                    return;
                }

                // Tool selection shortcuts (V, B, E, M, I, G, T) when not typing
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
                {
                    if (e.Key == Key.V)
                    {
                        EditorPanel.SelectToolByName("Move");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.B)
                    {
                        EditorPanel.SelectToolByName("Brush");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.E)
                    {
                        EditorPanel.SelectToolByName("Eraser");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.M)
                    {
                        EditorPanel.SelectToolByName("Selection");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.I)
                    {
                        EditorPanel.SelectToolByName("Eyedropper");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.G)
                    {
                        EditorPanel.SelectToolByName("Fill");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.T)
                    {
                        EditorPanel.SelectToolByName("Text");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.C)
                    {
                        EditorPanel.SelectToolByName("CropCanvas");
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.K)
                    {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        {
                            if (EditorPanel.ActiveToolName == "Slice")
                                EditorPanel.SelectToolByName("SliceSelect");
                            else
                                EditorPanel.SelectToolByName("Slice");
                        }
                        else
                        {
                            EditorPanel.SelectToolByName("Slice");
                        }
                        SyncToolboxHighlight();
                        e.Handled = true;
                        return;
                    }
                }
            }

            // CropCanvas apply: Enter, cancel: Escape
            if (e.Key == Key.Enter && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && EditorPanel.ActiveToolName == "CropCanvas")
            {
                OptCropApply_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && EditorPanel.ActiveToolName == "CropCanvas")
            {
                OptCropCancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Move/Transform Apply (Enter) / Cancel (Esc)
            if (e.Key == Key.Enter && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && (EditorPanel.ActiveToolName == "Move" || EditorPanel.ActiveToolName == "Transform") && _transformSessionActive)
            {
                OptMoveApply_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && (EditorPanel.ActiveToolName == "Move" || EditorPanel.ActiveToolName == "Transform") && _transformSessionActive)
            {
                OptMoveCancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // PolyLasso close path: Enter
            if (e.Key == Key.Enter && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && EditorPanel.ActiveToolName == "PolyLasso" && _selectionPoints.Count >= 3)
            {
                ClosePolyLassoSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && (_selectionRect.HasValue || _selectionPoints.Count > 0))
            {
                ClearSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && (_selectionRect.HasValue || _selectionPoints.Count >= 3) && _node.EditorDoc != null)
            {
                DeleteSelectionContent();
                e.Handled = true;
                return;
            }

            // Ctrl+C: Copy active selection
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopyActiveSelection();
                e.Handled = true;
                return;
            }

            // Ctrl+V: Paste active selection
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteSelectionAsLayer();
                e.Handled = true;
                return;
            }

            // Ctrl+J: Layer via Copy or Duplicate Layer
            if (e.Key == Key.J && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_node.EditorDoc != null && _node.EditorDoc.ActiveLayer != null)
                {
                    CopyActiveSelection();
                    PasteSelectionAsLayer();
                    e.Handled = true;
                }
                return;
            }
        }

        private void ImageProcessingNodeContentControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && _node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                _isSpacePressed = false;
                if (_isPanning)
                {
                    _isPanning = false;
                    MainScrollViewer.ReleaseMouseCapture();
                }
                MainScrollViewer.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private bool IsInsideSelection(int localX, int localY)
        {
            if (_activeSelectionGeometry == null) return true;
            if (_node?.EditorDoc?.ActiveLayer == null) return true;

            int docX = localX + _node.EditorDoc.ActiveLayer.OffsetX;
            int docY = localY + _node.EditorDoc.ActiveLayer.OffsetY;

            if (!_hasCachedSelectionMask || _cachedSelectionMask == null)
            {
                BuildSelectionMask();
            }

            if (_hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                if (docX >= _cachedSelectionStartX && docX <= _cachedSelectionEndX &&
                    docY >= _cachedSelectionStartY && docY <= _cachedSelectionEndY)
                {
                    return _cachedSelectionMask[docX - _cachedSelectionStartX, docY - _cachedSelectionStartY];
                }
                return false;
            }
            return false;
        }

        private void BuildSelectionMask()
        {
            if (_activeSelectionGeometry == null || _activeSelectionGeometry.IsEmpty())
            {
                _hasCachedSelectionMask = false;
                _cachedSelectionMask = null;
                return;
            }

            if (_node.EditorDoc == null) return;

            var outlinedGeom = _activeSelectionGeometry.GetOutlinedPathGeometry();
            var bounds = outlinedGeom.Bounds;

            _cachedSelectionStartX = Math.Max(0, (int)Math.Floor(bounds.Left));
            _cachedSelectionEndX = Math.Min(_node.EditorDoc.Width - 1, (int)Math.Ceiling(bounds.Right));
            _cachedSelectionStartY = Math.Max(0, (int)Math.Floor(bounds.Top));
            _cachedSelectionEndY = Math.Min(_node.EditorDoc.Height - 1, (int)Math.Ceiling(bounds.Bottom));

            int w = _cachedSelectionEndX - _cachedSelectionStartX + 1;
            int h = _cachedSelectionEndY - _cachedSelectionStartY + 1;

            if (w > 0 && h > 0)
            {
                _cachedSelectionMask = new bool[w, h];

                // Create a DrawingVisual to draw the geometry shifted to (0,0)
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    dc.PushTransform(new TranslateTransform(-_cachedSelectionStartX, -_cachedSelectionStartY));
                    dc.DrawGeometry(Brushes.White, null, outlinedGeom);
                    dc.Pop();
                }

                // Render visual using WPF's rendering pipeline (extremely fast)
                var renderTarget = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);

                // Read pixels into byte array
                int stride = w * 4;
                var pixels = new byte[stride * h];
                renderTarget.CopyPixels(pixels, stride, 0);

                // Populate mask using alpha channel
                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        _cachedSelectionMask[x, y] = (alpha > 128);
                    }
                }

                _hasCachedSelectionMask = true;
            }
            else
            {
                _hasCachedSelectionMask = false;
                _cachedSelectionMask = null;
            }
        }

    }
}
