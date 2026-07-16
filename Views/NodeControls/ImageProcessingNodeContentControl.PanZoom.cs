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
using SkiaSharp;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        // ═══ Canvas constants ═══
        private const double CanvasSize = 10000.0;
        private const double CanvasHalf = CanvasSize / 2.0; // 5000

        private void WireScrollPanZoomMagnifier()
        {
            MainScrollViewer.PreviewMouseWheel += MainScrollViewer_PreviewMouseWheel;
            MainScrollViewer.PreviewMouseLeftButtonDown += MainScrollViewer_PreviewMouseLeftButtonDown;
            MainScrollViewer.PreviewMouseLeftButtonUp += MainScrollViewer_PreviewMouseLeftButtonUp;
            MainScrollViewer.PreviewMouseMove += MainScrollViewer_PreviewMouseMove;
            MainScrollViewer.MouseLeave += MainScrollViewer_MouseLeave;
            MainScrollViewer.PreviewKeyDown += MainScrollViewer_PreviewKeyDown;
        }

        // ═══ ZOOM (Ctrl+Wheel) ═══
        // Zoom vào vị trí chuột bằng cách điều chỉnh ScaleTransform + TranslateTransform
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            e.Handled = true;

            // Lấy vị trí chuột relative to viewport (Border)
            var mousePos = e.GetPosition(MainScrollViewer);
            var oldScale = ImageZoomScale.ScaleX;
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = oldScale * zoomFactor;
            if (newScale < 0.01) newScale = 0.01;
            if (newScale > 100.0) newScale = 100.0;
            if (Math.Abs(newScale - oldScale) < 0.0001) return;

            _hasUserCentered = true;

            double ox = (MainScrollViewer.ActualWidth - 10000) / 2.0;
            double oy = (MainScrollViewer.ActualHeight - 10000) / 2.0;

            // Tính tọa độ chuột trên canvas TRƯỚC khi zoom
            double canvasX = (mousePos.X - ox - CanvasTranslate.X) / oldScale;
            double canvasY = (mousePos.Y - oy - CanvasTranslate.Y) / oldScale;

            // Cập nhật scale
            ImageZoomScale.ScaleX = newScale;
            ImageZoomScale.ScaleY = newScale;

            // Sau khi zoom, giữ cùng canvas point dưới chuột:
            CanvasTranslate.X = mousePos.X - ox - canvasX * newScale;
            CanvasTranslate.Y = mousePos.Y - oy - canvasY * newScale;

            // Bitmap scaling mode
            if (MainImage != null)
            {
                RenderOptions.SetBitmapScalingMode(MainImage, newScale > 1.001 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            }

            // Update crop overlay if visible
            if (CropOverlayCanvas != null && CropOverlayCanvas.Visibility == Visibility.Visible)
            {
                UpdateCropOverlayDisplay();
            }
        }

        // ═══ PAN (drag) ═══
        private void MainScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var parent = dep;
                while (parent != null)
                {
                    if (parent == TextMoveContainer)
                    {
                        return; // Let child elements inside text box overlay handle mouse down natively
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            MainScrollViewer.Focus();

            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                if (_isSpacePressed)
                {
                    _isPanning = true;
                    _hasUserCentered = true;
                    _panStart = e.GetPosition(MainScrollViewer);
                    _panOriginX = CanvasTranslate.X;
                    _panOriginY = CanvasTranslate.Y;
                    MainScrollViewer.Cursor = Cursors.SizeAll;
                    MainScrollViewer.CaptureMouse();
                    e.Handled = true;
                    return;
                }

                // If not space-pressed, left click inside MainImage delegates to manual tools (including Move)
                var clickPos = e.GetPosition(MainImage);
                if (clickPos.X >= 0 && clickPos.X <= MainImage.ActualWidth &&
                    clickPos.Y >= 0 && clickPos.Y <= MainImage.ActualHeight)
                {
                    HandleManualEditorMouseDown(e);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    ImageProcessingNodeControl.AddCropPointFromClick(
                        _node, MainImage, ImageZoomScale, e.GetPosition(MainImage),
                        ImageAreaGrid, null, _onCropClickForIp);
                    return;
                }

                // Luôn cho phép pan trong AI mode (không cần check extent)
                _isPanning = true;
                _hasUserCentered = true;
                _panStart = e.GetPosition(MainScrollViewer);
                _panOriginX = CanvasTranslate.X;
                _panOriginY = CanvasTranslate.Y;
                MainScrollViewer.Cursor = Cursors.SizeAll;
                MainScrollViewer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_isDrawingPixels || _isSelecting || _isMovingLayer)
            {
                HandleManualEditorMouseUp();
                e.Handled = true;
                return;
            }

            if (!_isPanning) return;
            _isPanning = false;
            MainScrollViewer.Cursor = Cursors.Arrow;
            MainScrollViewer.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            UpdateBrushCursorPosition();
            UpdateEyedropperCursorPosition(e);

            if (EditorPanel != null)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Transform" || tool == "CropCanvas") return;
            }

            if (_isDrawingPixels || _isSelecting || _isMovingLayer)
            {
                HandleManualEditorMouseMove(e);
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                var pos = e.GetPosition(MainScrollViewer);
                var dx = pos.X - _panStart.X;
                var dy = pos.Y - _panStart.Y;
                // Di chuyển canvas translate trực tiếp (rất mượt)
                CanvasTranslate.X = _panOriginX + dx;
                CanvasTranslate.Y = _panOriginY + dy;
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && MainImage.Source is BitmapSource)
            {
                MainScrollViewer.Cursor = Cursors.Cross;
                MagOverlayPanel.Visibility = Visibility.Visible;
                UpdateMagnifierUi(e.GetPosition(MainImage));
            }
            else
            {
                if (MagOverlayPanel.Visibility == Visibility.Visible)
                    MagOverlayPanel.Visibility = Visibility.Collapsed;
                if (MainScrollViewer.Cursor == Cursors.Cross)
                     MainScrollViewer.Cursor = Cursors.Arrow;
            }
        }

        /// <summary>
        /// Đặt ảnh vào tâm canvas (tọa độ 5000,5000) và fit viewport.
        /// Gọi sau khi ảnh được load hoặc tab được switch.
        /// </summary>
        internal void CenterImageOnCanvas(double imageWidth, double imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0) return;
            if (Math.Abs(_lastCenterImageWidth - imageWidth) > 0.1 || Math.Abs(_lastCenterImageHeight - imageHeight) > 0.1)
            {
                _hasUserCentered = false;
            }
            _lastCenterImageWidth = imageWidth;
            _lastCenterImageHeight = imageHeight;

            // Đặt ImageAreaGrid vào tâm canvas (center of 10000x10000)
            double canvasLeft = CanvasHalf - imageWidth / 2.0;
            double canvasTop = CanvasHalf - imageHeight / 2.0;
            Canvas.SetLeft(ImageAreaGrid, canvasLeft);
            Canvas.SetTop(ImageAreaGrid, canvasTop);

            // Tính viewport size
            double vpW = MainScrollViewer.ActualWidth;
            double vpH = MainScrollViewer.ActualHeight;
            if (vpW <= 0) vpW = 800;
            if (vpH <= 0) vpH = 600;

            // Tính zoom level sao cho ảnh fit viewport (với padding)
            double padFactor = 0.92; // 8% padding
            double sX = (vpW * padFactor) / imageWidth;
            double sY = (vpH * padFactor) / imageHeight;
            double fitScale = Math.Min(sX, sY);
            if (fitScale < 0.01) fitScale = 0.01;
            if (fitScale > 1.0) fitScale = 1.0;

            ImageZoomScale.ScaleX = fitScale;
            ImageZoomScale.ScaleY = fitScale;

            // Tính translate để tâm ảnh (trên canvas) nằm giữa viewport
            CanvasTranslate.X = CanvasHalf * (1.0 - fitScale);
            CanvasTranslate.Y = CanvasHalf * (1.0 - fitScale);

            // Bitmap scaling mode
            if (MainImage != null)
            {
                RenderOptions.SetBitmapScalingMode(MainImage, fitScale > 1.001 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            }
        }

        private void UpdateBrushCursorPosition()
        {
            if (BrushPreviewCursor == null || MainImage == null || _node.EditorDoc == null) return;

            // Clear any dynamically added brush shape elements first
            if (BrushCursorCanvas != null)
            {
                for (int i = BrushCursorCanvas.Children.Count - 1; i >= 0; i--)
                {
                    if (BrushCursorCanvas.Children[i] != BrushPreviewCursor && BrushCursorCanvas.Children[i] != BrushCrosshairCursor)
                    {
                        BrushCursorCanvas.Children.RemoveAt(i);
                    }
                }
            }

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                string tool = EditorPanel.ActiveToolName;
                if ((tool == "Brush" || tool == "Eraser") && MainImage.Source != null)
                {
                    var imgPos = Mouse.GetPosition(MainImage);
                    bool inside = imgPos.X >= 0 && imgPos.X <= MainImage.ActualWidth &&
                                  imgPos.Y >= 0 && imgPos.Y <= MainImage.ActualHeight;

                    if (inside && !_isSpacePressed && !_isPanning)
                    {
                        if (MainImage.Cursor != Cursors.None)
                        {
                            MainImage.Cursor = Cursors.None;
                        }

                        double scaleX = 1.0; // Avoid double-scaling as parent element is already scaled by ImageZoomScale
                        double radius = (EditorPanel.BrushSize * scaleX) / 2.0;
                        double diameter = radius * 2;

                        var containerPos = Mouse.GetPosition(ImageContainer);

                        // Check if cursor visual size on screen is small (threshold: visual diameter <= 10 pixels)
                        bool showCrosshair = (EditorPanel.BrushSize * ImageZoomScale.ScaleX) <= 10.0;
                        if (BrushCrosshairCursor != null)
                        {
                            if (showCrosshair)
                            {
                                Canvas.SetLeft(BrushCrosshairCursor, containerPos.X);
                                Canvas.SetTop(BrushCrosshairCursor, containerPos.Y);

                                if (CrosshairScale != null)
                                {
                                    double invScale = 1.0 / ImageZoomScale.ScaleX;
                                    CrosshairScale.ScaleX = invScale;
                                    CrosshairScale.ScaleY = invScale;
                                }

                                BrushCrosshairCursor.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
                            }
                        }

                        double hardness = EditorPanel.BrushHardness / 100.0;
                        double flow = EditorPanel.BrushFlow / 100.0;
                        Color brushColor = _node.EditorDoc.ForegroundColor;

                        double imgRadius = EditorPanel.BrushSize / 2.0;

                        if (_currentBrushPreset == BrushPreset.Chalk)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in ChalkPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * (offset.size * 0.15);
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 180), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Spray)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in SprayPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * 0.25;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 200 * (1.0 - distRatio * 0.5)), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Scatter)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in ScatterPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.scale;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Flat)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            var rect = new Rectangle
                            {
                                Width = diameter,
                                Height = Math.Max(2.0, diameter / 3.0),
                                Stroke = Brushes.White,
                                StrokeThickness = 1
                            };
                            rect.Effect = new System.Windows.Media.Effects.DropShadowEffect
                            {
                                BlurRadius = 1,
                                ShadowDepth = 0,
                                Color = Colors.Black,
                                Opacity = 0.8
                            };
                            Canvas.SetLeft(rect, containerPos.X - radius);
                            Canvas.SetTop(rect, containerPos.Y - radius / 3.0);
                            BrushCursorCanvas.Children.Add(rect);
                        }
                        else if (_currentBrushPreset == BrushPreset.RoundSoft)
                        {
                            BrushPreviewCursor.Width = diameter;
                            BrushPreviewCursor.Height = diameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - radius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - radius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255 * 0.5), brushColor.R, brushColor.G, brushColor.B), 0.4));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Pencil)
                        {
                            double pencilRadius = Math.Min(radius, Math.Max(1.5 * scaleX, radius * 0.5));
                            double pencilDiameter = pencilRadius * 2;
                            BrushPreviewCursor.Width = pencilDiameter;
                            BrushPreviewCursor.Height = pencilDiameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - pencilRadius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - pencilRadius);

                            BrushPreviewCursor.Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B));
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Airbrush)
                        {
                            double airRadius = radius * 1.8;
                            double airDiameter = airRadius * 2;
                            BrushPreviewCursor.Width = airDiameter;
                            BrushPreviewCursor.Height = airDiameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - airRadius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - airRadius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255 * 0.25), brushColor.R, brushColor.G, brushColor.B), 0.3));
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        else if (_currentBrushPreset == BrushPreset.Splatter)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 6.0;
                            foreach (var offset in SplatterPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.4;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * offset.opacity * 255), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.Charcoal)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 4.0;
                            foreach (var offset in CharcoalPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.45;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * offset.opacity * 200), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else if (_currentBrushPreset == BrushPreset.OilBrush)
                        {
                            BrushPreviewCursor.Visibility = Visibility.Collapsed;
                            double offsetMul = radius * 3.5;
                            foreach (var offset in OilBrushPresetOffsets)
                            {
                                double imgSpotRadius = 0.5 + (imgRadius - 0.5) * offset.size * 0.25;
                                double spotRadius = imgSpotRadius * scaleX;
                                double spotDiameter = spotRadius * 2;
                                var spotEllipse = new Ellipse
                                {
                                    Width = spotDiameter,
                                    Height = spotDiameter,
                                    Fill = new SolidColorBrush(Color.FromArgb((byte)(flow * 200), brushColor.R, brushColor.G, brushColor.B))
                                };
                                Canvas.SetLeft(spotEllipse, containerPos.X + offset.x * offsetMul - spotRadius);
                                Canvas.SetTop(spotEllipse, containerPos.Y + offset.y * offsetMul - spotRadius);
                                BrushCursorCanvas.Children.Add(spotEllipse);
                            }
                        }
                        else // RoundHard
                        {
                            BrushPreviewCursor.Width = diameter;
                            BrushPreviewCursor.Height = diameter;
                            Canvas.SetLeft(BrushPreviewCursor, containerPos.X - radius);
                            Canvas.SetTop(BrushPreviewCursor, containerPos.Y - radius);

                            var radialBrush = new RadialGradientBrush();
                            radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.0));

                            double stopOffset = radius > 0.001 ? Math.Min(hardness, Math.Max(0.0, (radius - scaleX) / radius)) : hardness;
                            if (stopOffset < 0.99)
                            {
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), stopOffset));
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                            }
                            else
                            {
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(flow * 255), brushColor.R, brushColor.G, brushColor.B), 0.99));
                                radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, brushColor.R, brushColor.G, brushColor.B), 1.0));
                            }

                            BrushPreviewCursor.Fill = radialBrush;
                            BrushPreviewCursor.OpacityMask = null;
                            BrushPreviewCursor.Visibility = Visibility.Visible;
                        }
                        return;
                    }
                }
            }

            if (MainImage.Cursor == Cursors.None)
            {
                MainImage.Cursor = null; // Restore default cursor
            }
            BrushPreviewCursor.Visibility = Visibility.Collapsed;
            if (BrushCrosshairCursor != null)
            {
                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateEyedropperCursorPosition(MouseEventArgs e)
        {
            if (EyedropperPreviewContainer == null || MainImage == null || _node.EditorDoc == null) return;

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual)
            {
                string tool = EditorPanel.ActiveToolName;
                if (tool == "Eyedropper" && MainImage.Source != null)
                {
                    var imgPos = Mouse.GetPosition(MainImage);
                    bool inside = imgPos.X >= 0 && imgPos.X <= MainImage.ActualWidth &&
                                  imgPos.Y >= 0 && imgPos.Y <= MainImage.ActualHeight;

                    if (inside && !_isSpacePressed && !_isPanning)
                    {
                        var activeLayer = _node.EditorDoc.ActiveLayer;
                        if (activeLayer != null)
                        {
                            double docScaleX = _node.EditorDoc.Width / MainImage.ActualWidth;
                            double docScaleY = _node.EditorDoc.Height / MainImage.ActualHeight;
                            int docX = (int)Math.Round(imgPos.X * docScaleX);
                            int docY = (int)Math.Round(imgPos.Y * docScaleY);

                            int localX = docX - activeLayer.OffsetX;
                            int localY = docY - activeLayer.OffsetY;

                            try
                            {
                                var stride = 4;
                                var singlePixel = new byte[4];
                                if (localX >= 0 && localX < activeLayer.Width && localY >= 0 && localY < activeLayer.Height)
                                {
                                    activeLayer.Bitmap.CopyPixels(new Int32Rect(localX, localY, 1, 1), singlePixel, stride, 0);
                                }
                                Color sampledColor = Color.FromArgb(singlePixel[3], singlePixel[2], singlePixel[1], singlePixel[0]);

                                // Set background colors of the split ring preview
                                EyedropperNewColorBorder.Background = new SolidColorBrush(sampledColor);
                                EyedropperOldColorBorder.Background = new SolidColorBrush(_node.EditorDoc.ForegroundColor);

                                // Set Hex Text representation
                                if (sampledColor.A == 0)
                                {
                                    EyedropperHexText.Text = "Transparent";
                                }
                                else
                                {
                                    EyedropperHexText.Text = $"#{sampledColor.R:X2}{sampledColor.G:X2}{sampledColor.B:X2}";
                                }

                                // Move preview container to cursor
                                var containerPos = Mouse.GetPosition(ImageContainer);
                                Canvas.SetLeft(EyedropperPreviewContainer, containerPos.X);
                                Canvas.SetTop(EyedropperPreviewContainer, containerPos.Y);

                                if (EyedropperScaleTransform != null && ImageZoomScale != null)
                                {
                                    double invScale = 1.0 / Math.Max(0.01, ImageZoomScale.ScaleX);
                                    EyedropperScaleTransform.ScaleX = invScale;
                                    EyedropperScaleTransform.ScaleY = invScale;
                                }

                                EyedropperPreviewContainer.Visibility = Visibility.Visible;
                                return;
                            }
                            catch { /* ignore */ }
                        }
                    }
                }
            }

            EyedropperPreviewContainer.Visibility = Visibility.Collapsed;
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            MagOverlayPanel.Visibility = Visibility.Collapsed;
            if (MainScrollViewer.Cursor == Cursors.Cross)
                MainScrollViewer.Cursor = Cursors.Arrow;
            if (BrushPreviewCursor != null)
                BrushPreviewCursor.Visibility = Visibility.Collapsed;
            if (BrushCrosshairCursor != null)
                BrushCrosshairCursor.Visibility = Visibility.Collapsed;
            if (EyedropperPreviewContainer != null)
                EyedropperPreviewContainer.Visibility = Visibility.Collapsed;
        }

        private void MainScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                ImageProcessingNodeControl.CompleteActiveCrop(_node, null))
            {
                e.Handled = true;
            }
        }

        private void UpdateMagnifierUi(Point imgPos)
        {
            if (MainImage.Source is not BitmapSource bmp) return;
            int px = (int)Math.Round(imgPos.X);
            int py = (int)Math.Round(imgPos.Y);
            MagCoordTextBlock.Text = $"{px}, {py}";

            int halfRegion = MagSize / (MagZoom * 2);
            int srcX = Math.Max(0, px - halfRegion);
            int srcY = Math.Max(0, py - halfRegion);
            int srcW = halfRegion * 2;
            int srcH = halfRegion * 2;
            if (srcX + srcW > bmp.PixelWidth) srcW = bmp.PixelWidth - srcX;
            if (srcY + srcH > bmp.PixelHeight) srcH = bmp.PixelHeight - srcY;
            if (srcW <= 0 || srcH <= 0) return;

            try
            {
                MagZoomImage.Source = new CroppedBitmap(bmp, new Int32Rect(srcX, srcY, srcW, srcH));
            }
            catch { /* ignore */ }
        }
    }
}
