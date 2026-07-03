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
        #region MOUSE DRAWING ENGINE FOR IMAGE EDITOR MODE

        private bool _isDrawingPixels;
        private byte[]? _tempDrawingPixels;
        private byte[]? _strokeAlphaMask;
        private Point _lastDrawingPixelPoint;
        private byte[]? _oldPixelsForUndo;
        private BrushPreset _currentBrushPreset = BrushPreset.RoundHard;
        private readonly Random _brushRng = new();

        // ── Throttled composite during drawing (avoid lag) ──
        private DispatcherTimer? _compositeTimer;
        private bool _compositeDirty;

        /// <summary>Đánh dấu cần composite lại — timer sẽ xử lý ở tick tiếp theo (~30fps).</summary>
        private void MarkCompositeDirty()
        {
            _compositeDirty = true;
            if (_compositeTimer == null)
            {
                _compositeTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
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
                var composite = _node.EditorDoc.Composite();
                MainImage.Source = composite;
            }
            catch { /* ignore */ }
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
        private byte[]? _moveBasePixels;
        private byte[]? _moveFloatingPixels;
        private Geometry? _moveInitialGeometry;
        private byte[]? _moveInitialFullPixels;

        private void HandleManualEditorMouseDown(MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked || !activeLayer.IsVisible) return;

            string tool = EditorPanel.ActiveToolName;
            var clickPos = e.GetPosition(MainImage);

            if (tool != "Text" && TextMoveContainer != null && TextMoveContainer.Visibility == Visibility.Visible)
            {
                CommitActiveText();
            }

            if (clickPos.X < 0 || clickPos.X > MainImage.ActualWidth ||
                clickPos.Y < 0 || clickPos.Y > MainImage.ActualHeight)
                return;

            double scaleX = activeLayer.Width / MainImage.ActualWidth;
            double scaleY = activeLayer.Height / MainImage.ActualHeight;
            int px = (int)(clickPos.X * scaleX);
            int py = (int)(clickPos.Y * scaleY);

            if (tool == "Move")
            {
                int visibleLayersCount = _node.EditorDoc.Layers.Count(l => l.IsVisible);
                bool canGrab = false;

                if (visibleLayersCount >= 2)
                {
                    canGrab = true;
                }
                else
                {
                    // Check if clicked inside selection
                    if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
                    {
                        if (px >= _cachedSelectionStartX && px <= _cachedSelectionEndX &&
                            py >= _cachedSelectionStartY && py <= _cachedSelectionEndY)
                        {
                            canGrab = _cachedSelectionMask[px - _cachedSelectionStartX, py - _cachedSelectionStartY];
                        }
                    }

                    // Or if clicked on a non-transparent pixel of the active layer
                    if (!canGrab)
                    {
                        int stride = activeLayer.Width * 4;
                        byte[] singlePixel = new byte[4];
                        activeLayer.Bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), singlePixel, 4, 0);
                        if (singlePixel[3] > 0) // Alpha > 0
                        {
                            canGrab = true;
                        }
                    }
                }

                if (!canGrab) return; // ignore click

                _isMovingLayer = true;
                _moveStartMousePos = clickPos;

                int w = activeLayer.Width;
                int h = activeLayer.Height;
                int strideValue = w * 4;
                _moveInitialFullPixels = new byte[strideValue * h];
                activeLayer.Bitmap.CopyPixels(_moveInitialFullPixels, strideValue, 0);

                if (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
                {
                    _moveInitialGeometry = _activeSelectionGeometry.Clone();

                    // Cut the selection:
                    _moveBasePixels = new byte[strideValue * h];
                    _moveFloatingPixels = new byte[strideValue * h];
                    Array.Copy(_moveInitialFullPixels, _moveBasePixels, _moveInitialFullPixels.Length);

                    // Separate selected pixels
                    for (int y = 0; y < h; y++)
                    {
                        int rowOffset = y * strideValue;
                        for (int x = 0; x < w; x++)
                        {
                            bool isInside = IsInsideSelection(x, y);
                            if (isInside)
                            {
                                _moveFloatingPixels[rowOffset + x * 4] = _moveInitialFullPixels[rowOffset + x * 4];
                                _moveFloatingPixels[rowOffset + x * 4 + 1] = _moveInitialFullPixels[rowOffset + x * 4 + 1];
                                _moveFloatingPixels[rowOffset + x * 4 + 2] = _moveInitialFullPixels[rowOffset + x * 4 + 2];
                                _moveFloatingPixels[rowOffset + x * 4 + 3] = _moveInitialFullPixels[rowOffset + x * 4 + 3];

                                _moveBasePixels[rowOffset + x * 4] = 0;
                                _moveBasePixels[rowOffset + x * 4 + 1] = 0;
                                _moveBasePixels[rowOffset + x * 4 + 2] = 0;
                                _moveBasePixels[rowOffset + x * 4 + 3] = 0;
                            }
                        }
                    }
                }
                else
                {
                    _moveBasePixels = null;
                    _moveFloatingPixels = null;
                    _moveInitialGeometry = null;
                }

                MainScrollViewer.CaptureMouse();
                return;
            }

            if (tool == "Eyedropper")
            {
                PickColorWithEyedropper(px, py);
                return;
            }

            if (tool == "Selection")
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
                _selectionPoints.Add(new Point(px, py));
                SelectionBoxRect.Visibility = Visibility.Collapsed;
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
                    _selectionPoints.Add(new Point(px, py));
                    UpdatePolyLassoPreview(clickPos);
                    MainScrollViewer.CaptureMouse();
                }
                else
                {
                    _selectionPoints.Add(new Point(px, py));
                    UpdatePolyLassoPreview(clickPos);
                }
                return;
            }

            if (tool == "Text")
            {
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

                // Create a new text layer (Photoshop style)
                var doc = _node.EditorDoc;
                if (doc != null)
                {
                    var textLayer = new EditorLayer(doc.Width, doc.Height, "Text Layer " + (doc.Layers.Count + 1));
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

            if (tool == "Brush" || tool == "Eraser")
            {
                _isDrawingPixels = true;
                _lastDrawingPixelPoint = new Point(px, py);

                int w = activeLayer.Width;
                int h = activeLayer.Height;
                int stride = w * 4;
                _oldPixelsForUndo = new byte[stride * h];
                activeLayer.Bitmap.CopyPixels(_oldPixelsForUndo, stride, 0);

                _tempDrawingPixels = new byte[stride * h];
                Array.Copy(_oldPixelsForUndo, _tempDrawingPixels, _oldPixelsForUndo.Length);

                _strokeAlphaMask = new byte[w * h];

                bool isEraser = (tool == "Eraser");
                double radius = EditorPanel.BrushSize;
                double hardness = EditorPanel.BrushHardness;
                double flow = EditorPanel.BrushFlow;
                Color color = _node.EditorDoc.ForegroundColor;

                DrawBrushCircle(_strokeAlphaMask, w, h, px, py, radius, hardness, flow, _currentBrushPreset);
                ApplyStrokeToPixels(_tempDrawingPixels, _oldPixelsForUndo, _strokeAlphaMask, w, h, color, isEraser);

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), _tempDrawingPixels, stride, 0);
                // Defer composite — chỉ đánh dấu dirty, timer sẽ composite ở ~30fps
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

            double scaleX = activeLayer.Width / MainImage.ActualWidth;
            double scaleY = activeLayer.Height / MainImage.ActualHeight;
            int px = Math.Clamp((int)(mousePos.X * scaleX), 0, activeLayer.Width - 1);
            int py = Math.Clamp((int)(mousePos.Y * scaleY), 0, activeLayer.Height - 1);

            if (_isSelecting)
            {
                if (tool == "Selection")
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
                else if (tool == "Lasso")
                {
                    var newPt = new Point(px, py);
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
                return;
            }

            if (tool == "Move" && _isMovingLayer)
            {
                double docDeltaX = (mousePos.X - _moveStartMousePos.X) * scaleX;
                double docDeltaY = (mousePos.Y - _moveStartMousePos.Y) * scaleY;

                int dx = (int)Math.Round(docDeltaX);
                int dy = (int)Math.Round(docDeltaY);

                int w = activeLayer.Width;
                int h = activeLayer.Height;
                int moveStride = w * 4;

                var tempPixels = new byte[moveStride * h];

                if (_moveFloatingPixels != null && _moveBasePixels != null)
                {
                    Array.Copy(_moveBasePixels, tempPixels, tempPixels.Length);

                    for (int y = 0; y < h; y++)
                    {
                        int srcY = y - dy;
                        if (srcY < 0 || srcY >= h) continue;

                        for (int x = 0; x < w; x++)
                        {
                            int srcX = x - dx;
                            if (srcX < 0 || srcX >= w) continue;

                            byte srcAlpha = _moveFloatingPixels[srcY * moveStride + srcX * 4 + 3];
                            if (srcAlpha > 0)
                            {
                                int destIdx = y * moveStride + x * 4;
                                int srcIdx = srcY * moveStride + srcX * 4;
                                tempPixels[destIdx] = _moveFloatingPixels[srcIdx];
                                tempPixels[destIdx + 1] = _moveFloatingPixels[srcIdx + 1];
                                tempPixels[destIdx + 2] = _moveFloatingPixels[srcIdx + 2];
                                tempPixels[destIdx + 3] = _moveFloatingPixels[srcIdx + 3];
                            }
                        }
                    }

                    if (_moveInitialGeometry != null)
                    {
                        var transform = new TranslateTransform(dx, dy);
                        _activeSelectionGeometry = Geometry.Combine(_moveInitialGeometry, Geometry.Empty, GeometryCombineMode.Union, transform);
                        UpdatePolygonDisplay();
                    }
                }
                else
                {
                    for (int y = 0; y < h; y++)
                    {
                        int srcY = y - dy;
                        int destRowOffset = y * moveStride;

                        for (int x = 0; x < w; x++)
                        {
                            int srcX = x - dx;
                            int destIdx = destRowOffset + x * 4;

                            if (srcX >= 0 && srcX < w && srcY >= 0 && srcY < h)
                            {
                                int srcIdx = srcY * moveStride + srcX * 4;
                                tempPixels[destIdx] = _moveInitialFullPixels[srcIdx];
                                tempPixels[destIdx + 1] = _moveInitialFullPixels[srcIdx + 1];
                                tempPixels[destIdx + 2] = _moveInitialFullPixels[srcIdx + 2];
                                tempPixels[destIdx + 3] = _moveInitialFullPixels[srcIdx + 3];
                            }
                            else
                            {
                                tempPixels[destIdx + 3] = 0;
                            }
                        }
                    }
                }

                activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), tempPixels, moveStride, 0);
                MarkCompositeDirty();
                return;
            }

            if (!_isDrawingPixels || _tempDrawingPixels == null) return;

            bool isEraser = (tool == "Eraser");
            double radius = EditorPanel.BrushSize;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;
            Color color = _node.EditorDoc.ForegroundColor;

            var currentPoint = new Point(px, py);
            DrawBrushLine(_strokeAlphaMask, activeLayer.Width, activeLayer.Height, _lastDrawingPixelPoint, currentPoint, radius, hardness, flow, _currentBrushPreset);
            _lastDrawingPixelPoint = currentPoint;

            ApplyStrokeToPixels(_tempDrawingPixels, _oldPixelsForUndo, _strokeAlphaMask, activeLayer.Width, activeLayer.Height, color, isEraser);

            int stride = activeLayer.Width * 4;
            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), _tempDrawingPixels, stride, 0);
            // Defer composite — chỉ đánh dấu dirty, timer xử lý
            MarkCompositeDirty();
        }

        private void HandleManualEditorMouseUp()
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;

            if (_isMovingLayer)
            {
                _isMovingLayer = false;
                MainScrollViewer.ReleaseMouseCapture();

                if (activeLayer != null && _moveInitialFullPixels != null)
                {
                    int moveStride = activeLayer.Width * 4;
                    var finalPixels = new byte[moveStride * activeLayer.Height];
                    activeLayer.Bitmap.CopyPixels(finalPixels, moveStride, 0);

                    var moveCmd = new PixelEditCommand(activeLayer, _moveInitialFullPixels, finalPixels);
                    _node.EditorDoc.History.Execute(moveCmd);

                    if (_activeSelectionGeometry != null)
                    {
                        BuildSelectionMask();
                    }

                    activeLayer.InvalidateThumbnail();
                    OnEditorDocumentModified();
                }

                _moveInitialFullPixels = null;
                _moveBasePixels = null;
                _moveFloatingPixels = null;
                _moveInitialGeometry = null;
                return;
            }

            if (_isSelecting)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Selection")
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
                            var rectGeom = new RectangleGeometry(new Rect(lx, ly, lw, lh));
                            ApplyNewGeometry(rectGeom);
                        }
                        else
                        {
                            ClearSelection();
                        }
                    }
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

            if (activeLayer == null || _oldPixelsForUndo == null) return;

            int stride = activeLayer.Width * 4;
            var newPixels = new byte[stride * activeLayer.Height];
            activeLayer.Bitmap.CopyPixels(newPixels, stride, 0);

            var cmd = new PixelEditCommand(activeLayer, _oldPixelsForUndo, newPixels);
            _node.EditorDoc.History.Execute(cmd);

            _tempDrawingPixels = null;
            _oldPixelsForUndo = null;
            _strokeAlphaMask = null;

            // Flush: dừng timer, sync thumbnail + composite cuối cùng
            FlushCompositeAndSync();
        }

        private void ClearSelection()
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
        }

        private void ApplyNewGeometry(Geometry newGeometry)
        {
            if (_activeSelectionGeometry == null || _currentSelectionMode == SelectionMode.New)
            {
                _activeSelectionGeometry = newGeometry;
            }
            else if (_currentSelectionMode == SelectionMode.Add)
            {
                _activeSelectionGeometry = Geometry.Combine(_activeSelectionGeometry, newGeometry, GeometryCombineMode.Union, null);
            }
            else if (_currentSelectionMode == SelectionMode.Subtract)
            {
                _activeSelectionGeometry = Geometry.Combine(_activeSelectionGeometry, newGeometry, GeometryCombineMode.Exclude, null);
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
            if (SelectionPreviewPolygon != null)
            {
                SelectionPreviewPolygon.Visibility = Visibility.Collapsed;
                SelectionPreviewPolygon.Data = null;
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

            if (_activeSelectionGeometry != null)
            {
                double scaleX = MainImage.ActualWidth / activeLayer.Width;
                double scaleY = MainImage.ActualHeight / activeLayer.Height;

                var scaledGeometry = _activeSelectionGeometry.Clone();
                scaledGeometry.Transform = new ScaleTransform(scaleX, scaleY);

                var outlined = scaledGeometry.GetOutlinedPathGeometry();

                SelectionPolygon.Data = outlined;
                SelectionPolygon.Visibility = Visibility.Visible;

                if (SelectionPolygonBg != null)
                {
                    SelectionPolygonBg.Data = outlined;
                    SelectionPolygonBg.Visibility = Visibility.Visible;
                }
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
            }
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

            double scaleX = MainImage.ActualWidth / activeLayer.Width;
            double scaleY = MainImage.ActualHeight / activeLayer.Height;

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
            SelectionPreviewPolygon.Visibility = Visibility.Visible;
        }

        private void UpdateLassoPreview()
        {
            if (_node.EditorDoc == null || SelectionPreviewPolygon == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            double scaleX = MainImage.ActualWidth / activeLayer.Width;
            double scaleY = MainImage.ActualHeight / activeLayer.Height;

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
            SelectionPreviewPolygon.Visibility = Visibility.Visible;
        }


        private void ImageProcessingNodeContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
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

                // Ctrl+D: Deselect
                if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    ClearSelection();
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
                }
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

        private void DrawBrushCircle(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow,
            BrushPreset preset = BrushPreset.RoundHard)
        {
            switch (preset)
            {
                case BrushPreset.RoundSoft:
                    DrawBrush_RoundSoft(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Flat:
                    DrawBrush_Flat(alphaMask, width, height, cx, cy, radius, hardness, flow);
                    break;
                case BrushPreset.Chalk:
                    DrawBrush_Chalk(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Spray:
                    DrawBrush_Spray(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Scatter:
                    DrawBrush_Scatter(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Pencil:
                    DrawBrush_Pencil(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                default: // RoundHard
                    DrawBrush_RoundHard(alphaMask, width, height, cx, cy, radius, hardness, flow);
                    break;
            }
        }

        #region ═══ BRUSH PRESETS ═══

        /// <summary>Round Hard — cọ tròn cứng (mặc định gốc).</summary>
        private void DrawBrush_RoundHard(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            int startX = Math.Max(0, (int)Math.Floor(cx - radius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
            int startY = Math.Max(0, (int)Math.Floor(cy - radius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));

            double r2 = radius * radius;
            double innerRadius = radius * (hardness / 100.0);
            double flowMul = flow / 100.0;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double dist = Math.Sqrt(dist2);
                        double pixelOpacity = 1.0;
                        if (dist > innerRadius)
                        {
                            if (radius - innerRadius > 0.001)
                                pixelOpacity = 1.0 - (dist - innerRadius) / (radius - innerRadius);
                            else
                                pixelOpacity = 0.0;
                        }

                        byte brushAlpha = (byte)(pixelOpacity * flowMul * 255.0);
                        if (brushAlpha <= 0) continue;

                        int maskOffset = rowOffset + x;
                        if (brushAlpha > alphaMask[maskOffset])
                            alphaMask[maskOffset] = brushAlpha;
                    }
                }
            }
        }

        /// <summary>Round Soft — gaussian-like smooth falloff.</summary>
        private void DrawBrush_RoundSoft(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            int startX = Math.Max(0, (int)Math.Floor(cx - radius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
            int startY = Math.Max(0, (int)Math.Floor(cy - radius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));

            double r2 = radius * radius;
            double flowMul = flow / 100.0;
            // Gaussian sigma: falloff curve — sigma = radius / 2.5 cho hiệu ứng mềm
            double sigma = radius / 2.5;
            double sigma2x2 = 2.0 * sigma * sigma;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double gaussianOpacity = Math.Exp(-dist2 / sigma2x2);
                        byte brushAlpha = (byte)(gaussianOpacity * flowMul * 255.0);
                        if (brushAlpha <= 0) continue;

                        int maskOffset = rowOffset + x;
                        if (brushAlpha > alphaMask[maskOffset])
                            alphaMask[maskOffset] = brushAlpha;
                    }
                }
            }
        }

        /// <summary>Flat — cọ dẹp hình chữ nhật ngang, ratio ~3:1.</summary>
        private void DrawBrush_Flat(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            double halfW = radius;          // chiều rộng = diameter
            double halfH = radius / 3.0;    // chiều cao = 1/3 diameter
            double flowMul = flow / 100.0;
            double hardnessMul = hardness / 100.0;

            int startX = Math.Max(0, (int)Math.Floor(cx - halfW));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + halfW));
            int startY = Math.Max(0, (int)Math.Floor(cy - halfH));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + halfH));

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = Math.Abs(y - cy) / halfH; // 0..1

                for (int x = startX; x <= endX; x++)
                {
                    double dx = Math.Abs(x - cx) / halfW; // 0..1
                    if (dx > 1.0 || dy > 1.0) continue;
                    if (!IsInsideSelection(x, y)) continue;

                    // Edge softness
                    double edgeDist = Math.Max(dx, dy);
                    double pixelOpacity = 1.0;
                    double innerEdge = hardnessMul;
                    if (edgeDist > innerEdge && innerEdge < 1.0)
                        pixelOpacity = 1.0 - (edgeDist - innerEdge) / (1.0 - innerEdge);

                    byte brushAlpha = (byte)(pixelOpacity * flowMul * 255.0);
                    if (brushAlpha <= 0) continue;

                    int maskOffset = rowOffset + x;
                    if (brushAlpha > alphaMask[maskOffset])
                        alphaMask[maskOffset] = brushAlpha;
                }
            }
        }

        /// <summary>Chalk — phấn, noise-modulated circle mask.</summary>
        private void DrawBrush_Chalk(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            int startX = Math.Max(0, (int)Math.Floor(cx - radius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
            int startY = Math.Max(0, (int)Math.Floor(cy - radius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));

            double r2 = radius * radius;
            double flowMul = flow / 100.0;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        // Noise: random gaps tạo hiệu ứng phấn gritty
                        if (_brushRng.NextDouble() < 0.35) continue; // 35% pixel bị bỏ

                        double dist = Math.Sqrt(dist2);
                        double falloff = 1.0 - (dist / radius);
                        // Thêm noise vào opacity
                        double noiseMul = 0.4 + _brushRng.NextDouble() * 0.6;
                        double pixelOpacity = falloff * noiseMul;

                        byte brushAlpha = (byte)Math.Clamp(pixelOpacity * flowMul * 255.0, 0, 255);
                        if (brushAlpha <= 0) continue;

                        int maskOffset = rowOffset + x;
                        if (brushAlpha > alphaMask[maskOffset])
                            alphaMask[maskOffset] = brushAlpha;
                    }
                }
            }
        }

        /// <summary>Spray — bình xịt, scatter random dots trong bán kính.</summary>
        private void DrawBrush_Spray(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double flowMul = flow / 100.0;
            // Số dots tỷ lệ với diện tích brush
            int dotCount = (int)(radius * radius * 0.15);
            dotCount = Math.Clamp(dotCount, 8, 2000);

            for (int i = 0; i < dotCount; i++)
            {
                // Random point trong circle
                double angle = _brushRng.NextDouble() * Math.PI * 2;
                double r = Math.Sqrt(_brushRng.NextDouble()) * radius; // uniform distribution in circle
                int px = (int)(cx + Math.Cos(angle) * r);
                int py = (int)(cy + Math.Sin(angle) * r);

                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                if (!IsInsideSelection(px, py)) continue;

                // Opacity giảm theo khoảng cách
                double distRatio = r / radius;
                double dotOpacity = (1.0 - distRatio * 0.5) * flowMul;
                byte brushAlpha = (byte)Math.Clamp(dotOpacity * 255.0, 0, 255);
                if (brushAlpha <= 0) continue;

                int maskOffset = py * width + px;
                if (brushAlpha > alphaMask[maskOffset])
                    alphaMask[maskOffset] = brushAlpha;
            }
        }

        /// <summary>Scatter — điểm rải rác, random positions + size variation.</summary>
        private void DrawBrush_Scatter(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double flowMul = flow / 100.0;
            // Tạo 3-7 blobs ngẫu nhiên quanh tâm
            int blobCount = 3 + _brushRng.Next(5);

            for (int b = 0; b < blobCount; b++)
            {
                // Random offset
                double offsetX = (_brushRng.NextDouble() - 0.5) * radius * 1.5;
                double offsetY = (_brushRng.NextDouble() - 0.5) * radius * 1.5;
                double blobCx = cx + offsetX;
                double blobCy = cy + offsetY;
                double blobRadius = radius * (0.15 + _brushRng.NextDouble() * 0.35);

                int startX = Math.Max(0, (int)Math.Floor(blobCx - blobRadius));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(blobCx + blobRadius));
                int startY = Math.Max(0, (int)Math.Floor(blobCy - blobRadius));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(blobCy + blobRadius));
                double br2 = blobRadius * blobRadius;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - blobCy;
                    double dy2 = dy * dy;
                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - blobCx;
                        if (dx * dx + dy2 <= br2)
                        {
                            if (!IsInsideSelection(x, y)) continue;
                            double dist = Math.Sqrt(dx * dx + dy2);
                            double falloff = 1.0 - (dist / blobRadius);
                            byte brushAlpha = (byte)Math.Clamp(falloff * flowMul * 255.0, 0, 255);
                            if (brushAlpha <= 0) continue;
                            int maskOffset = rowOffset + x;
                            if (brushAlpha > alphaMask[maskOffset])
                                alphaMask[maskOffset] = brushAlpha;
                        }
                    }
                }
            }
        }

        /// <summary>Pencil — bút chì, nét nhỏ cứng với slight jitter.</summary>
        private void DrawBrush_Pencil(byte[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            // Pencil: cap radius nhỏ hơn, hardness = 100%
            double pencilRadius = Math.Min(radius, Math.Max(1.5, radius * 0.5));
            double flowMul = flow / 100.0;

            // Slight position jitter
            cx += (_brushRng.NextDouble() - 0.5) * 0.8;
            cy += (_brushRng.NextDouble() - 0.5) * 0.8;

            int startX = Math.Max(0, (int)Math.Floor(cx - pencilRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + pencilRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - pencilRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + pencilRadius));
            double r2 = pencilRadius * pencilRadius;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        // Hard edge — full opacity within circle
                        byte brushAlpha = (byte)(flowMul * 255.0);
                        if (brushAlpha <= 0) continue;

                        int maskOffset = rowOffset + x;
                        if (brushAlpha > alphaMask[maskOffset])
                            alphaMask[maskOffset] = brushAlpha;
                    }
                }
            }
        }

        #endregion

        private void DrawBrushLine(byte[] alphaMask, int width, int height, Point p1, Point p2, double radius, double hardness, double flow,
            BrushPreset preset = BrushPreset.RoundHard)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len == 0)
            {
                DrawBrushCircle(alphaMask, width, height, p1.X, p1.Y, radius, hardness, flow, preset);
                return;
            }

            double step = Math.Max(1.0, radius / 4.0);
            // Spray/Scatter cần step lớn hơn để không quá dense
            if (preset == BrushPreset.Spray || preset == BrushPreset.Scatter)
                step = Math.Max(step, radius * 0.5);

            for (double d = 0; d <= len; d += step)
            {
                double cx = p1.X + (dx * d / len);
                double cy = p1.Y + (dy * d / len);
                DrawBrushCircle(alphaMask, width, height, cx, cy, radius, hardness, flow, preset);
            }
            DrawBrushCircle(alphaMask, width, height, p2.X, p2.Y, radius, hardness, flow, preset);
        }

        private void ApplyStrokeToPixels(byte[] destPixels, byte[] srcPixels, byte[] alphaMask, int width, int height, Color color, bool isEraser)
        {
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                int pixelRowOffset = rowOffset * 4;
                for (int x = 0; x < width; x++)
                {
                    if (_selectionRect.HasValue)
                    {
                        if (x < _selectionRect.Value.Left || x > _selectionRect.Value.Right ||
                            y < _selectionRect.Value.Top || y > _selectionRect.Value.Bottom)
                        {
                            int pixelOffset = pixelRowOffset + x * 4;
                            destPixels[pixelOffset] = srcPixels[pixelOffset];
                            destPixels[pixelOffset + 1] = srcPixels[pixelOffset + 1];
                            destPixels[pixelOffset + 2] = srcPixels[pixelOffset + 2];
                            destPixels[pixelOffset + 3] = srcPixels[pixelOffset + 3];
                            continue;
                        }
                    }

                    int maskOffset = rowOffset + x;
                    byte maskAlphaByte = alphaMask[maskOffset];
                    if (maskAlphaByte == 0)
                    {
                        int pixelOffset = pixelRowOffset + x * 4;
                        destPixels[pixelOffset] = srcPixels[pixelOffset];
                        destPixels[pixelOffset + 1] = srcPixels[pixelOffset + 1];
                        destPixels[pixelOffset + 2] = srcPixels[pixelOffset + 2];
                        destPixels[pixelOffset + 3] = srcPixels[pixelOffset + 3];
                        continue;
                    }

                    double maskAlpha = maskAlphaByte / 255.0;
                    int pOffset = pixelRowOffset + x * 4;

                    if (isEraser)
                    {
                        byte srcA = srcPixels[pOffset + 3];
                        destPixels[pOffset] = srcPixels[pOffset];
                        destPixels[pOffset + 1] = srcPixels[pOffset + 1];
                        destPixels[pOffset + 2] = srcPixels[pOffset + 2];
                        destPixels[pOffset + 3] = (byte)Math.Clamp(srcA * (1.0 - maskAlpha), 0, 255);
                    }
                    else
                    {
                        byte bB = srcPixels[pOffset];
                        byte bG = srcPixels[pOffset + 1];
                        byte bR = srcPixels[pOffset + 2];
                        byte bA = srcPixels[pOffset + 3];

                        double srcA = color.A / 255.0 * maskAlpha;
                        double dstA = bA / 255.0;
                        double outA = srcA + dstA * (1.0 - srcA);

                        if (outA > 0)
                        {
                            byte outR = (byte)Math.Clamp(((color.R * srcA) + (bR * dstA * (1.0 - srcA))) / outA, 0, 255);
                            byte outG = (byte)Math.Clamp(((color.G * srcA) + (bG * dstA * (1.0 - srcA))) / outA, 0, 255);
                            byte outB = (byte)Math.Clamp(((color.B * srcA) + (bB * dstA * (1.0 - srcA))) / outA, 0, 255);

                            destPixels[pOffset] = outB;
                            destPixels[pOffset + 1] = outG;
                            destPixels[pOffset + 2] = outR;
                            destPixels[pOffset + 3] = (byte)(outA * 255.0);
                        }
                        else
                        {
                            destPixels[pOffset] = 0;
                            destPixels[pOffset + 1] = 0;
                            destPixels[pOffset + 2] = 0;
                            destPixels[pOffset + 3] = 0;
                        }
                    }
                }
            }
        }

        private void FloodFill(byte[] pixels, int width, int height, int startX, int startY, Color fillColor)
        {
            int stride = width * 4;
            int offset = startY * stride + startX * 4;
            byte targetB = pixels[offset];
            byte targetG = pixels[offset + 1];
            byte targetR = pixels[offset + 2];
            byte targetA = pixels[offset + 3];

            byte fillB = fillColor.B;
            byte fillG = fillColor.G;
            byte fillR = fillColor.R;
            byte fillA = fillColor.A;

            if (targetB == fillB && targetG == fillG && targetR == fillR && targetA == fillA)
                return;

            var queue = new System.Collections.Generic.Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                int x = (int)p.X;
                int y = (int)p.Y;

                if (x < 0 || x >= width || y < 0 || y >= height) continue;

                if (_selectionRect.HasValue)
                {
                    if (x < _selectionRect.Value.Left || x > _selectionRect.Value.Right ||
                        y < _selectionRect.Value.Top || y > _selectionRect.Value.Bottom)
                    {
                        continue;
                    }
                }

                int currentOffset = y * stride + x * 4;
                if (pixels[currentOffset] == targetB &&
                    pixels[currentOffset + 1] == targetG &&
                    pixels[currentOffset + 2] == targetR &&
                    pixels[currentOffset + 3] == targetA)
                {
                    pixels[currentOffset] = fillB;
                    pixels[currentOffset + 1] = fillG;
                    pixels[currentOffset + 2] = fillR;
                    pixels[currentOffset + 3] = fillA;

                    queue.Enqueue(new Point(x + 1, y));
                    queue.Enqueue(new Point(x - 1, y));
                    queue.Enqueue(new Point(x, y + 1));
                    queue.Enqueue(new Point(x, y - 1));
                }
            }
        }

        private void PickColorWithEyedropper(int px, int py)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            if (px >= 0 && px < activeLayer.Width && py >= 0 && py < activeLayer.Height)
            {
                var stride = 4;
                var singlePixel = new byte[4];
                activeLayer.Bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), singlePixel, stride, 0);

                Color picked = Color.FromArgb(singlePixel[3], singlePixel[2], singlePixel[1], singlePixel[0]);
                _node.EditorDoc.ForegroundColor = picked;
            }
        }

        #endregion

        #region ═══ PHOTOSHOP STYLE BRUSH POPUP & PREVIEWS ═══

        private bool _isSyncingBrushProperties = false;

        private void SyncFromEditorPanelBrushProperties()
        {
            if (EditorPanel == null || _isSyncingBrushProperties) return;
            _isSyncingBrushProperties = true;
            try
            {
                if (OptBrushSizeInput != null && OptBrushSizeInput.Text != ((int)EditorPanel.BrushSize).ToString())
                {
                    OptBrushSizeInput.Text = ((int)EditorPanel.BrushSize).ToString();
                }

                if (PopupBrushSize != null && PopupBrushSize.Value != EditorPanel.BrushSize)
                {
                    PopupBrushSize.Value = EditorPanel.BrushSize;
                }

                if (PopupBrushHardness != null && PopupBrushHardness.Value != EditorPanel.BrushHardness)
                {
                    PopupBrushHardness.Value = EditorPanel.BrushHardness;
                }

                if (PopupBrushFlow != null && PopupBrushFlow.Value != EditorPanel.BrushFlow)
                {
                    PopupBrushFlow.Value = EditorPanel.BrushFlow;
                }

                if (PopupBrushPreset != null && PopupBrushPreset.SelectedIndex != (int)_currentBrushPreset)
                {
                    PopupBrushPreset.SelectedIndex = (int)_currentBrushPreset;
                }

                // Update preview representations
                UpdateBrushPreviewVisuals();
            }
            finally
            {
                _isSyncingBrushProperties = false;
            }
        }

        private void UpdateBrushPreviewVisuals()
        {
            if (EditorPanel == null) return;

            if (BrushBarPreviewContainer != null)
            {
                BrushBarPreviewContainer.Content = CreateBrushPreviewElement(20, 20, _currentBrushPreset, EditorPanel.BrushSize, EditorPanel.BrushHardness, EditorPanel.BrushFlow, Colors.White);
            }
            if (BrushPopupPreviewContainer != null)
            {
                BrushPopupPreviewContainer.Content = CreateBrushPreviewElement(68, 68, _currentBrushPreset, EditorPanel.BrushSize, EditorPanel.BrushHardness, EditorPanel.BrushFlow, Colors.White);
            }
        }

        private UIElement CreateBrushPreviewElement(double width, double height, BrushPreset preset, double actualSize, double hardness, double flow, Color brushColor)
        {
            double maxDiameter = height - 4;
            // PREVIEW SIZE: Ignore actualSize to make preview fill the container and represent shape clearly
            double diameter = maxDiameter;
            double radius = diameter / 2.0;

            // Normalize hardness and flow since they are in 0-100 and 1-100 ranges
            double f = flow > 1.0 ? flow / 100.0 : flow;
            double h = hardness > 1.0 ? hardness / 100.0 : hardness;

            var grid = new Grid { Width = width, Height = height };

            switch (preset)
            {
                case BrushPreset.RoundSoft:
                    {
                        var ellipse = new Ellipse
                        {
                            Width = diameter,
                            Height = diameter,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var gradient = new RadialGradientBrush();
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255 * 0.5), brushColor.R, brushColor.G, brushColor.B), 0.4));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;

                case BrushPreset.Flat:
                    {
                        var rect = new Rectangle
                        {
                            Width = diameter,
                            Height = Math.Max(2.0, diameter / 3.0),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            RadiusX = 1,
                            RadiusY = 1
                        };
                        var brush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(0, 1)
                        };
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), h));
                        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        rect.Fill = brush;
                        grid.Children.Add(rect);
                    }
                    break;

                case BrushPreset.Chalk:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        var rand = new Random((int)actualSize + (int)hardness);
                        double centerX = width / 2;
                        double centerY = height / 2;
                        
                        for (int i = 0; i < 15; i++)
                        {
                            double angle = rand.NextDouble() * Math.PI * 2;
                            double r = rand.NextDouble() * radius;
                            double dotSize = 1.5 + rand.NextDouble() * 2.0;

                            var dot = new Ellipse
                            {
                                Width = dotSize,
                                Height = dotSize,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 180), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + Math.Cos(angle) * r - dotSize / 2);
                            Canvas.SetTop(dot, centerY + Math.Sin(angle) * r - dotSize / 2);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Spray:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        var rand = new Random(42);
                        double centerX = width / 2;
                        double centerY = height / 2;

                        for (int i = 0; i < 30; i++)
                        {
                            double angle = rand.NextDouble() * Math.PI * 2;
                            double r = Math.Sqrt(rand.NextDouble()) * radius;
                            double dotSize = 1.0;

                            var dot = new Ellipse
                            {
                                Width = dotSize,
                                Height = dotSize,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 200 * (1.0 - (r / radius) * 0.5)), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + Math.Cos(angle) * r - dotSize / 2);
                            Canvas.SetTop(dot, centerY + Math.Sin(angle) * r - dotSize / 2);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Scatter:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        var rand = new Random(2026);
                        double centerX = width / 2;
                        double centerY = height / 2;

                        for (int i = 0; i < 6; i++)
                        {
                            double angle = rand.NextDouble() * Math.PI * 2;
                            double r = rand.NextDouble() * radius * 1.1;
                            double dotSize = 2.0 + rand.NextDouble() * 4.0;

                            var dot = new Ellipse
                            {
                                Width = dotSize,
                                Height = dotSize,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 160), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + Math.Cos(angle) * r - dotSize / 2);
                            Canvas.SetTop(dot, centerY + Math.Sin(angle) * r - dotSize / 2);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Pencil:
                    {
                        double pencilSize = Math.Max(2.0, diameter * 0.4);
                        var ellipse = new Ellipse
                        {
                            Width = pencilSize,
                            Height = pencilSize,
                            Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        grid.Children.Add(ellipse);
                    }
                    break;

                default: // RoundHard
                    {
                        var ellipse = new Ellipse
                        {
                            Width = diameter,
                            Height = diameter,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var gradient = new RadialGradientBrush();
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                        double stopOffset = Math.Max(0.0, h);
                        if (stopOffset < 0.99)
                        {
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), stopOffset));
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        }
                        else
                        {
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255), brushColor.R, brushColor.G, brushColor.B), 0.99));
                            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        }
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;
            }

            return grid;
        }

        private void OptBrushSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyBrushSizeFromTextBox();
        }

        private void OptBrushSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyBrushSizeFromTextBox();
                MainScrollViewer.Focus();
                e.Handled = true;
            }
        }

        private void ApplyBrushSizeFromTextBox()
        {
            if (OptBrushSizeInput == null || EditorPanel == null) return;
            if (double.TryParse(OptBrushSizeInput.Text, out double size))
            {
                size = Math.Clamp(size, 1.0, 200.0);
                if (EditorPanel.SliderBrushSize != null && EditorPanel.SliderBrushSize.Value != size)
                {
                    EditorPanel.SliderBrushSize.Value = size;
                }
            }
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        private void BtnBrushPreviewDropdown_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (BrushSettingsPopup != null)
            {
                if (BrushSettingsPopup.IsOpen)
                {
                    BrushSettingsPopup.IsOpen = false;
                }
                else
                {
                    SyncFromEditorPanelBrushProperties();
                    BrushSettingsPopup.IsOpen = true;

                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.PreviewMouseDown -= Window_PreviewMouseDown;
                        window.PreviewMouseDown += Window_PreviewMouseDown;
                    }
                }
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BrushSettingsPopup == null || !BrushSettingsPopup.IsOpen) return;

            var child = BrushSettingsPopup.Child as FrameworkElement;
            if (child != null)
            {
                var mousePosPopup = e.GetPosition(child);
                var popupRect = new Rect(0, 0, child.ActualWidth, child.ActualHeight);

                var mousePosBtn = e.GetPosition(BtnBrushPreviewDropdown);
                var btnRect = new Rect(0, 0, BtnBrushPreviewDropdown.ActualWidth, BtnBrushPreviewDropdown.ActualHeight);

                if (!popupRect.Contains(mousePosPopup) && !btnRect.Contains(mousePosBtn))
                {
                    BrushSettingsPopup.IsOpen = false;

                    var window = sender as Window;
                    if (window != null)
                    {
                        window.PreviewMouseDown -= Window_PreviewMouseDown;
                    }
                }
            }
        }

        private void BrushSettingsPopup_Closed(object sender, EventArgs e)
        {
            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= Window_PreviewMouseDown;
            }
        }

        private void PopupBrushPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PopupBrushPreset == null || _isSyncingBrushProperties || EditorPanel == null) return;
            _currentBrushPreset = (BrushPreset)PopupBrushPreset.SelectedIndex;

            if (_currentBrushPreset == BrushPreset.RoundHard)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 100;
            }
            else if (_currentBrushPreset == BrushPreset.RoundSoft)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 0;
            }

            if (_currentBrushPreset == BrushPreset.Pencil)
            {
                if (EditorPanel.SliderBrushHardness != null) EditorPanel.SliderBrushHardness.Value = 100;
            }

            if (PopupHardnessPanel != null)
            {
                bool useHardness = (_currentBrushPreset == BrushPreset.RoundHard || _currentBrushPreset == BrushPreset.Flat);
                PopupHardnessPanel.Opacity = useHardness ? 1.0 : 0.4;
                PopupBrushHardness.IsEnabled = useHardness;
            }

            SyncFromEditorPanelBrushProperties();
            UpdateBrushCursorPosition();
        }

        private void PopupBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushSize != null && EditorPanel.SliderBrushSize.Value != e.NewValue)
            {
                EditorPanel.SliderBrushSize.Value = e.NewValue;
            }
            if (TxtPopupBrushSize != null)
            {
                TxtPopupBrushSize.Text = $"{(int)e.NewValue}px";
            }
            if (OptBrushSizeInput != null && OptBrushSizeInput.Text != ((int)e.NewValue).ToString())
            {
                OptBrushSizeInput.Text = ((int)e.NewValue).ToString();
            }
            UpdateBrushPreviewVisuals();
        }

        private void PopupBrushHardness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushHardness != null && EditorPanel.SliderBrushHardness.Value != e.NewValue)
            {
                EditorPanel.SliderBrushHardness.Value = e.NewValue;
            }
            if (TxtPopupBrushHardness != null)
            {
                TxtPopupBrushHardness.Text = $"{(int)e.NewValue}%";
            }
            UpdateBrushPreviewVisuals();
        }

        private void PopupBrushFlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBrushProperties || EditorPanel == null) return;
            if (EditorPanel.SliderBrushFlow != null && EditorPanel.SliderBrushFlow.Value != e.NewValue)
            {
                EditorPanel.SliderBrushFlow.Value = e.NewValue;
            }
            if (TxtPopupBrushFlow != null)
            {
                TxtPopupBrushFlow.Text = $"{(int)e.NewValue}%";
            }
            UpdateBrushPreviewVisuals();
        }

        private bool IsInsideSelection(int x, int y)
        {
            if (_activeSelectionGeometry == null) return true;

            if (_hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                if (x >= _cachedSelectionStartX && x <= _cachedSelectionEndX &&
                    y >= _cachedSelectionStartY && y <= _cachedSelectionEndY)
                {
                    return _cachedSelectionMask[x - _cachedSelectionStartX, y - _cachedSelectionStartY];
                }
                return false;
            }
            return true;
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
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            var outlinedGeom = _activeSelectionGeometry.GetOutlinedPathGeometry();
            var bounds = outlinedGeom.Bounds;

            _cachedSelectionStartX = Math.Max(0, (int)Math.Floor(bounds.Left));
            _cachedSelectionEndX = Math.Min(activeLayer.Width - 1, (int)Math.Ceiling(bounds.Right));
            _cachedSelectionStartY = Math.Max(0, (int)Math.Floor(bounds.Top));
            _cachedSelectionEndY = Math.Min(activeLayer.Height - 1, (int)Math.Ceiling(bounds.Bottom));

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

        #endregion
    }
}
