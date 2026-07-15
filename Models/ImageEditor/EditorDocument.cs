using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Document chính của editor — chứa layer stack, history, và compositing logic.
    /// Mỗi ImageProcessingNode có tối đa 1 EditorDocument (tạo khi vào Manual mode).
    /// </summary>
    public sealed class EditorDocument : INotifyPropertyChanged
    {
        private EditorLayer? _activeLayer;
        private Color _foregroundColor = Colors.Black;
        private Color _backgroundColor = Colors.White;
        private int _nextLayerNumber = 1;
        private RenderTargetBitmap? _cachedRenderTarget;
        private WriteableBitmap? _cachedCpuRenderTarget;

        /// <summary>Get or create the cached CPU render target for fast compositing.</summary>
        public WriteableBitmap? GetCachedCpuRenderTarget()
        {
            if (_cachedCpuRenderTarget == null ||
                _cachedCpuRenderTarget.PixelWidth != Width ||
                _cachedCpuRenderTarget.PixelHeight != Height)
            {
                _cachedCpuRenderTarget = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32, null);
            }
            return _cachedCpuRenderTarget;
        }
        private byte[]? _cachedCpuResultPixels;
        private byte[]? _cachedCpuLayerPixels;

        public EditorDocument(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("Document dimensions must be positive.");

            Width = width;
            Height = height;
            History = new EditorHistory();
            Layers = new ObservableCollection<EditorLayer>();
        }

        /// <summary>Kích thước document (pixel).</summary>
        public int Width { get; internal set; }
        public int Height { get; internal set; }

        /// <summary>Đường dẫn tệp dự án (.iep) đã lưu hoặc nạp.</summary>
        public string? ProjectPath { get; set; }

        /// <summary>Stack các layer (index 0 = bottom, last = top).</summary>
        public ObservableCollection<EditorLayer> Layers { get; }

        /// <summary>Layer đang được chọn để vẽ/chỉnh sửa.</summary>
        public EditorLayer? ActiveLayer
        {
            get => _activeLayer;
            set => SetField(ref _activeLayer, value);
        }

        /// <summary>Hệ thống undo/redo.</summary>
        public EditorHistory History { get; }

        /// <summary>Màu foreground (brush color).</summary>
        public Color ForegroundColor
        {
            get => _foregroundColor;
            set => SetField(ref _foregroundColor, value);
        }

        /// <summary>Màu background (eraser reveal color).</summary>
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set => SetField(ref _backgroundColor, value);
        }

        /// <summary>
        /// Tạo document từ BitmapSource (ảnh gốc → Layer 0 "Background").
        /// </summary>
        public static EditorDocument FromBitmapSource(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var doc = new EditorDocument(source.PixelWidth, source.PixelHeight);
            var bgLayer = new EditorLayer(doc.Width, doc.Height, "layer 0");
            bgLayer.CopyFrom(source);
            bgLayer.IsLocked = true;

            doc.Layers.Add(bgLayer);
            doc.ActiveLayer = bgLayer;
            doc._nextLayerNumber = 1;
            return doc;
        }

        /// <summary>
        /// Tạo document trống (canvas trắng).
        /// </summary>
        public static EditorDocument CreateBlank(int width, int height)
        {
            var doc = new EditorDocument(width, height);
            var bgLayer = new EditorLayer(width, height, "layer 0");
            bgLayer.Fill(Colors.White);
            bgLayer.IsLocked = true;

            doc.Layers.Add(bgLayer);
            doc.ActiveLayer = bgLayer;
            doc._nextLayerNumber = 1;
            return doc;
        }

        /// <summary>Lấy tên layer tiếp theo (số duy nhất, không bao giờ tái sử dụng).</summary>
        public string GetNextLayerName()
        {
            return $"layer {_nextLayerNumber++}";
        }

        /// <summary>Thêm 1 layer mới (transparent) lên trên active layer.</summary>
        public EditorLayer AddNewLayer(string name = "")
        {
            int insertIndex = Layers.Count;
            if (_activeLayer != null)
            {
                int activeIdx = Layers.IndexOf(_activeLayer);
                if (activeIdx >= 0) insertIndex = activeIdx + 1;
            }

            if (string.IsNullOrEmpty(name))
                name = GetNextLayerName();

            var layer = new EditorLayer(Width, Height, name);
            Layers.Insert(insertIndex, layer);
            ActiveLayer = layer;
            return layer;
        }

        /// <summary>Xoá 1 layer. Không cho xoá layer cuối cùng.</summary>
        public bool RemoveLayer(EditorLayer layer)
        {
            if (layer == null || Layers.Count <= 1) return false;

            int idx = Layers.IndexOf(layer);
            if (idx < 0) return false;

            Layers.RemoveAt(idx);

            // Chọn layer gần nhất
            if (ActiveLayer == layer)
                ActiveLayer = Layers[Math.Min(idx, Layers.Count - 1)];

            return true;
        }

        /// <summary>Di chuyển layer trong stack.</summary>
        public void MoveLayer(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Layers.Count) return;
            if (toIndex < 0 || toIndex >= Layers.Count) return;
            if (fromIndex == toIndex) return;

            Layers.Move(fromIndex, toIndex);
        }

        /// <summary>Xoay toàn bộ canvas và tất cả layer trong tài liệu.</summary>
        public void RotateCanvas(double angle)
        {
            if (angle != 90 && angle != -90 && angle != 270 && angle != 180)
                return;

            int oldW = Width;
            int oldH = Height;
            int newW = (angle == 180) ? oldW : oldH;
            int newH = (angle == 180) ? oldH : oldW;

            var cmd = new Commands.RotateCanvasCommand(this, angle, oldW, oldH, newW, newH);
            History.Execute(cmd);
        }

        /// <summary>
        /// Composite tất cả visible layers → 1 BitmapSource.
        /// Sử dụng GPU Rendering (RenderTargetBitmap) cho Normal blend mode,
        /// và tự động fall back về CPU Rendering khi sử dụng blend mode phức tạp.
        /// </summary>
        public BitmapSource Composite()
        {
            // Force CPU rendering via SkiaSharp which is 100x faster than WPF's software-backed RenderTargetBitmap
            bool useGPU = false;

            if (useGPU)
            {
                var drawingVisual = new DrawingVisual();
                RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    foreach (var layer in Layers)
                    {
                        if (!layer.IsVisible || layer.Opacity <= 0 || layer.IsTempHidden) continue;

                        drawingContext.PushOpacity(layer.Opacity);

                        if (layer.IsTextLayer)
                        {
                            // Support layer transformations for text layers!
                            double centerX = layer.TextX + layer.TextWidth / 2.0;
                            double centerY = layer.TextY + layer.TextHeight / 2.0;

                            var transformGroup = new TransformGroup();
                            transformGroup.Children.Add(new TranslateTransform(-centerX, -centerY));
                            transformGroup.Children.Add(new ScaleTransform(layer.LayerScaleX, layer.LayerScaleY));
                            transformGroup.Children.Add(new RotateTransform(layer.LayerAngle));

                            double totalDx = layer.LayerTranslateX + layer.TempMoveDx;
                            double totalDy = layer.LayerTranslateY + layer.TempMoveDy;
                            transformGroup.Children.Add(new TranslateTransform(centerX + totalDx, centerY + totalDy));

                            drawingContext.PushTransform(transformGroup);

                            var fontStyle = layer.TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
                            var fontWeight = layer.TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
                            var typeface = new Typeface(new FontFamily(layer.TextFontFamily), fontStyle, fontWeight, FontStretches.Normal);
                            
                            var formattedText = new FormattedText(
                                layer.TextContent ?? "",
                                System.Globalization.CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                layer.TextFontSize,
                                new SolidColorBrush(layer.TextColor),
                                1.0
                            );

                            formattedText.MaxTextWidth = Math.Max(1, layer.TextWidth);
                            formattedText.MaxTextHeight = Math.Max(1, layer.TextHeight);
                            formattedText.TextAlignment = layer.TextAlignment == "Center" ? TextAlignment.Center :
                                                          layer.TextAlignment == "Right" ? TextAlignment.Right : TextAlignment.Left;

                            drawingContext.DrawText(formattedText, new Point(layer.TextX, layer.TextY));
                            
                            drawingContext.Pop(); // Pop Transform
                            drawingContext.Pop(); // Pop Opacity
                            continue;
                        }

                        var activeBmp = layer.Bitmap;
                        var activeOrig = layer.OriginalTransformBitmap;
                        var activeContentBounds = layer.ContentBounds;

                        if (layer.ActiveChildLayer != null)
                        {
                            activeBmp = layer.ActiveChildLayer.Bitmap;
                            activeOrig = layer.ActiveChildLayer.OriginalTransformBitmap;
                            activeContentBounds = layer.ActiveChildLayer.ContentBounds;
                        }

                        if (activeOrig != null && layer.TempSelectionGeometry == null)
                        {
                            // Dynamic non-destructive high-quality transformation rendering from the original bitmap using cached bounds
                            var contentBounds = activeContentBounds;
                            if (contentBounds.IsEmpty || contentBounds.Width <= 0 || contentBounds.Height <= 0)
                            {
                                contentBounds = new Rect(0, 0, Width, Height);
                            }
                            double centerX = contentBounds.Left + contentBounds.Width / 2.0;
                            double centerY = contentBounds.Top + contentBounds.Height / 2.0;

                            var transformGroup = new TransformGroup();
                            transformGroup.Children.Add(new TranslateTransform(-centerX, -centerY));
                            transformGroup.Children.Add(new ScaleTransform(layer.LayerScaleX, layer.LayerScaleY));
                            transformGroup.Children.Add(new RotateTransform(layer.LayerAngle));

                            double totalDx = layer.LayerTranslateX + layer.TempMoveDx;
                            double totalDy = layer.LayerTranslateY + layer.TempMoveDy;
                            transformGroup.Children.Add(new TranslateTransform(centerX + totalDx, centerY + totalDy));

                            drawingContext.PushTransform(transformGroup);
                            var destRect = activeContentBounds;
                            if (destRect.IsEmpty || destRect.Width <= 0 || destRect.Height <= 0)
                            {
                                destRect = new Rect(0, 0, Width, Height);
                            }
                            drawingContext.DrawImage(activeOrig, destRect);
                            drawingContext.Pop();
                        }
                        else if (layer.TempMoveDx != 0 || layer.TempMoveDy != 0)
                        {
                            if (layer.TempSelectionGeometry != null)
                            {
                                // Draw background (everything outside selection)
                                var boundsGeom = new RectangleGeometry(new Rect(0, 0, Width, Height));
                                var bgGeom = Geometry.Combine(boundsGeom, layer.TempSelectionGeometry, GeometryCombineMode.Exclude, null);
                                drawingContext.PushClip(bgGeom);
                                drawingContext.DrawImage(activeBmp, new Rect(0, 0, Width, Height));
                                drawingContext.Pop();

                                // Draw shifted selection
                                var transform = new TranslateTransform(layer.TempMoveDx, layer.TempMoveDy);
                                var fgGeom = Geometry.Combine(layer.TempSelectionGeometry, Geometry.Empty, GeometryCombineMode.Union, transform);
                                drawingContext.PushClip(fgGeom);
                                drawingContext.DrawImage(activeBmp, new Rect(layer.TempMoveDx, layer.TempMoveDy, Width, Height));
                                drawingContext.Pop();
                            }
                            else
                            {
                                // Shift entire layer
                                drawingContext.DrawImage(activeBmp, new Rect(layer.TempMoveDx, layer.TempMoveDy, Width, Height));
                            }
                        }
                        else
                        {
                            drawingContext.DrawImage(activeBmp, new Rect(0, 0, Width, Height));
                        }

                        drawingContext.Pop();
                    }
                }

                // Render bằng GPU thông qua RenderTargetBitmap
                if (_cachedRenderTarget == null ||
                    _cachedRenderTarget.PixelWidth != Width ||
                    _cachedRenderTarget.PixelHeight != Height)
                {
                    _cachedRenderTarget = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
                }
                _cachedRenderTarget.Render(drawingVisual);
                return _cachedRenderTarget;
            }
            else
            {
                // CPU rendering tối ưu: 3-phase lock management
                // Phase 1: Lock tất cả layer bitmaps (instant vì không nằm trong visual tree)
                // Phase 2: Lock render target, vẽ từ BackBuffer pointers (zero-copy), unlock render target
                // Phase 3: Unlock tất cả layer bitmaps
                // → Không nested lock, không GC pressure, không deadlock
                try { System.IO.File.AppendAllText(@"d:\UngDung_PC\Flow-App\error.log", $"[{DateTime.Now:HH:mm:ss.fff}] Composite START, Layers={Layers.Count}\n"); } catch { }

                if (_cachedCpuRenderTarget == null ||
                    _cachedCpuRenderTarget.PixelWidth != Width ||
                    _cachedCpuRenderTarget.PixelHeight != Height)
                {
                    _cachedCpuRenderTarget = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32, null);
                }

                // ====== PHASE 1: Thu thập pixel data KHÔNG Lock bất kỳ WPF bitmap nào ======
                var entries = new System.Collections.Generic.List<(
                    EditorLayer layer,
                    byte[]? pixelData,                 // pixel data snapshot (pinned cho Skia)
                    int bmpW, int bmpH, int bmpStride,
                    SkiaSharp.SKBitmap? cachedSK,      // cached SKBitmap gốc (nếu có)
                    Rect contentBounds
                )>();

                foreach (var layer in Layers)
                {
                    if (!layer.IsVisible || layer.Opacity <= 0 || layer.IsTempHidden) continue;

                    if (layer.IsTextLayer)
                    {
                        entries.Add((layer, null, 0, 0, 0, null, Rect.Empty));
                        continue;
                    }

                    var activeBmp = layer.ActiveChildLayer != null ? layer.ActiveChildLayer.Bitmap : layer.Bitmap;
                    var activeOrigSK = layer.ActiveChildLayer != null ? layer.ActiveChildLayer.CachedOriginalSKBitmap : layer.CachedOriginalSKBitmap;
                    var activeContentBounds = layer.ActiveChildLayer != null ? layer.ActiveChildLayer.ContentBounds : layer.ContentBounds;

                    if (activeOrigSK != null && layer.TempSelectionGeometry == null)
                    {
                        // Có cached SKBitmap gốc → không cần copy pixel
                        entries.Add((layer, null, 0, 0, 0, activeOrigSK, activeContentBounds));
                    }
                    else
                    {
                        // CopyPixels KHÔNG cần Lock — read-only, an toàn
                        try
                        {
                            int bmpW = activeBmp.PixelWidth;
                            int bmpH = activeBmp.PixelHeight;
                            int bmpStride = bmpW * 4;
                            byte[] px = new byte[bmpStride * bmpH];
                            activeBmp.CopyPixels(px, bmpStride, 0);
                            entries.Add((layer, px, bmpW, bmpH, bmpStride, null, activeContentBounds));
                        }
                        catch
                        {
                            // Skip layer nếu đọc thất bại
                        }
                    }
                }

                // ====== PHASE 2: Lock render target, vẽ tất cả, unlock render target ======
                try { System.IO.File.AppendAllText(@"d:\UngDung_PC\Flow-App\error.log", $"[{DateTime.Now:HH:mm:ss.fff}] Phase1 DONE, entries={entries.Count}, locking render target...\n"); } catch { }
                _cachedCpuRenderTarget.Lock();
                try
                {
                    var info = new SkiaSharp.SKImageInfo(Width, Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var surface = SkiaSharp.SKSurface.Create(info, _cachedCpuRenderTarget.BackBuffer, _cachedCpuRenderTarget.BackBufferStride))
                    {
                        if (surface != null)
                        {
                            var canvas = surface.Canvas;
                            canvas.Clear(SkiaSharp.SKColors.Transparent);

                            foreach (var entry in entries)
                            {
                                var layer = entry.layer;

                                if (layer.IsTextLayer)
                                {
                                    using (var paint = new SkiaSharp.SKPaint())
                                    {
                                        paint.IsAntialias = true;
                                        paint.TextSize = (float)layer.TextFontSize;
                                        paint.Color = new SkiaSharp.SKColor(
                                            layer.TextColor.R, layer.TextColor.G, layer.TextColor.B,
                                            (byte)Math.Clamp(layer.TextColor.A * layer.Opacity, 0, 255));

                                        var slant = layer.TextFontStyle == "Italic" ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright;
                                        var weight = layer.TextFontStyle == "Bold" ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal;
                                        paint.Typeface = SkiaSharp.SKTypeface.FromFamilyName(layer.TextFontFamily, weight, SkiaSharp.SKFontStyleWidth.Normal, slant);

                                        paint.TextAlign = SkiaSharp.SKTextAlign.Left;
                                        float textX = (float)layer.TextX;
                                        if (layer.TextAlignment == "Center") { paint.TextAlign = SkiaSharp.SKTextAlign.Center; textX = (float)(layer.TextX + layer.TextWidth / 2.0); }
                                        else if (layer.TextAlignment == "Right") { paint.TextAlign = SkiaSharp.SKTextAlign.Right; textX = (float)(layer.TextX + layer.TextWidth); }

                                        string[] lines = (layer.TextContent ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                                        SkiaSharp.SKFontMetrics metrics;
                                        paint.GetFontMetrics(out metrics);
                                        float lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
                                        if (lineHeight <= 0) lineHeight = (float)layer.TextFontSize * 1.2f;
                                        float currentY = (float)(layer.TextY - metrics.Ascent);

                                        canvas.Save();
                                        float cX = (float)(layer.TextX + layer.TextWidth / 2.0);
                                        float cY = (float)(layer.TextY + layer.TextHeight / 2.0);
                                        float tDx = (float)(layer.LayerTranslateX + layer.TempMoveDx);
                                        float tDy = (float)(layer.LayerTranslateY + layer.TempMoveDy);
                                        canvas.Translate(cX + tDx, cY + tDy);
                                        canvas.RotateDegrees((float)layer.LayerAngle);
                                        canvas.Scale((float)layer.LayerScaleX, (float)layer.LayerScaleY);
                                        canvas.Translate(-cX, -cY);

                                        foreach (var line in lines)
                                        {
                                            if (currentY - layer.TextY > layer.TextHeight) break;
                                            canvas.DrawText(line, textX, currentY, paint);
                                            currentY += lineHeight;
                                        }
                                        canvas.Restore();
                                    }
                                    continue;
                                }

                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.IsAntialias = true;
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

                                    if (entry.cachedSK != null)
                                    {
                                        // Vẽ từ cached SKBitmap gốc với transform
                                        canvas.Save();
                                        var cb = entry.contentBounds;
                                        if (cb.IsEmpty || cb.Width <= 0 || cb.Height <= 0) cb = new Rect(0, 0, Width, Height);
                                        double centerX = cb.Left + cb.Width / 2.0;
                                        double centerY = cb.Top + cb.Height / 2.0;
                                        double tdx = layer.LayerTranslateX + layer.TempMoveDx;
                                        double tdy = layer.LayerTranslateY + layer.TempMoveDy;
                                        canvas.Translate((float)(centerX + tdx), (float)(centerY + tdy));
                                        canvas.RotateDegrees((float)layer.LayerAngle);
                                        canvas.Scale((float)layer.LayerScaleX, (float)layer.LayerScaleY);
                                        canvas.Translate((float)-centerX, (float)-centerY);
                                        var dr = entry.contentBounds;
                                        if (dr.IsEmpty || dr.Width <= 0 || dr.Height <= 0) dr = new Rect(0, 0, Width, Height);
                                        canvas.DrawBitmap(entry.cachedSK, new SkiaSharp.SKRect((float)dr.Left, (float)dr.Top, (float)dr.Right, (float)dr.Bottom), paint);
                                        canvas.Restore();
                                    }
                                    else if (entry.pixelData != null)
                                    {
                                        // Vẽ từ pixel data snapshot — pin byte array cho Skia
                                        var gcPin = System.Runtime.InteropServices.GCHandle.Alloc(entry.pixelData, System.Runtime.InteropServices.GCHandleType.Pinned);
                                        try
                                        {
                                            var srcInfo = new SkiaSharp.SKImageInfo(entry.bmpW, entry.bmpH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                                            using (var skBmp = new SkiaSharp.SKBitmap())
                                            {
                                                skBmp.InstallPixels(srcInfo, gcPin.AddrOfPinnedObject(), entry.bmpStride);
                                                canvas.Save();

                                                if (layer.TempMoveDx != 0 || layer.TempMoveDy != 0)
                                                {
                                                    if (layer.TempSelectionPath != null)
                                                    {
                                                        canvas.Save();
                                                        canvas.ClipPath(layer.TempSelectionPath, SkiaSharp.SKClipOperation.Difference, true);
                                                        canvas.DrawBitmap(skBmp, 0, 0, paint);
                                                        canvas.Restore();

                                                        canvas.Save();
                                                        canvas.Translate((float)layer.TempMoveDx, (float)layer.TempMoveDy);
                                                        canvas.ClipPath(layer.TempSelectionPath, SkiaSharp.SKClipOperation.Intersect, true);
                                                        canvas.DrawBitmap(skBmp, 0, 0, paint);
                                                        canvas.Restore();
                                                    }
                                                    else
                                                    {
                                                        canvas.DrawBitmap(skBmp, (float)layer.TempMoveDx, (float)layer.TempMoveDy, paint);
                                                    }
                                                }
                                                else
                                                {
                                                    canvas.DrawBitmap(skBmp, 0, 0, paint);
                                                }
                                                canvas.Restore();
                                            }
                                        }
                                        finally
                                        {
                                            gcPin.Free();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    _cachedCpuRenderTarget.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
                }
                finally
                {
                    _cachedCpuRenderTarget.Unlock();
                }
                try { System.IO.File.AppendAllText(@"d:\UngDung_PC\Flow-App\error.log", $"[{DateTime.Now:HH:mm:ss.fff}] Composite DONE\n"); } catch { }

                return _cachedCpuRenderTarget;
            }
        }

        /// <summary>Flatten tất cả layers → 1 frozen BitmapSource.</summary>
        public BitmapSource Flatten()
        {
            var result = Composite();
            if (result.CanFreeze)
            {
                result.Freeze();
            }
            return result;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
        #endregion
        public void RaisePropertyChanged(string name) => OnPropertyChanged(name);
    }
}
