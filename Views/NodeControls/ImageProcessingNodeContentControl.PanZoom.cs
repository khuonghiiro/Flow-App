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
        private void WireScrollPanZoomMagnifier()
        {
            MainScrollViewer.PreviewMouseWheel += MainScrollViewer_PreviewMouseWheel;
            MainScrollViewer.PreviewMouseLeftButtonDown += MainScrollViewer_PreviewMouseLeftButtonDown;
            MainScrollViewer.PreviewMouseLeftButtonUp += MainScrollViewer_PreviewMouseLeftButtonUp;
            MainScrollViewer.PreviewMouseMove += MainScrollViewer_PreviewMouseMove;
            MainScrollViewer.MouseLeave += MainScrollViewer_MouseLeave;
            MainScrollViewer.PreviewKeyDown += MainScrollViewer_PreviewKeyDown;
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            e.Handled = true;
            var position = e.GetPosition(MainScrollViewer);
            var oldScale = ImageZoomScale.ScaleX;
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = oldScale * zoomFactor;
            if (newScale < 0.01) newScale = 0.01;
            if (newScale > 100.0) newScale = 100.0;
            if (Math.Abs(newScale - oldScale) < 0.0001) return;

            double extentWidth = MainScrollViewer.ExtentWidth;
            double extentHeight = MainScrollViewer.ExtentHeight;
            double relativeX = 0.5, relativeY = 0.5;
            if (extentWidth > 0 && extentHeight > 0)
            {
                relativeX = (MainScrollViewer.HorizontalOffset + position.X) / extentWidth;
                relativeY = (MainScrollViewer.VerticalOffset + position.Y) / extentHeight;
            }

            ImageZoomScale.ScaleX = newScale;
            ImageZoomScale.ScaleY = newScale;
            if (MainImage != null)
            {
                RenderOptions.SetBitmapScalingMode(MainImage, newScale > 1.001 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            }
            MainScrollViewer.UpdateLayout();

            if (CropOverlayCanvas != null && CropOverlayCanvas.Visibility == Visibility.Visible)
            {
                UpdateCropOverlayDisplay();
            }

            extentWidth = MainScrollViewer.ExtentWidth;
            extentHeight = MainScrollViewer.ExtentHeight;
            if (extentWidth > 0 && extentHeight > 0)
            {
                var targetX = relativeX * extentWidth - position.X;
                var targetY = relativeY * extentHeight - position.Y;
                MainScrollViewer.ScrollToHorizontalOffset(Math.Max(0, Math.Min(targetX, extentWidth)));
                MainScrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(targetY, extentHeight)));
            }
        }

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
                    _panStart = e.GetPosition(MainScrollViewer);
                    _panOriginX = MainScrollViewer.HorizontalOffset;
                    _panOriginY = MainScrollViewer.VerticalOffset;
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

                if (MainScrollViewer.ExtentWidth <= MainScrollViewer.ViewportWidth &&
                    MainScrollViewer.ExtentHeight <= MainScrollViewer.ViewportHeight)
                    return;

                _isPanning = true;
                _panStart = e.GetPosition(MainScrollViewer);
                _panOriginX = MainScrollViewer.HorizontalOffset;
                _panOriginY = MainScrollViewer.VerticalOffset;
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
                MainScrollViewer.ScrollToHorizontalOffset(_panOriginX - dx);
                MainScrollViewer.ScrollToVerticalOffset(_panOriginY - dy);
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
                            double scaleX = activeLayer.Width / MainImage.ActualWidth;
                            double scaleY = activeLayer.Height / MainImage.ActualHeight;
                            int px = Math.Clamp((int)(imgPos.X * scaleX), 0, activeLayer.Width - 1);
                            int py = Math.Clamp((int)(imgPos.Y * scaleY), 0, activeLayer.Height - 1);

                            try
                            {
                                var stride = 4;
                                var singlePixel = new byte[4];
                                activeLayer.Bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), singlePixel, stride, 0);
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
