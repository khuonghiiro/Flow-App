using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        private Rect _activeTransformRect;
        private string _activeTransformHandle = "";
        private Point _transformDragStartMouse;
        private Rect _transformDragStartRect;
        private bool _isDraggingTransform = false;

        // Photoshop-like Transform Session state variables
        private bool _transformSessionActive = false;
        private EditorLayer? _sessionLayer;
        private byte[] _originalTransformPixels;
        private double _sessionScaleX = 1.0;
        private double _sessionScaleY = 1.0;
        private double _sessionAngle = 0.0;
        private double _sessionTranslateX = 0.0;
        private double _sessionTranslateY = 0.0;
        
        private double _startDragScaleX = 1.0;
        private double _startDragScaleY = 1.0;
        private double _startDragAngle = 0.0;
        private double _startDragTranslateX = 0.0;
        private double _startDragTranslateY = 0.0;

        private Point _transformCenter;
        private double _sessionCanvasWidth = 0.0;
        private double _sessionCanvasHeight = 0.0;

        #region TOP OPTION BUTTONS CLICKS



        private void OptMoveRotate90Left_Click(object sender, RoutedEventArgs e)
        {
            RotateActiveLayerImmediate(-90);
        }

        private void OptMoveRotate90Right_Click(object sender, RoutedEventArgs e)
        {
            RotateActiveLayerImmediate(90);
        }

        private void OptMoveRotate45_Click(object sender, RoutedEventArgs e)
        {
            RotateActiveLayerImmediate(45);
        }

        private void OptMoveRotate180_Click(object sender, RoutedEventArgs e)
        {
            RotateActiveLayerImmediate(180);
        }

        private void OptMoveFlipH_Click(object sender, RoutedEventArgs e)
        {
            FlipActiveLayerImmediate(horizontal: true);
        }

        private void OptMoveFlipV_Click(object sender, RoutedEventArgs e)
        {
            FlipActiveLayerImmediate(horizontal: false);
        }

        private void OptMoveApply_Click(object sender, RoutedEventArgs e)
        {
            CommitTransformSession();
        }

        private void OptMoveCancel_Click(object sender, RoutedEventArgs e)
        {
            CancelTransformSession();
        }

        #endregion

        #region SESSION LIFECYCLE MANAGEMENT

        private void CommitTransformSession()
        {
            if (!_transformSessionActive || _node.EditorDoc == null) return;
            var activeLayer = _sessionLayer;
            if (activeLayer == null) return;

            // Cache the final transform parameters before ResetVisualTransforms wipes them!
            double finalScaleX = _sessionScaleX;
            double finalScaleY = _sessionScaleY;
            double finalAngle = _sessionAngle;
            double finalTranslateX = _sessionTranslateX;
            double finalTranslateY = _sessionTranslateY;

            // Compute final pixel transformation from original pixels
            double width = _sessionCanvasWidth;
            double height = _sessionCanvasHeight;

            double pixelScaleX = activeLayer.Width / width;
            double pixelScaleY = activeLayer.Height / height;

            double localCenterX = _transformCenter.X * pixelScaleX;
            double localCenterY = _transformCenter.Y * pixelScaleY;
            double localTranslateX = finalTranslateX * pixelScaleX;
            double localTranslateY = finalTranslateY * pixelScaleY;

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(-localCenterX, -localCenterY));
            transformGroup.Children.Add(new ScaleTransform(finalScaleX, finalScaleY));
            transformGroup.Children.Add(new RotateTransform(finalAngle));
            transformGroup.Children.Add(new TranslateTransform(localCenterX + localTranslateX, localCenterY + localTranslateY));

            // Restore original pixels before drawing final transform
            int stride = activeLayer.Width * 4;
            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), _originalTransformPixels, stride, 0);

            // Temporarily reset canvas transforms so ApplyLayerTransform draws correctly
            ResetVisualTransforms();

            // Capture old state for Undo/Redo tracking
            double oldScaleX = activeLayer.LayerScaleX;
            double oldScaleY = activeLayer.LayerScaleY;
            double oldAngle = activeLayer.LayerAngle;
            double oldTranslateX = activeLayer.LayerTranslateX;
            double oldTranslateY = activeLayer.LayerTranslateY;
            var oldOrig = activeLayer.OriginalTransformBitmap;

            // Save the new accumulated scale/translate/rotate values on the layer
            activeLayer.LayerScaleX = finalScaleX;
            activeLayer.LayerScaleY = finalScaleY;
            activeLayer.LayerAngle = finalAngle;
            activeLayer.LayerTranslateX = finalTranslateX;
            activeLayer.LayerTranslateY = finalTranslateY;

            // Clean up session state variables BEFORE executing transform to prevent inconsistent overlay updates
            _transformSessionActive = false;
            _sessionLayer = null;
            _originalTransformPixels = null;

            activeLayer.IsVisible = true;
            activeLayer.IsTempHidden = false;
            TransformPreviewImage.Visibility = Visibility.Collapsed;
            if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;

            // Perform transformation on pixels and write to history
            ApplyLayerTransform(activeLayer, transformGroup, oldScaleX, oldScaleY, oldAngle, oldTranslateX, oldTranslateY, oldOrig, true);

            OnEditorDocumentModified();
            UpdateTransformOverlayDisplay();
        }

        private void CancelTransformSession()
        {
            if (!_transformSessionActive || _node.EditorDoc == null) return;
            var activeLayer = _sessionLayer;
            if (activeLayer == null) return;

            // Revert to original pixels
            int stride = activeLayer.Width * 4;
            activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, activeLayer.Width, activeLayer.Height), _originalTransformPixels, stride, 0);

            ResetVisualTransforms();

            _transformSessionActive = false;
            _sessionLayer = null;
            _originalTransformPixels = null;

            activeLayer.IsVisible = true;
            activeLayer.IsTempHidden = false;
            TransformPreviewImage.Visibility = Visibility.Collapsed;
            if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;

            OnEditorDocumentModified();
            UpdateTransformOverlayDisplay();
        }

        private void ResetVisualTransforms()
        {
            _sessionScaleX = 1.0;
            _sessionScaleY = 1.0;
            _sessionAngle = 0.0;
            _sessionTranslateX = 0.0;
            _sessionTranslateY = 0.0;

            TransformPreviewScale.ScaleX = 1.0;
            TransformPreviewScale.ScaleY = 1.0;
            TransformPreviewRotate.Angle = 0;
            TransformPreviewTranslate.X = 0;
            TransformPreviewTranslate.Y = 0;

            TransformBoxScale.ScaleX = 1.0;
            TransformBoxScale.ScaleY = 1.0;
            TransformBoxRotate.Angle = 0;
            TransformBoxTranslate.X = 0;
            TransformBoxTranslate.Y = 0;
        }

        #endregion

        #region PIXEL TRANSFORM UTILITIES

        private void RotateActiveLayerImmediate(double angle)
        {
            var activeLayer = _node.EditorDoc?.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked) return;

            if (_transformSessionActive)
            {
                _sessionAngle += angle;
                UpdateVisualTransforms();
            }
            else
            {
                var transform = new RotateTransform(angle, activeLayer.Width / 2.0, activeLayer.Height / 2.0);
                ApplyLayerTransform(activeLayer, transform);
            }
        }

        private void FlipActiveLayerImmediate(bool horizontal)
        {
            var activeLayer = _node.EditorDoc?.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked) return;

            if (_transformSessionActive)
            {
                if (horizontal) _sessionScaleX = -_sessionScaleX;
                else _sessionScaleY = -_sessionScaleY;
                UpdateVisualTransforms();
            }
            else
            {
                var transform = new ScaleTransform(horizontal ? -1 : 1, horizontal ? 1 : -1, activeLayer.Width / 2.0, activeLayer.Height / 2.0);
                ApplyLayerTransform(activeLayer, transform);
            }
        }

        private void ApplyLayerTransform(EditorLayer layer, Transform transform,
                                         double oldScaleX = 1.0, double oldScaleY = 1.0, double oldAngle = 0.0,
                                         double oldTranslateX = 0.0, double oldTranslateY = 0.0, WriteableBitmap? oldOrig = null,
                                         bool keepOriginalTransform = false)
        {
            if (_node.EditorDoc == null) return;

            int stride = layer.Width * 4;
            byte[] oldPixels = new byte[stride * layer.Height];
            layer.Bitmap.CopyPixels(oldPixels, stride, 0);

            var drawingVisual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, layer.Width, layer.Height));
                drawingContext.PushTransform(transform);
                var sourceBitmap = (keepOriginalTransform ? layer.OriginalTransformBitmap : null) ?? layer.Bitmap;
                var destRect = (keepOriginalTransform && layer.OriginalTransformBitmap != null) ? layer.ContentBounds : new Rect(0, 0, layer.Width, layer.Height);
                if (destRect.IsEmpty || destRect.Width <= 0 || destRect.Height <= 0)
                {
                    destRect = new Rect(0, 0, layer.Width, layer.Height);
                }
                drawingContext.DrawImage(sourceBitmap, destRect);
                drawingContext.Pop();
            }

            var rtb = new RenderTargetBitmap(layer.Width, layer.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            var converted = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            byte[] newPixels = new byte[stride * layer.Height];
            converted.CopyPixels(newPixels, stride, 0);

            PixelEditCommand cmd;
            if (keepOriginalTransform)
            {
                cmd = new PixelEditCommand(layer, oldPixels, newPixels, oldScaleX, oldScaleY, oldAngle, oldTranslateX, oldTranslateY, oldOrig);
                cmd.KeepOriginalTransformBitmap = true;
                cmd.CaptureNewTransformState();
            }
            else
            {
                cmd = new PixelEditCommand(layer, oldPixels, newPixels);
                cmd.KeepOriginalTransformBitmap = false;
            }

            _node.EditorDoc.History.Execute(cmd);

            layer.InvalidateThumbnail();
            OnEditorDocumentModified();
            UpdateTransformOverlayDisplay();
        }

        #endregion

        #region INTERACTIVE SCALING & ROTATING

        private double Distance(Point a, Point b)
        {
            return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        private double GetDistanceToRect(Rect rect, Point p)
        {
            double dx = Math.Max(0, Math.Max(rect.Left - p.X, p.X - rect.Right));
            double dy = Math.Max(0, Math.Max(rect.Top - p.Y, p.Y - rect.Bottom));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private Rect GetLayerContentBounds(WriteableBitmap? bitmap)
        {
            if (bitmap == null) return new Rect(0, 0, 100, 100);
            try
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;
                if (w <= 0 || h <= 0) return new Rect(0, 0, 100, 100);

                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                bitmap.CopyPixels(pixels, stride, 0);

                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool found = false;

                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        if (alpha > 5) // Ignore transparent edges
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    return new Rect(0, 0, w, h);
                }

                minX = Math.Max(0, minX - 4);
                minY = Math.Max(0, minY - 4);
                maxX = Math.Min(w - 1, maxX + 4);
                maxY = Math.Min(h - 1, maxY + 4);

                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetLayerContentBounds error: " + ex);
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        private Point GetTransformedPoint(Point p, Point center, double scaleX, double scaleY, double angle, double translateX, double translateY)
        {
            // 1. Scale relative to center
            double sx = center.X + (p.X - center.X) * scaleX;
            double sy = center.Y + (p.Y - center.Y) * scaleY;

            // 2. Rotate relative to center
            double rad = angle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double dx = sx - center.X;
            double dy = sy - center.Y;

            double rx = center.X + dx * cos - dy * sin;
            double ry = center.Y + dx * sin + dy * cos;

            // 3. Translate
            return new Point(rx + translateX, ry + translateY);
        }

        private Point GetUntransformedPoint(Point p, Point center, double scaleX, double scaleY, double angle, double translateX, double translateY)
        {
            // 1. Translate back
            double tx = p.X - translateX;
            double ty = p.Y - translateY;

            // 2. Rotate back (negative angle)
            double rad = -angle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double dx = tx - center.X;
            double dy = ty - center.Y;

            double rx = center.X + dx * cos - dy * sin;
            double ry = center.Y + dx * sin + dy * cos;

            // 3. Scale back (clamp scale to avoid divide by zero)
            double sx = Math.Abs(scaleX) > 0.001 ? center.X + (rx - center.X) / scaleX : rx;
            double sy = Math.Abs(scaleY) > 0.001 ? center.Y + (ry - center.Y) / scaleY : ry;

            return new Point(sx, sy);
        }

        public void UpdateTransformOverlayDisplay()
        {
            if (TransformOverlayCanvas == null || MainImage == null || _node.EditorDoc == null) return;

            var activeLayer = _node.EditorDoc.ActiveLayer;
            bool isTransformTool = EditorPanel.ActiveToolName == "Transform";

            if (activeLayer == null || !isTransformTool || !activeLayer.IsVisible)
            {
                TransformOverlayCanvas.Visibility = Visibility.Collapsed;
                TransformPreviewImage.Visibility = Visibility.Collapsed;
                if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            double width = _transformSessionActive ? _sessionCanvasWidth : MainImage.ActualWidth;
            double height = _transformSessionActive ? _sessionCanvasHeight : MainImage.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double scaleX = width / activeLayer.Width;
            double scaleY = height / activeLayer.Height;

            // Base layout calculations for handles are only initialized if the session is not actively dragging.
            // When dragging, WPF's GPU RenderTransform scale/rotate properties handle visually updating the elements.
            if (!_transformSessionActive)
            {
                if (activeLayer.OriginalTransformBitmap != null)
                {
                    // Align bounding box with original unscaled bounds, and sync session variables using cached bounds
                    _activeTransformRect = activeLayer.ContentBounds;
                    if (_activeTransformRect.IsEmpty || _activeTransformRect.Width <= 0 || _activeTransformRect.Height <= 0)
                    {
                        _activeTransformRect = new Rect(0, 0, activeLayer.Width, activeLayer.Height);
                    }
                    _sessionScaleX = activeLayer.LayerScaleX;
                    _sessionScaleY = activeLayer.LayerScaleY;
                    _sessionAngle = activeLayer.LayerAngle;
                    _sessionTranslateX = activeLayer.LayerTranslateX;
                    _sessionTranslateY = activeLayer.LayerTranslateY;
                }
                else
                {
                    // Align bounding box with current visual transformed pixels
                    _activeTransformRect = GetLayerContentBounds(activeLayer.Bitmap);
                    _sessionScaleX = 1.0;
                    _sessionScaleY = 1.0;
                    _sessionAngle = 0.0;
                    _sessionTranslateX = 0.0;
                    _sessionTranslateY = 0.0;
                }

                // Base untransformed rectangle in display space
                Rect displayRect = new Rect(
                    _activeTransformRect.X * scaleX,
                    _activeTransformRect.Y * scaleY,
                    _activeTransformRect.Width * scaleX,
                    _activeTransformRect.Height * scaleY
                );

                // Compute current center
                _transformCenter = new Point(displayRect.Left + displayRect.Width / 2.0, displayRect.Top + displayRect.Height / 2.0);

                // Sync transform origins based on current center
                double normX = _transformCenter.X / width;
                double normY = _transformCenter.Y / height;
                TransformPreviewImage.RenderTransformOrigin = new Point(normX, normY);
                TransformBoxVisual.RenderTransformOrigin = new Point(normX, normY);

                // Base coordinates
                Point pTL = new Point(displayRect.Left, displayRect.Top);
                Point pT  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Top);
                Point pTR = new Point(displayRect.Right, displayRect.Top);
                Point pR  = new Point(displayRect.Right, displayRect.Top + displayRect.Height / 2);
                Point pBR = new Point(displayRect.Right, displayRect.Bottom);
                Point pB  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Bottom);
                Point pBL = new Point(displayRect.Left, displayRect.Bottom);
                Point pL  = new Point(displayRect.Left, displayRect.Top + displayRect.Height / 2);

                // Draw bounding border
                var pathGeometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = pTL, IsClosed = true };
                figure.Segments.Add(new LineSegment(pTR, true));
                figure.Segments.Add(new LineSegment(pBR, true));
                figure.Segments.Add(new LineSegment(pBL, true));
                pathGeometry.Figures.Add(figure);
                TransformBoxBorderPath.Data = pathGeometry;

                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;

                double handleSize = 12.0 / zoom;
                double half = handleSize / 2.0;
                double strokeThickness = 1.5 / zoom;

                foreach (var child in TransformHandlesCanvas.Children)
                {
                    if (child is System.Windows.Shapes.Rectangle r)
                    {
                        r.Width = handleSize;
                        r.Height = handleSize;
                        r.StrokeThickness = strokeThickness;
                    }
                }

                // Position handles
                if (TransHandle_TL != null) { Canvas.SetLeft(TransHandle_TL, pTL.X - half); Canvas.SetTop(TransHandle_TL, pTL.Y - half); }
                if (TransHandle_T != null)  { Canvas.SetLeft(TransHandle_T, pT.X - half); Canvas.SetTop(TransHandle_T, pT.Y - half); }
                if (TransHandle_TR != null) { Canvas.SetLeft(TransHandle_TR, pTR.X - half); Canvas.SetTop(TransHandle_TR, pTR.Y - half); }
                if (TransHandle_R != null)  { Canvas.SetLeft(TransHandle_R, pR.X - half); Canvas.SetTop(TransHandle_R, pR.Y - half); }
                if (TransHandle_BR != null) { Canvas.SetLeft(TransHandle_BR, pBR.X - half); Canvas.SetTop(TransHandle_BR, pBR.Y - half); }
                if (TransHandle_B != null)  { Canvas.SetLeft(TransHandle_B, pB.X - half); Canvas.SetTop(TransHandle_B, pB.Y - half); }
                if (TransHandle_BL != null) { Canvas.SetLeft(TransHandle_BL, pBL.X - half); Canvas.SetTop(TransHandle_BL, pBL.Y - half); }
                if (TransHandle_L != null)  { Canvas.SetLeft(TransHandle_L, pL.X - half); Canvas.SetTop(TransHandle_L, pL.Y - half); }
            }

            TransformBoxVisual.Width = width;
            TransformBoxVisual.Height = height;

            TransformOverlayCanvas.Visibility = Visibility.Visible;
            if (_transformSessionActive)
            {
                TransformPreviewImage.Visibility = Visibility.Visible;
            }
            else
            {
                TransformPreviewImage.Visibility = Visibility.Collapsed;
            }

            // Always apply the visual transforms to align the box and handles!
            UpdateVisualTransforms();
        }

        private void TransformOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null || activeLayer.IsLocked) return;

            // Stable mouse position relative to untransformed parent container (no visual tree latency/caching)
            var clickPos = e.GetPosition(TransformOverlayCanvas);
            double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
            if (zoom <= 0) zoom = 1.0;

            double threshold = 16.0 / zoom;

            double width = _transformSessionActive ? _sessionCanvasWidth : MainImage.ActualWidth;
            double height = _transformSessionActive ? _sessionCanvasHeight : MainImage.ActualHeight;
            double scaleX = width / activeLayer.Width;
            double scaleY = height / activeLayer.Height;

            Rect displayRect = new Rect(
                _activeTransformRect.X * scaleX,
                _activeTransformRect.Y * scaleY,
                _activeTransformRect.Width * scaleX,
                _activeTransformRect.Height * scaleY
            );

            // Center of rect (untransformed display space)
            _transformCenter = new Point(displayRect.Left + displayRect.Width / 2.0, displayRect.Top + displayRect.Height / 2.0);

            // Compute current screen handle locations mathematically (fast, zero layout latency)
            Point ptTL = new Point(displayRect.Left, displayRect.Top);
            Point ptT  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Top);
            Point ptTR = new Point(displayRect.Right, displayRect.Top);
            Point ptR  = new Point(displayRect.Right, displayRect.Top + displayRect.Height / 2);
            Point ptBR = new Point(displayRect.Right, displayRect.Bottom);
            Point ptB  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Bottom);
            Point ptBL = new Point(displayRect.Left, displayRect.Bottom);
            Point ptL  = new Point(displayRect.Left, displayRect.Top + displayRect.Height / 2);

            Point pTL = GetTransformedPoint(ptTL, _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pT  = GetTransformedPoint(ptT,  _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pTR = GetTransformedPoint(ptTR, _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pR  = GetTransformedPoint(ptR,  _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pBR = GetTransformedPoint(ptBR, _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pB  = GetTransformedPoint(ptB,  _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pBL = GetTransformedPoint(ptBL, _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pL  = GetTransformedPoint(ptL,  _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);

            Point[] currentScreenHandles = { pTL, pT, pTR, pR, pBR, pB, pBL, pL };
            string[] handleNames = { "TL", "T", "TR", "R", "BR", "B", "BL", "L" };

            _activeTransformHandle = "";
            double minDistance = double.MaxValue;
            int bestHandleIdx = -1;

            // 1. Check direct handle clicks in screen space
            for (int i = 0; i < 8; i++)
            {
                double dist = Distance(clickPos, currentScreenHandles[i]);
                if (dist < threshold && dist < minDistance)
                {
                    minDistance = dist;
                    bestHandleIdx = i;
                }
            }

            if (bestHandleIdx != -1)
            {
                _activeTransformHandle = handleNames[bestHandleIdx];
            }
            else
            {
                // 2. Check distance to rotated bounding box border and corners for Rotate vs Move (Photoshop style: rotate from corners only)
                Point localClick = GetUntransformedPoint(clickPos, _transformCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
                double distToBorder = GetDistanceToRect(displayRect, localClick) * zoom;

                double dTL = Distance(clickPos, pTL);
                double dTR = Distance(clickPos, pTR);
                double dBR = Distance(clickPos, pBR);
                double dBL = Distance(clickPos, pBL);
                double minCornerDist = Math.Min(Math.Min(dTL, dTR), Math.Min(dBR, dBL));

                double resizeThreshold = 16.0 / zoom;
                double rotateMaxThreshold = 48.0 / zoom;

                if (distToBorder > 0 && minCornerDist > resizeThreshold && minCornerDist <= rotateMaxThreshold)
                {
                    _activeTransformHandle = "Rotate";
                }
                else if (distToBorder == 0) // Inside the box
                {
                    _activeTransformHandle = "Move";
                }
            }

            if (_activeTransformHandle != "")
            {
                _isDraggingTransform = true;
                _transformDragStartMouse = clickPos;
                _transformDragStartRect = displayRect;

                if (!_transformSessionActive)
                {
                    _transformSessionActive = true;
                    _sessionLayer = activeLayer;
                    _sessionCanvasWidth = width;
                    _sessionCanvasHeight = height;

                    int stride = activeLayer.Width * 4;
                    _originalTransformPixels = new byte[stride * activeLayer.Height];
                    activeLayer.Bitmap.CopyPixels(_originalTransformPixels, stride, 0);

                    // Setup Smart Transform to prevent resolution loss
                    if (activeLayer.OriginalTransformBitmap == null)
                    {
                        activeLayer.OriginalTransformBitmap = activeLayer.Bitmap.Clone();
                        activeLayer.LayerScaleX = 1.0;
                        activeLayer.LayerScaleY = 1.0;
                        activeLayer.LayerAngle = 0.0;
                        activeLayer.LayerTranslateX = 0.0;
                        activeLayer.LayerTranslateY = 0.0;
                    }

                    _sessionScaleX = activeLayer.LayerScaleX;
                    _sessionScaleY = activeLayer.LayerScaleY;
                    _sessionAngle = activeLayer.LayerAngle;
                    _sessionTranslateX = activeLayer.LayerTranslateX;
                    _sessionTranslateY = activeLayer.LayerTranslateY;

                    // Recalculate _activeTransformRect using the cached OriginalTransformBitmap bounds for session calculations!
                    _activeTransformRect = activeLayer.ContentBounds;
                    if (_activeTransformRect.IsEmpty || _activeTransformRect.Width <= 0 || _activeTransformRect.Height <= 0)
                    {
                        _activeTransformRect = new Rect(0, 0, activeLayer.Width, activeLayer.Height);
                    }

                    // Recompute displayRect and _transformCenter based on the untransformed original bounds
                    scaleX = width / activeLayer.Width;
                    scaleY = height / activeLayer.Height;
                    displayRect = new Rect(
                        _activeTransformRect.X * scaleX,
                        _activeTransformRect.Y * scaleY,
                        _activeTransformRect.Width * scaleX,
                        _activeTransformRect.Height * scaleY
                    );
                    _transformCenter = new Point(displayRect.Left + displayRect.Width / 2.0, displayRect.Top + displayRect.Height / 2.0);
                    _transformDragStartRect = displayRect; // Update start rect to the original bounds!

                    double normX = _transformCenter.X / width;
                    double normY = _transformCenter.Y / height;
                    
                    // Sync transform origins and sizes
                    TransformBoxVisual.Width = width;
                    TransformBoxVisual.Height = height;

                    TransformPreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    TransformBoxVisual.RenderTransformOrigin = new Point(normX, normY);

                    TransformPreviewImage.Source = activeLayer.OriginalTransformBitmap;
                    TransformPreviewImage.Width = displayRect.Width;
                    TransformPreviewImage.Height = displayRect.Height;
                    TransformPreviewImage.Margin = new Thickness(displayRect.X, displayRect.Y, 0, 0);

                    activeLayer.IsTempHidden = true;
                    OnEditorDocumentModified();

                    TransformPreviewImage.Visibility = Visibility.Visible;
                    UpdateVisualTransforms(); // Instantly apply accumulated scales and translation!
                }

                // Cache start coordinates for cumulative additions
                _startDragScaleX = _sessionScaleX;
                _startDragScaleY = _sessionScaleY;
                _startDragAngle = _sessionAngle;
                _startDragTranslateX = _sessionTranslateX;
                _startDragTranslateY = _sessionTranslateY;

                // Assign cursor shape immediately based on handle drag type
                if (_activeTransformHandle == "Rotate") TransformOverlayCanvas.Cursor = Cursors.None;
                else if (_activeTransformHandle == "Move") TransformOverlayCanvas.Cursor = Cursors.SizeAll;
                else if (_activeTransformHandle == "L" || _activeTransformHandle == "R") TransformOverlayCanvas.Cursor = Cursors.SizeWE;
                else if (_activeTransformHandle == "T" || _activeTransformHandle == "B") TransformOverlayCanvas.Cursor = Cursors.SizeNS;
                else if (_activeTransformHandle == "TL" || _activeTransformHandle == "BR") TransformOverlayCanvas.Cursor = Cursors.SizeNWSE;
                else if (_activeTransformHandle == "TR" || _activeTransformHandle == "BL") TransformOverlayCanvas.Cursor = Cursors.SizeNESW;

                TransformOverlayCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void TransformOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            var currentMouse = e.GetPosition(TransformOverlayCanvas);

            double width = _transformSessionActive ? _sessionCanvasWidth : MainImage.ActualWidth;
            double height = _transformSessionActive ? _sessionCanvasHeight : MainImage.ActualHeight;
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            double scaleX = width / activeLayer.Width;
            double scaleY = height / activeLayer.Height;

            Rect displayRect = new Rect(
                _activeTransformRect.X * scaleX,
                _activeTransformRect.Y * scaleY,
                _activeTransformRect.Width * scaleX,
                _activeTransformRect.Height * scaleY
            );

            Point untransformedCenter = new Point(displayRect.Left + displayRect.Width / 2.0, displayRect.Top + displayRect.Height / 2.0);

            // Compute current screen handle locations mathematically (fast, zero layout latency)
            Point ptTL = new Point(displayRect.Left, displayRect.Top);
            Point ptT  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Top);
            Point ptTR = new Point(displayRect.Right, displayRect.Top);
            Point ptR  = new Point(displayRect.Right, displayRect.Top + displayRect.Height / 2);
            Point ptBR = new Point(displayRect.Right, displayRect.Bottom);
            Point ptB  = new Point(displayRect.Left + displayRect.Width / 2, displayRect.Bottom);
            Point ptBL = new Point(displayRect.Left, displayRect.Bottom);
            Point ptL  = new Point(displayRect.Left, displayRect.Top + displayRect.Height / 2);

            Point pTL = GetTransformedPoint(ptTL, untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pT  = GetTransformedPoint(ptT,  untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pTR = GetTransformedPoint(ptTR, untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pR  = GetTransformedPoint(ptR,  untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pBR = GetTransformedPoint(ptBR, untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pB  = GetTransformedPoint(ptB,  untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pBL = GetTransformedPoint(ptBL, untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
            Point pL  = GetTransformedPoint(ptL,  untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);

            if (!_isDraggingTransform)
            {
                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;
                double threshold = 16.0 / zoom;

                Point[] currentScreenHandles = { pTL, pT, pTR, pR, pBR, pB, pBL, pL };
                string[] handleNames = { "TL", "T", "TR", "R", "BR", "B", "BL", "L" };

                bool nearHandle = false;
                string nearHandleName = "";
                for (int i = 0; i < 8; i++)
                {
                    if (Distance(currentMouse, currentScreenHandles[i]) < threshold)
                    {
                        nearHandle = true;
                        nearHandleName = handleNames[i];
                        break;
                    }
                }

                if (nearHandle)
                {
                    // Show standard resize cursor shapes on hover
                    if (nearHandleName == "L" || nearHandleName == "R") TransformOverlayCanvas.Cursor = Cursors.SizeWE;
                    else if (nearHandleName == "T" || nearHandleName == "B") TransformOverlayCanvas.Cursor = Cursors.SizeNS;
                    else if (nearHandleName == "TL" || nearHandleName == "BR") TransformOverlayCanvas.Cursor = Cursors.SizeNWSE;
                    else if (nearHandleName == "TR" || nearHandleName == "BL") TransformOverlayCanvas.Cursor = Cursors.SizeNESW;

                    if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Point localMouse = GetUntransformedPoint(currentMouse, untransformedCenter, _sessionScaleX, _sessionScaleY, _sessionAngle, _sessionTranslateX, _sessionTranslateY);
                    double distToBorder = GetDistanceToRect(displayRect, localMouse) * zoom;

                    double dTL = Distance(currentMouse, pTL);
                    double dTR = Distance(currentMouse, pTR);
                    double dBR = Distance(currentMouse, pBR);
                    double dBL = Distance(currentMouse, pBL);
                    double minCornerDist = Math.Min(Math.Min(dTL, dTR), Math.Min(dBR, dBL));

                    double resizeThreshold = 16.0 / zoom;
                    double rotateMaxThreshold = 48.0 / zoom;

                    if (distToBorder > 0 && minCornerDist > resizeThreshold && minCornerDist <= rotateMaxThreshold)
                    {
                        TransformOverlayCanvas.Cursor = Cursors.None;
                        if (RotateCursorCanvas != null)
                        {
                            RotateCursorCanvas.Visibility = Visibility.Visible;
                            double cursorSize = 14.0 / zoom;
                            Canvas.SetLeft(RotateCursorCanvas, currentMouse.X - cursorSize / 2.0);
                            Canvas.SetTop(RotateCursorCanvas, currentMouse.Y - cursorSize / 2.0);

                            // Dynamically scale rotate cursor to be readable/large with zoom
                            RotateCursorCanvas.Width = cursorSize;
                            RotateCursorCanvas.Height = cursorSize;
                            if (RotateCursorIcon != null)
                            {
                                RotateCursorIcon.Width = cursorSize;
                                RotateCursorIcon.Height = cursorSize;
                            }
                            if (RotateCursorIconOutline != null)
                            {
                                RotateCursorIconOutline.Width = cursorSize + 4.0 / zoom;
                                RotateCursorIconOutline.Height = cursorSize + 4.0 / zoom;
                                Canvas.SetLeft(RotateCursorIconOutline, -2.0 / zoom);
                                Canvas.SetTop(RotateCursorIconOutline, -2.0 / zoom);
                            }
                        }
                    }
                    else
                    {
                        TransformOverlayCanvas.Cursor = Cursors.Arrow;
                        if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;
                    }
                }
                return;
            }

            double dx = currentMouse.X - _transformDragStartMouse.X;
            double dy = currentMouse.Y - _transformDragStartMouse.Y;

            if (_activeTransformHandle == "Rotate")
            {
                Point currentCenter = new Point(untransformedCenter.X + _sessionTranslateX, untransformedCenter.Y + _sessionTranslateY);
                double startAngle = Math.Atan2(_transformDragStartMouse.Y - currentCenter.Y, _transformDragStartMouse.X - currentCenter.X) * 180.0 / Math.PI;
                double currentAngle = Math.Atan2(currentMouse.Y - currentCenter.Y, currentMouse.X - currentCenter.X) * 180.0 / Math.PI;
                
                double dAngle = currentAngle - startAngle;
                while (dAngle > 180) dAngle -= 360;
                while (dAngle < -180) dAngle += 360;
                _sessionAngle = _startDragAngle + dAngle;

                if (RotateCursorCanvas != null)
                {
                    double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                    if (zoom <= 0) zoom = 1.0;
                    double cursorSize = 14.0 / zoom;

                    RotateCursorCanvas.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RotateCursorCanvas, currentMouse.X - cursorSize / 2.0);
                    Canvas.SetTop(RotateCursorCanvas, currentMouse.Y - cursorSize / 2.0);

                    RotateCursorCanvas.Width = cursorSize;
                    RotateCursorCanvas.Height = cursorSize;
                    if (RotateCursorIcon != null)
                    {
                        RotateCursorIcon.Width = cursorSize;
                        RotateCursorIcon.Height = cursorSize;
                    }
                    if (RotateCursorIconOutline != null)
                    {
                        RotateCursorIconOutline.Width = cursorSize + 4.0 / zoom;
                        RotateCursorIconOutline.Height = cursorSize + 4.0 / zoom;
                        Canvas.SetLeft(RotateCursorIconOutline, -2.0 / zoom);
                        Canvas.SetTop(RotateCursorIconOutline, -2.0 / zoom);
                    }
                }
            }
            else if (_activeTransformHandle == "Move")
            {
                _sessionTranslateX = _startDragTranslateX + dx;
                _sessionTranslateY = _startDragTranslateY + dy;
            }
            else
            {
                // Align scaling axes along the rotated angle
                double rad = _startDragAngle * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);

                double localDx = dx * cos + dy * sin;
                double localDy = -dx * sin + dy * cos;

                // Center-relative scaling multiplies scale change rate by 2.0x
                double deltaScaleX = 2.0 * localDx / _transformDragStartRect.Width;
                double deltaScaleY = 2.0 * localDy / _transformDragStartRect.Height;

                if (_activeTransformHandle.Contains("R"))
                    _sessionScaleX = _startDragScaleX + deltaScaleX;
                else if (_activeTransformHandle.Contains("L"))
                    _sessionScaleX = _startDragScaleX - deltaScaleX;

                if (_activeTransformHandle.Contains("B"))
                    _sessionScaleY = _startDragScaleY + deltaScaleY;
                else if (_activeTransformHandle.Contains("T"))
                    _sessionScaleY = _startDragScaleY - deltaScaleY;

                // Clamp scale magnitude slightly to avoid complete collapse to zero
                if (Math.Abs(_sessionScaleX) < 0.02) _sessionScaleX = Math.Sign(_sessionScaleX) * 0.02;
                if (Math.Abs(_sessionScaleY) < 0.02) _sessionScaleY = Math.Sign(_sessionScaleY) * 0.02;
            }

            UpdateVisualTransforms();
            e.Handled = true;
        }

        private void UpdateVisualTransforms()
        {
            TransformPreviewScale.ScaleX = _sessionScaleX;
            TransformPreviewScale.ScaleY = _sessionScaleY;
            TransformPreviewRotate.Angle = _sessionAngle;
            TransformPreviewTranslate.X = _sessionTranslateX;
            TransformPreviewTranslate.Y = _sessionTranslateY;

            TransformBoxScale.ScaleX = _sessionScaleX;
            TransformBoxScale.ScaleY = _sessionScaleY;
            TransformBoxRotate.Angle = _sessionAngle;
            TransformBoxTranslate.X = _sessionTranslateX;
            TransformBoxTranslate.Y = _sessionTranslateY;

            // Apply inverse scale to handles so their visual size on screen remains constant
            double invScaleX = Math.Abs(_sessionScaleX) > 0.001 ? 1.0 / _sessionScaleX : 1.0;
            double invScaleY = Math.Abs(_sessionScaleY) > 0.001 ? 1.0 / _sessionScaleY : 1.0;
            var invScale = new ScaleTransform(invScaleX, invScaleY);

            var handles = new[] { TransHandle_TL, TransHandle_T, TransHandle_TR, TransHandle_R, TransHandle_BR, TransHandle_B, TransHandle_BL, TransHandle_L };
            foreach (var h in handles)
            {
                if (h != null)
                {
                    h.RenderTransformOrigin = new Point(0.5, 0.5);
                    h.RenderTransform = invScale;
                }
            }

            // Compensate border StrokeThickness so it doesn't get thick/thin when scaled
            if (TransformBoxBorderPath != null)
            {
                double zoom = ImageZoomScale != null ? ImageZoomScale.ScaleX : 1.0;
                if (zoom <= 0) zoom = 1.0;
                double baseThickness = 2.5 / zoom;
                double avgScale = (Math.Abs(_sessionScaleX) + Math.Abs(_sessionScaleY)) / 2.0;
                if (avgScale > 0.001)
                {
                    TransformBoxBorderPath.StrokeThickness = baseThickness / avgScale;
                }
            }
        }

        private void TransformOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingTransform) return;

            _isDraggingTransform = false;
            TransformOverlayCanvas.ReleaseMouseCapture();
            TransformOverlayCanvas.Cursor = Cursors.Arrow;
            if (RotateCursorCanvas != null) RotateCursorCanvas.Visibility = Visibility.Collapsed;

            e.Handled = true;
        }

        #endregion
    }
}
