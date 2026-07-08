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
        private float[]? _strokeAlphaMaskF;
        private float[]? _tempAlphaMaskF;
        private double _strokeDistanceAccumulator;
        private readonly List<Point> _strokePoints = new();
        private Point _lastDrawingPixelPoint;
        private byte[]? _oldPixelsForUndo;
        private BrushPreset _currentBrushPreset = BrushPreset.RoundHard;
        private readonly Random _brushRng = new();
        private int _strokeMinX;
        private int _strokeMinY;
        private int _strokeMaxX;
        private int _strokeMaxY;

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
        private Geometry? _moveInitialGeometry;
        private byte[]? _moveInitialFullPixels;
        private EditorLayer? _movingLayer;
        private double _accumulatedMoveDx = 0;
        private double _accumulatedMoveDy = 0;

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
                CommitKeyMoveSession();

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
                    activeLayer.TempMoveDx = 0;
                    activeLayer.TempMoveDy = 0;
                    _accumulatedMoveDx = 0;
                    _accumulatedMoveDy = 0;
                }
                else
                {
                    _moveInitialGeometry = null;
                    activeLayer.TempSelectionGeometry = null;
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
                double radius = EditorPanel.BrushSize;
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
                _selectionPoints.Add(new Point(px, py));
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

                _strokeAlphaMaskF = new float[w * h];
                _tempAlphaMaskF = new float[w * h];
                _strokePoints.Clear();
                _strokePoints.Add(new Point(px, py));
                _strokeDistanceAccumulator = 0.0;

                bool isEraser = (tool == "Eraser");
                double radius = EditorPanel.BrushSize;
                double hardness = EditorPanel.BrushHardness;
                double flow = EditorPanel.BrushFlow;
                Color color = _node.EditorDoc.ForegroundColor;

                double extendedRadius = radius + 2.0;
                if (_currentBrushPreset == BrushPreset.Chalk || _currentBrushPreset == BrushPreset.Spray || _currentBrushPreset == BrushPreset.Scatter ||
                    _currentBrushPreset == BrushPreset.Splatter || _currentBrushPreset == BrushPreset.Charcoal || _currentBrushPreset == BrushPreset.OilBrush)
                {
                    extendedRadius = radius * 3.5 + 5.0;
                }

                _strokeMinX = Math.Clamp((int)(px - extendedRadius), 0, w - 1);
                _strokeMaxX = Math.Clamp((int)(px + extendedRadius), 0, w - 1);
                _strokeMinY = Math.Clamp((int)(py - extendedRadius), 0, h - 1);
                _strokeMaxY = Math.Clamp((int)(py + extendedRadius), 0, h - 1);

                DrawBrushCircle(_strokeAlphaMaskF, w, h, px, py, radius, hardness, flow, _currentBrushPreset);
                ApplyStrokeToPixels(_tempDrawingPixels, _oldPixelsForUndo, _strokeAlphaMaskF, w, h, color, isEraser, _strokeMinX, _strokeMinY, _strokeMaxX, _strokeMaxY);

                int dirtyW = _strokeMaxX - _strokeMinX + 1;
                int dirtyH = _strokeMaxY - _strokeMinY + 1;
                if (dirtyW > 0 && dirtyH > 0)
                {
                    activeLayer.Bitmap.WritePixels(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH), _tempDrawingPixels, stride, _strokeMinX, _strokeMinY);
                }
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
                if (tool == "Selection" || tool == "ObjectSelection")
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
                else if (tool == "QuickSelection")
                {
                    double qSelRadius = EditorPanel.BrushSize;
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

            if (!_isDrawingPixels || _tempDrawingPixels == null || _strokeAlphaMaskF == null || _tempAlphaMaskF == null) return;

            bool isEraser = (tool == "Eraser");
            double radius = EditorPanel.BrushSize;
            double hardness = EditorPanel.BrushHardness;
            double flow = EditorPanel.BrushFlow;
            Color color = _node.EditorDoc.ForegroundColor;

            var currentPoint = new Point(px, py);
            var prevPoint = _lastDrawingPixelPoint;

            if (_strokePoints.Count > 0 && _strokePoints[^1] == currentPoint) return;
            _strokePoints.Add(currentPoint);
            _lastDrawingPixelPoint = currentPoint;

            int n = _strokePoints.Count - 1;
            int activeW = activeLayer.Width;
            int activeH = activeLayer.Height;

            if (n == 1)
            {
                DrawBrushLine(_strokeAlphaMaskF, activeW, activeH, _strokePoints[0], _strokePoints[1], radius, hardness, flow, _currentBrushPreset, ref _strokeDistanceAccumulator);
            }
            else if (n == 2)
            {
                Point pExtrapolated = new Point(2 * _strokePoints[0].X - _strokePoints[1].X, 2 * _strokePoints[0].Y - _strokePoints[1].Y);
                DrawBrushSplineSegment(_strokeAlphaMaskF, activeW, activeH, pExtrapolated, _strokePoints[0], _strokePoints[1], _strokePoints[2], radius, hardness, flow, _currentBrushPreset, ref _strokeDistanceAccumulator);
            }
            else if (n >= 3)
            {
                DrawBrushSplineSegment(_strokeAlphaMaskF, activeW, activeH, _strokePoints[n-3], _strokePoints[n-2], _strokePoints[n-1], _strokePoints[n], radius, hardness, flow, _currentBrushPreset, ref _strokeDistanceAccumulator);
            }

            Array.Copy(_strokeAlphaMaskF, _tempAlphaMaskF, _strokeAlphaMaskF.Length);

            double tempDistanceAccumulator = _strokeDistanceAccumulator;
            DrawBrushLine(_tempAlphaMaskF, activeW, activeH, _strokePoints[n-1], _strokePoints[n], radius, hardness, flow, _currentBrushPreset, ref tempDistanceAccumulator);

            double extendedRadius = radius + 2.0;
            if (_currentBrushPreset == BrushPreset.Chalk || _currentBrushPreset == BrushPreset.Spray || _currentBrushPreset == BrushPreset.Scatter ||
                _currentBrushPreset == BrushPreset.Splatter || _currentBrushPreset == BrushPreset.Charcoal || _currentBrushPreset == BrushPreset.OilBrush)
            {
                extendedRadius = radius * 3.5 + 5.0;
            }

            int segmentMinX = Math.Clamp((int)(Math.Min(prevPoint.X, currentPoint.X) - extendedRadius), 0, activeLayer.Width - 1);
            int segmentMaxX = Math.Clamp((int)(Math.Max(prevPoint.X, currentPoint.X) + extendedRadius), 0, activeLayer.Width - 1);
            int segmentMinY = Math.Clamp((int)(Math.Min(prevPoint.Y, currentPoint.Y) - extendedRadius), 0, activeLayer.Height - 1);
            int segmentMaxY = Math.Clamp((int)(Math.Max(prevPoint.Y, currentPoint.Y) + extendedRadius), 0, activeLayer.Height - 1);

            _strokeMinX = Math.Min(_strokeMinX, segmentMinX);
            _strokeMaxX = Math.Max(_strokeMaxX, segmentMaxX);
            _strokeMinY = Math.Min(_strokeMinY, segmentMinY);
            _strokeMaxY = Math.Max(_strokeMaxY, segmentMaxY);

            ApplyStrokeToPixels(_tempDrawingPixels, _oldPixelsForUndo, _tempAlphaMaskF, activeLayer.Width, activeLayer.Height, color, isEraser, _strokeMinX, _strokeMinY, _strokeMaxX, _strokeMaxY);

            int stride = activeLayer.Width * 4;
            int dirtyW = _strokeMaxX - _strokeMinX + 1;
            int dirtyH = _strokeMaxY - _strokeMinY + 1;
            if (dirtyW > 0 && dirtyH > 0)
            {
                activeLayer.Bitmap.WritePixels(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH), _tempDrawingPixels, stride, _strokeMinX, _strokeMinY);
            }
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

                var targetLayer = _movingLayer ?? activeLayer;
                if (targetLayer != null && _moveInitialFullPixels != null)
                {
                    if (_moveInitialGeometry != null)
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
                    }
                }
                return;
            }

            if (_isSelecting)
            {
                string tool = EditorPanel.ActiveToolName;
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

            if (activeLayer == null || _oldPixelsForUndo == null) return;

            if (_strokePoints.Count >= 2 && _strokeAlphaMaskF != null)
            {
                int n = _strokePoints.Count - 1;
                int w = activeLayer.Width;
                int h = activeLayer.Height;
                double radius = EditorPanel.BrushSize;
                double hardness = EditorPanel.BrushHardness;
                double flow = EditorPanel.BrushFlow;
                Color color = _node.EditorDoc.ForegroundColor;
                string tool = EditorPanel.ActiveToolName;
                bool isEraser = (tool == "Eraser");

                if (n > 1)
                {
                    Point pExtrapolated = new Point(2 * _strokePoints[n].X - _strokePoints[n-1].X, 2 * _strokePoints[n].Y - _strokePoints[n-1].Y);
                    DrawBrushSplineSegment(_strokeAlphaMaskF, w, h, _strokePoints[n-2], _strokePoints[n-1], _strokePoints[n], pExtrapolated, radius, hardness, flow, _currentBrushPreset, ref _strokeDistanceAccumulator);
                }

                if (_tempDrawingPixels != null)
                {
                    ApplyStrokeToPixels(_tempDrawingPixels, _oldPixelsForUndo, _strokeAlphaMaskF, w, h, color, isEraser, _strokeMinX, _strokeMinY, _strokeMaxX, _strokeMaxY);

                    int stride = w * 4;
                    int dirtyW = _strokeMaxX - _strokeMinX + 1;
                    int dirtyH = _strokeMaxY - _strokeMinY + 1;
                    if (dirtyW > 0 && dirtyH > 0)
                    {
                        activeLayer.Bitmap.WritePixels(new Int32Rect(_strokeMinX, _strokeMinY, dirtyW, dirtyH), _tempDrawingPixels, stride, _strokeMinX, _strokeMinY);
                    }
                }
            }

            int strideFinal = activeLayer.Width * 4;
            var newPixels = new byte[strideFinal * activeLayer.Height];
            activeLayer.Bitmap.CopyPixels(newPixels, strideFinal, 0);

            var cmd = new PixelEditCommand(activeLayer, _oldPixelsForUndo, newPixels);
            _node.EditorDoc.History.Execute(cmd);

            _tempDrawingPixels = null;
            _oldPixelsForUndo = null;
            _strokeAlphaMaskF = null;
            _tempAlphaMaskF = null;
            _strokePoints.Clear();

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
        }

        private void CommitSelectionMove(EditorLayer originalLayer, byte[] sourceFullPixels, int dx, int dy)
        {
            if (_node.EditorDoc == null || _moveInitialGeometry == null || !_hasCachedSelectionMask || _cachedSelectionMask == null)
                return;

            int w = originalLayer.Width;
            int h = originalLayer.Height;
            int stride = w * 4;

            // 1. Cut the selection from originalLayer (basePixels will have the transparent hole)
            byte[] basePixels = new byte[stride * h];
            byte[] newLayerPixels = new byte[stride * h];
            Array.Copy(sourceFullPixels, basePixels, sourceFullPixels.Length);

            // Separate selection pixels (using clamped loop)
            int startY = Math.Max(0, _cachedSelectionStartY);
            int endY = Math.Min(h - 1, _cachedSelectionEndY);
            int startX = Math.Max(0, _cachedSelectionStartX);
            int endX = Math.Min(w - 1, _cachedSelectionEndX);

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * stride;
                for (int x = startX; x <= endX; x++)
                {
                    if (IsInsideSelection(x, y))
                    {
                        int idx = rowOffset + x * 4;
                        // Put into newLayerPixels at the SHIFTED position!
                        int destX = x + dx;
                        int destY = y + dy;
                        if (destX >= 0 && destX < w && destY >= 0 && destY < h)
                        {
                            int destIdx = (destY * w + destX) * 4;
                            newLayerPixels[destIdx] = sourceFullPixels[idx];
                            newLayerPixels[destIdx + 1] = sourceFullPixels[idx + 1];
                            newLayerPixels[destIdx + 2] = sourceFullPixels[idx + 2];
                            newLayerPixels[destIdx + 3] = sourceFullPixels[idx + 3];
                        }

                        // Clear from original layer
                        basePixels[idx] = 0;
                        basePixels[idx + 1] = 0;
                        basePixels[idx + 2] = 0;
                        basePixels[idx + 3] = 0;
                    }
                }
            }

            // 2. Create the new layer containing newLayerPixels
            string newLayerName = $"{originalLayer.Name} Selection";
            var newLayer = new EditorLayer(w, h, newLayerName);
            newLayer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), newLayerPixels, stride, 0);

            // Create a tight WriteableBitmap of size bw x bh for OriginalTransformBitmap
            int bw = endX - startX + 1;
            int bh = endY - startY + 1;
            if (bw > 0 && bh > 0)
            {
                int tightStride = bw * 4;
                byte[] tightPixels = new byte[tightStride * bh];
                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * stride;
                    int tightRowOffset = (y - startY) * tightStride;
                    for (int x = startX; x <= endX; x++)
                    {
                        if (IsInsideSelection(x, y))
                        {
                            int idx = rowOffset + x * 4;
                            int tightIdx = tightRowOffset + (x - startX) * 4;
                            tightPixels[tightIdx] = sourceFullPixels[idx];
                            tightPixels[tightIdx + 1] = sourceFullPixels[idx + 1];
                            tightPixels[tightIdx + 2] = sourceFullPixels[idx + 2];
                            tightPixels[tightIdx + 3] = sourceFullPixels[idx + 3];
                        }
                    }
                }
                var origBmp = new WriteableBitmap(bw, bh, 96, 96, PixelFormats.Bgra32, null);
                origBmp.WritePixels(new Int32Rect(0, 0, bw, bh), tightPixels, tightStride, 0);
                newLayer.OriginalTransformBitmap = origBmp;
                newLayer.ContentBounds = new Rect(startX + dx, startY + dy, bw, bh);
            }

            if (_activeSelectionGeometry != null)
            {
                var copyGeom = _activeSelectionGeometry.Clone();
                if (dx != 0 || dy != 0)
                {
                    copyGeom.Transform = new TranslateTransform(dx, dy);
                }
                newLayer.ContentGeometry = copyGeom.GetOutlinedPathGeometry();
            }

            newLayer.InvalidateThumbnail();

            // Insert new layer above originalLayer
            int activeIndex = _node.EditorDoc.Layers.IndexOf(originalLayer);
            int insertIndex = activeIndex >= 0 ? activeIndex + 1 : _node.EditorDoc.Layers.Count;

            // 3. Create the CutToNewLayerCommand (which does originalLayer pixel update & newLayer addition)
            var cutCmd = new CutToNewLayerCommand(
                _node.EditorDoc, 
                originalLayer, 
                sourceFullPixels, // old pixels of original layer (contains selection)
                basePixels,       // new pixels of original layer (contains transparent hole)
                newLayer, 
                insertIndex
            );

            // Execute command
            _node.EditorDoc.History.Execute(cutCmd);

            // Clear selection preview
            ClearSelection();

            // Refresh UI
            OnEditorDocumentModified();
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
                double scaleX = MainImage.ActualWidth / activeLayer.Width;
                double scaleY = MainImage.ActualHeight / activeLayer.Height;

                var scaledGeometry = _activeSelectionGeometry.Clone();
                scaledGeometry.Transform = new ScaleTransform(scaleX, scaleY);

                var outlined = scaledGeometry.GetOutlinedPathGeometry();

                // Scale stroke thickness inversely with zoom — ~1px on screen
                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;
                double strokeW = 1.0 / zoom;

                SelectionPolygon.Data = outlined;
                SelectionPolygon.StrokeThickness = strokeW;
                SelectionPolygon.StrokeDashArray = new DoubleCollection { 2.0, 2.0 };
                SelectionPolygon.Visibility = Visibility.Visible;

                if (SelectionPolygonBg != null)
                {
                    SelectionPolygonBg.Data = outlined;
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
            ApplyPreviewStrokeStyle();
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

                // Ctrl+S: Save FX Config
                if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (BtnSaveFxQuick != null)
                    {
                        SaveFxConfig(BtnSaveFxQuick);
                    }
                    else
                    {
                        FlowMy.Utils.FxConfigCache.SaveToFile();
                    }
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
                if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
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
                    EditorPanel.UndoAction();
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

        private void DrawBrushCircle(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow,
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
                case BrushPreset.Airbrush:
                    DrawBrush_Airbrush(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Splatter:
                    DrawBrush_Splatter(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Charcoal:
                    DrawBrush_Charcoal(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.OilBrush:
                    DrawBrush_OilBrush(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                default: // RoundHard
                    DrawBrush_RoundHard(alphaMask, width, height, cx, cy, radius, hardness, flow);
                    break;
            }
        }

        #region ═══ BRUSH PRESETS ═══

        /// <summary>Round Hard — cọ tròn cứng (mặc định gốc).</summary>
        private void DrawBrush_RoundHard(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double innerRadius = r * (hardness / 100.0);
            double range = outerRadius - innerRadius;
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

                    if (dist2 <= outerRadius * outerRadius)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double dist = Math.Sqrt(dist2);
                        double t = (dist - innerRadius) / range;
                        t = Math.Clamp(t, 0.0, 1.0);
                        double pixelOpacity = 1.0 - (t * t * (3.0 - 2.0 * t));

                        float stampOpacity = (float)(pixelOpacity * flowMul);
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        /// <summary>Round Soft — gaussian-like smooth falloff.</summary>
        private void DrawBrush_RoundSoft(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double flowMul = flow / 100.0;
            double sigma = r / 2.5;
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

                    if (dist2 <= outerRadius * outerRadius)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double dist = Math.Sqrt(dist2);
                        double gaussianOpacity = Math.Exp(-dist2 / sigma2x2);
                        double edgeOpacity = Math.Clamp((outerRadius - dist), 0.0, 1.0);
                        double pixelOpacity = gaussianOpacity * edgeOpacity;

                        float stampOpacity = (float)(pixelOpacity * flowMul);
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        /// <summary>Flat — cọ dẹp hình chữ nhật ngang, ratio ~3:1.</summary>
        private void DrawBrush_Flat(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            double halfW = radius;          // chiều rộng = diameter
            double halfH = radius / 3.0;    // chiều cao = 1/3 diameter
            double flowMul = flow / 100.0;
            double hardnessMul = hardness / 100.0;
            double innerEdge = Math.Min(hardnessMul, Math.Max(0.0, 1.0 - 1.0 / Math.Max(1.0, radius)));

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
                    if (edgeDist > innerEdge && innerEdge < 1.0)
                        pixelOpacity = 1.0 - (edgeDist - innerEdge) / (1.0 - innerEdge);

                    float stampOpacity = (float)(pixelOpacity * flowMul);
                    if (stampOpacity <= 0f) continue;

                    int maskOffset = rowOffset + x;
                    alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                }
            }
        }

        private void DrawBrush_Chalk(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 6.0;

            if (r <= 0.5)
            {
                foreach (var offset in ChalkPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in ChalkPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * (offset.size * 0.15);

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Spray — bình xịt, sử dụng phân bổ điểm cố định để đồng bộ hoàn toàn với preview.</summary>
        private void DrawBrush_Spray(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 6.0;

            if (r <= 0.5)
            {
                foreach (var offset in SprayPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                            double opacityScale = 1.0 - distRatio * 0.5;
                            float stampOpacity = (float)(opacityScale * flowMul);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in SprayPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * 0.25;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                double opacityScale = 1.0 - distRatio * 0.5;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * opacityScale * flowMul);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Scatter — điểm rải rác, sử dụng phân bổ cố định để đồng bộ hoàn toàn với preview.</summary>
        private void DrawBrush_Scatter(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 6.0;

            if (r <= 0.5)
            {
                foreach (var offset in ScatterPresetOffsets)
                {
                    double blobCx = cx + offset.x * offsetMul;
                    double blobCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(blobCx + 0.5);
                    int py = (int)Math.Floor(blobCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)flowMul;
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in ScatterPresetOffsets)
            {
                double blobCx = cx + offset.x * offsetMul;
                double blobCy = cy + offset.y * offsetMul;
                double blobRadius = 0.5 + (r - 0.5) * offset.scale;

                int startX = Math.Max(0, (int)Math.Floor(blobCx - blobRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(blobCx + blobRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(blobCy - blobRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(blobCy + blobRadius + 0.5));
                double outerRadius = blobRadius + 0.5;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - blobCy;
                    double dy2 = dy * dy;
                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - blobCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= outerRadius * outerRadius)
                        {
                            if (!IsInsideSelection(x, y)) continue;
                            double dist = Math.Sqrt(dist2);
                            double falloff = 1.0 - (dist / outerRadius);
                            double edgeOpacity = Math.Clamp((outerRadius - dist), 0.0, 1.0);
                            double pixelOpacity = falloff * edgeOpacity;
                            float stampOpacity = (float)Math.Clamp(pixelOpacity * flowMul, 0.0, 1.0);
                            if (stampOpacity <= 0f) continue;
                            
                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Pencil — bút chì, nét nhỏ cứng với slight jitter.</summary>
        private void DrawBrush_Pencil(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            // Pencil: cap radius nhỏ hơn, hardness = 100%
            double pencilRadius = Math.Min(r, Math.Max(1.5, r * 0.5));
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
                        float stampOpacity = (float)flowMul;
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        private void DrawBrush_Airbrush(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r * 1.8 + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double flowMul = flow / 100.0;
            double divisor = Math.Max(0.1, r * 0.8);

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= outerRadius * outerRadius)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double dist = Math.Sqrt(dist2);
                        double exponent = -dist / divisor;
                        double falloff = Math.Exp(exponent);
                        double edgeOpacity = Math.Clamp(outerRadius - dist, 0.0, 1.0);
                        double pixelOpacity = falloff * edgeOpacity;

                        float stampOpacity = (float)(pixelOpacity * flowMul);
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        private void DrawBrush_Splatter(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 6.0;

            if (r <= 0.5)
            {
                foreach (var offset in SplatterPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * offset.opacity);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in SplatterPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.4;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * offset.opacity);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        private void DrawBrush_Charcoal(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 4.0;

            if (r <= 0.5)
            {
                foreach (var offset in CharcoalPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            double noise = Math.Abs(Math.Sin(px * 12.9898 + py * 78.233) * 43758.5453) % 1.0;
                            if (noise < 0.3) continue;
                            float stampOpacity = (float)(flowMul * offset.opacity * 200.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in CharcoalPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.45;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double noise = Math.Abs(Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453) % 1.0;
                            if (noise < 0.35) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * offset.opacity * 200.0 / 255.0);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        private void DrawBrush_OilBrush(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 3.5;

            if (r <= 0.5)
            {
                foreach (var offset in OilBrushPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in OilBrushPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.25;

                double bristleFlow = 0.6 + 0.4 * (Math.Abs(Math.Sin(offset.x * 37.13 + offset.y * 53.45) * 1000.0) % 1.0);

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * bristleFlow);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        #endregion

        private double GetBrushStep(BrushPreset preset, double radius)
        {
            if (preset == BrushPreset.RoundSoft || preset == BrushPreset.Airbrush || preset == BrushPreset.Charcoal || preset == BrushPreset.OilBrush)
            {
                return Math.Max(0.1, radius * 0.15);
            }
            else if (preset == BrushPreset.Spray || preset == BrushPreset.Scatter || preset == BrushPreset.Chalk || preset == BrushPreset.Splatter)
            {
                double factor = (preset == BrushPreset.Splatter) ? 5.0 : 6.0;
                return Math.Max(1.0, radius * factor);
            }
            else if (preset == BrushPreset.Pencil)
            {
                double pencilRadius = Math.Min(radius, Math.Max(1.5, radius * 0.5));
                return Math.Max(0.1, pencilRadius * 0.2);
            }
            else
            {
                return Math.Max(0.1, radius * 0.2);
            }
        }

        private void DrawBrushLine(float[] alphaMask, int width, int height, Point p1, Point p2, double radius, double hardness, double flow,
            BrushPreset preset, ref double distanceAccumulator)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len == 0)
            {
                DrawBrushCircle(alphaMask, width, height, p1.X, p1.Y, radius, hardness, flow, preset);
                return;
            }

            double step = GetBrushStep(preset, radius);

            double d = 0;
            while (d <= len)
            {
                double remainingToStep = step - distanceAccumulator;
                if (d + remainingToStep <= len)
                {
                    d += remainingToStep;
                    double cx = p1.X + (dx * d / len);
                    double cy = p1.Y + (dy * d / len);
                    DrawBrushCircle(alphaMask, width, height, cx, cy, radius, hardness, flow, preset);
                    distanceAccumulator = 0;
                }
                else
                {
                    distanceAccumulator += (len - d);
                    break;
                }
            }
        }

        private Point CatmullRom(Point p0, Point p1, Point p2, Point p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float x = 0.5f * (float)((2 * p1.X) + (-p0.X + p2.X) * t
                + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2
                + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
            float y = 0.5f * (float)((2 * p1.Y) + (-p0.Y + p2.Y) * t
                + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2
                + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
            return new Point(x, y);
        }

        private void DrawBrushSplineSegment(float[] alphaMask, int width, int height, Point p0, Point p1, Point p2, Point p3, double radius, double hardness, double flow, BrushPreset preset, ref double distanceAccumulator)
        {
            double step = GetBrushStep(preset, radius);
            double estLength = Point.Subtract(p2, p1).Length;
            int subdivisions = Math.Max(20, (int)(estLength * 2.0));
            subdivisions = Math.Min(subdivisions, 200);

            Point pPrev = p1;
            for (int i = 1; i <= subdivisions; i++)
            {
                float t = (float)i / subdivisions;
                Point pCurr = CatmullRom(p0, p1, p2, p3, t);
                double dist = Point.Subtract(pCurr, pPrev).Length;

                if (dist == 0) continue;

                double dx = pCurr.X - pPrev.X;
                double dy = pCurr.Y - pPrev.Y;

                double d = 0;
                while (d <= dist)
                {
                    double remainingToStep = step - distanceAccumulator;
                    if (d + remainingToStep <= dist)
                    {
                        d += remainingToStep;
                        double cx = pPrev.X + (dx * d / dist);
                        double cy = pPrev.Y + (dy * d / dist);
                        DrawBrushCircle(alphaMask, width, height, cx, cy, radius, hardness, flow, preset);
                        distanceAccumulator = 0;
                    }
                    else
                    {
                        distanceAccumulator += (dist - d);
                        break;
                    }
                }
                pPrev = pCurr;
            }
        }

        private void ApplyStrokeToPixels(byte[] destPixels, byte[] srcPixels, float[] alphaMask, int width, int height, Color color, bool isEraser, int minX, int minY, int maxX, int maxY)
        {
            minX = Math.Clamp(minX, 0, width - 1);
            maxX = Math.Clamp(maxX, 0, width - 1);
            minY = Math.Clamp(minY, 0, height - 1);
            maxY = Math.Clamp(maxY, 0, height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                int pixelRowOffset = rowOffset * 4;
                for (int x = minX; x <= maxX; x++)
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
                    float maskAlpha = alphaMask[maskOffset];
                    if (maskAlpha == 0f)
                    {
                        int pixelOffset = pixelRowOffset + x * 4;
                        destPixels[pixelOffset] = srcPixels[pixelOffset];
                        destPixels[pixelOffset + 1] = srcPixels[pixelOffset + 1];
                        destPixels[pixelOffset + 2] = srcPixels[pixelOffset + 2];
                        destPixels[pixelOffset + 3] = srcPixels[pixelOffset + 3];
                        continue;
                    }

                    int pOffset = pixelRowOffset + x * 4;

                    if (isEraser)
                    {
                        byte srcA = srcPixels[pOffset + 3];
                        destPixels[pOffset] = srcPixels[pOffset];
                        destPixels[pOffset + 1] = srcPixels[pOffset + 1];
                        destPixels[pOffset + 2] = srcPixels[pOffset + 2];
                        destPixels[pOffset + 3] = (byte)Math.Clamp(srcA * (1.0f - maskAlpha), 0f, 255f);
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
                        double centerX = width / 2;
                        double centerY = height / 2;
                        
                        double previewRadius = radius / 6.0;
                        foreach (var offset in ChalkPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * (offset.size * 0.15);
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 180), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Spray:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in SprayPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * 0.25;
                            double spotDiameter = spotRadius * 2;
                            double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 200 * (1.0 - distRatio * 0.5)), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Scatter:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in ScatterPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.scale;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 160), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
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

                case BrushPreset.Airbrush:
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
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(f * 255 * 0.25), brushColor.R, brushColor.G, brushColor.B), 0.3));
                        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                        ellipse.Fill = gradient;
                        grid.Children.Add(ellipse);
                    }
                    break;

                case BrushPreset.Splatter:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in SplatterPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.4;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * offset.opacity * 255), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.Charcoal:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in CharcoalPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.45;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * offset.opacity * 200), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
                    }
                    break;

                case BrushPreset.OilBrush:
                    {
                        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
                        double centerX = width / 2;
                        double centerY = height / 2;

                        double previewRadius = radius / 6.0;
                        foreach (var offset in OilBrushPresetOffsets)
                        {
                            double spotRadius = 0.5 + (previewRadius - 0.5) * offset.size * 0.25;
                            double spotDiameter = spotRadius * 2;
                            var dot = new Ellipse
                            {
                                Width = spotDiameter,
                                Height = spotDiameter,
                                Fill = new SolidColorBrush(Color.FromArgb((byte)(f * 200), brushColor.R, brushColor.G, brushColor.B))
                            };
                            Canvas.SetLeft(dot, centerX + offset.x * radius - spotRadius);
                            Canvas.SetTop(dot, centerY + offset.y * radius - spotRadius);
                            canvas.Children.Add(dot);
                        }
                        grid.Children.Add(canvas);
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

        private bool IsClickInsidePopupOrComboBox(DependencyObject clickedElement, DependencyObject popupChild)
        {
            if (clickedElement == null || popupChild == null) return false;

            DependencyObject current = clickedElement;
            while (current != null)
            {
                if (current == popupChild || current == PopupBrushPreset)
                    return true;

                if (current is ComboBox || current is ComboBoxItem)
                    return true;

                if (current is System.Windows.Controls.Primitives.Popup p)
                {
                    if (p.PlacementTarget != null && IsClickInsidePopupOrComboBox(p.PlacementTarget, popupChild))
                        return true;
                }

                DependencyObject next = null;
                if (current is Visual)
                {
                    next = VisualTreeHelper.GetParent(current);
                }
                
                if (next == null && current is FrameworkElement fe)
                {
                    next = fe.Parent ?? fe.TemplatedParent;
                }
                
                if (next == null && current is FrameworkContentElement fce)
                {
                    next = fce.Parent ?? fce.TemplatedParent;
                }

                current = next;
            }
            return false;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BrushSettingsPopup == null || !BrushSettingsPopup.IsOpen) return;

            var child = BrushSettingsPopup.Child as FrameworkElement;
            if (child != null)
            {
                var clickedElement = e.OriginalSource as DependencyObject;
                if (IsClickInsidePopupOrComboBox(clickedElement, child))
                {
                    return; // Nhấp vào bên trong Popup hoặc ComboBox Dropdown, không đóng Popup
                }

                var mousePosBtn = e.GetPosition(BtnBrushPreviewDropdown);
                var btnRect = new Rect(0, 0, BtnBrushPreviewDropdown.ActualWidth, BtnBrushPreviewDropdown.ActualHeight);

                if (!btnRect.Contains(mousePosBtn))
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

        private void ShiftBitmapPixels(EditorLayer activeLayer, byte[] sourceFullPixels, int dx, int dy)
        {
            int w = activeLayer.Width;
            int h = activeLayer.Height;
            int stride = w * 4;
            byte[] tempPixels = new byte[stride * h];

            if (_moveInitialGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null)
            {
                // Start by copying sourceFullPixels
                Array.Copy(sourceFullPixels, tempPixels, tempPixels.Length);

                // Clear the source selection area in tempPixels
                for (int y = _cachedSelectionStartY; y <= _cachedSelectionEndY; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = _cachedSelectionStartX; x <= _cachedSelectionEndX; x++)
                    {
                        if (IsInsideSelection(x, y))
                        {
                            int idx = rowOffset + x * 4;
                            tempPixels[idx] = 0;
                            tempPixels[idx + 1] = 0;
                            tempPixels[idx + 2] = 0;
                            tempPixels[idx + 3] = 0;
                        }
                    }
                }

                // Draw the shifted selection area into tempPixels
                for (int y = _cachedSelectionStartY; y <= _cachedSelectionEndY; y++)
                {
                    int destY = y + dy;
                    if (destY < 0 || destY >= h) continue;

                    int rowOffset = y * stride;
                    for (int x = _cachedSelectionStartX; x <= _cachedSelectionEndX; x++)
                    {
                        int destX = x + dx;
                        if (destX < 0 || destX >= w) continue;

                        if (IsInsideSelection(x, y))
                        {
                            int srcIdx = rowOffset + x * 4;
                            int destIdx = (destY * w + destX) * 4;
                            tempPixels[destIdx] = sourceFullPixels[srcIdx];
                            tempPixels[destIdx + 1] = sourceFullPixels[srcIdx + 1];
                            tempPixels[destIdx + 2] = sourceFullPixels[srcIdx + 2];
                            tempPixels[destIdx + 3] = sourceFullPixels[srcIdx + 3];
                        }
                    }
                }
            }
            else
            {
                // Fast row copy for full layer move
                int pixelSize = 4;
                int rowBytes = w * pixelSize;

                for (int y = 0; y < h; y++)
                {
                    int srcY = y - dy;
                    int destRowOffset = y * stride;

                    if (srcY >= 0 && srcY < h)
                    {
                        int srcRowOffset = srcY * stride;
                        if (dx > 0)
                        {
                            Array.Clear(tempPixels, destRowOffset, dx * pixelSize);
                            Buffer.BlockCopy(sourceFullPixels, srcRowOffset, tempPixels, destRowOffset + dx * pixelSize, (w - dx) * pixelSize);
                        }
                        else if (dx < 0)
                        {
                            int copyWidth = w + dx;
                            Buffer.BlockCopy(sourceFullPixels, srcRowOffset - dx * pixelSize, tempPixels, destRowOffset, copyWidth * pixelSize);
                            Array.Clear(tempPixels, destRowOffset + copyWidth * pixelSize, (-dx) * pixelSize);
                        }
                        else
                        {
                            Buffer.BlockCopy(sourceFullPixels, srcRowOffset, tempPixels, destRowOffset, rowBytes);
                        }
                    }
                    else
                    {
                        Array.Clear(tempPixels, destRowOffset, rowBytes);
                    }
                }
            }

            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), tempPixels, stride, 0);
        }

        private void CommitPendingMoveTranslation()
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

                CommitSelectionMove(targetLayer, _moveInitialFullPixels, dx, dy);
                _moveInitialFullPixels = null;
                _moveInitialGeometry = null;
                _movingLayer = null;
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

            // Apply shift once
            ShiftBitmapPixels(targetLayer, _moveInitialFullPixels, dxLayer, dyLayer);

            int moveStride = targetLayer.Width * 4;
            var finalPixels = new byte[moveStride * targetLayer.Height];
            targetLayer.Bitmap.CopyPixels(finalPixels, moveStride, 0);

            var moveCmd = new PixelEditCommand(targetLayer, _moveInitialFullPixels, finalPixels);
            _node.EditorDoc?.History.Execute(moveCmd);

            _moveInitialFullPixels = null;
            _movingLayer = null;
            targetLayer.InvalidateThumbnail();
            OnEditorDocumentModified();
        }

        public bool CanHandleKey(KeyEventArgs e)
        {
            if (_node.ProcessingMode != Models.Nodes.ImageProcessingMode.Manual)
                return false;

            // Do not handle if focused on input fields
            if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox)
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
            if (e.Key == Key.Delete && modifiers == ModifierKeys.None)
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

        #endregion
    }
}
