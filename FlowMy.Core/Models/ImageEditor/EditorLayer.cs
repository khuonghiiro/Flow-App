using System;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Một layer trong editor. Mỗi layer chứa một WriteableBitmap (BGRA32)
    /// cùng kích thước với document, cùng các thuộc tính hiển thị (opacity, blend, visibility).
    /// </summary>
    public sealed class EditorLayer : INotifyPropertyChanged
    {
        private string _name;
        private double _opacity = 1.0;
        private bool _isVisible = true;
        private bool _isLocked;
        private BlendMode _blendMode = BlendMode.Normal;
        private int _offsetX;
        private int _offsetY;
        private WriteableBitmap? _thumbnailCache;
        private string _codeId = Guid.NewGuid().ToString("N");

        /// <summary>Mã định danh duy nhất (GUID) của layer để map kết quả AI từ workflow về đúng layer root.</summary>
        public string CodeId
        {
            get => _codeId;
            set => SetField(ref _codeId, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value);
        }

        // Parent/Child/Variant hierarchy for AI edits
        private EditorLayer? _parentLayer;
        public EditorLayer? ParentLayer
        {
            get => _parentLayer;
            set
            {
                if (SetField(ref _parentLayer, value))
                {
                    OnPropertyChanged(nameof(IsChildLayer));
                }
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<EditorLayer> ChildLayers { get; } = new();

        private EditorLayer? _activeChildLayer;
        public EditorLayer? ActiveChildLayer
        {
            get => _activeChildLayer;
            set
            {
                if (SetField(ref _activeChildLayer, value))
                {
                    foreach (var child in ChildLayers)
                    {
                        child.IsActiveVariant = (child == value);
                    }
                    OnPropertyChanged(nameof(IsParentOriginalActive));
                }
            }
        }

        private bool _isActiveVariant;
        public bool IsActiveVariant
        {
            get => _isActiveVariant;
            set => SetField(ref _isActiveVariant, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private int _loadingProgress;
        public int LoadingProgress
        {
            get => _loadingProgress;
            set => SetField(ref _loadingProgress, value);
        }

        private bool _isLoadingError;
        public bool IsLoadingError
        {
            get => _isLoadingError;
            set => SetField(ref _isLoadingError, value);
        }

        private bool _isChildrenCollapsed;
        public bool IsChildrenCollapsed
        {
            get => _isChildrenCollapsed;
            set
            {
                if (SetField(ref _isChildrenCollapsed, value))
                {
                    OnPropertyChanged(nameof(CollapseIcon));
                }
            }
        }

        /// <summary>Icon cho nút collapse: ▼ khi mở, ▶ khi đóng.</summary>
        public string CollapseIcon => _isChildrenCollapsed ? "▶" : "▼";

        /// <summary>Có ChildLayers không (dùng cho Visibility của nút collapse).</summary>
        public bool HasChildren => ChildLayers.Count > 0;

        private System.Windows.Threading.DispatcherTimer? _loadingTimer;

        /// <summary>Bắt đầu timer tăng LoadingProgress 1%/giây (max 99%).</summary>
        public void StartLoadingTimer()
        {
            LoadingProgress = 0;
            IsLoadingError = false;
            _loadingTimer?.Stop();
            _loadingTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _loadingTimer.Tick += (s, e) =>
            {
                if (LoadingProgress < 99)
                    LoadingProgress++;
            };
            _loadingTimer.Start();
        }

        /// <summary>Dừng timer. Nếu thành công → 100%, nếu lỗi → giữ nguyên.</summary>
        public void StopLoadingTimer(bool isError = false)
        {
            _loadingTimer?.Stop();
            _loadingTimer = null;
            if (!isError)
                LoadingProgress = 100;
        }

        public bool IsChildLayer => ParentLayer != null;
        public bool IsParentOriginalActive => ParentLayer == null && ActiveChildLayer == null;

        public EditorLayer(int width, int height, string name = "Layer")
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("Layer dimensions must be positive.");

            Id = Guid.NewGuid().ToString("N");
            _name = name;
            Width = width;
            Height = height;
            Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        }

        /// <summary>ID duy nhất của layer.</summary>
        public string Id { get; set; }

        /// <summary>Kích thước layer (có thể khác document — layer ảnh dùng kích thước ảnh gốc).</summary>
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Vị trí layer trên canvas document (top-left offset).</summary>
        public int OffsetX
        {
            get => ParentLayer != null ? ParentLayer.OffsetX : _offsetX;
            set
            {
                if (ParentLayer != null)
                {
                    ParentLayer.OffsetX = value;
                }
                else
                {
                    SetField(ref _offsetX, value);
                }
            }
        }
        public int OffsetY
        {
            get => ParentLayer != null ? ParentLayer.OffsetY : _offsetY;
            set
            {
                if (ParentLayer != null)
                {
                    ParentLayer.OffsetY = value;
                }
                else
                {
                    SetField(ref _offsetY, value);
                }
            }
        }

        /// <summary>Bitmap pixel data (BGRA32, kích thước = Width×Height).</summary>
        public WriteableBitmap Bitmap { get; set; }

        /// <summary>Tên hiển thị.</summary>
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        /// <summary>Opacity 0.0 (trong suốt) → 1.0 (đục hoàn toàn).</summary>
        public double Opacity
        {
            get => _opacity;
            set => SetField(ref _opacity, Math.Clamp(value, 0.0, 1.0));
        }

        /// <summary>Ẩn/hiện layer.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }

        /// <summary>Tạm ẩn layer trong quá trình biến đổi.</summary>
        public bool IsTempHidden { get; set; }

        private SkiaSharp.SKBitmap? _cachedOriginalSKBitmap;
        public SkiaSharp.SKBitmap? CachedOriginalSKBitmap => _cachedOriginalSKBitmap;

        private WriteableBitmap? _originalTransformBitmap;
        public WriteableBitmap? OriginalTransformBitmap
        {
            get => _originalTransformBitmap;
            set
            {
                _originalTransformBitmap = value;
                
                _cachedOriginalSKBitmap?.Dispose();
                _cachedOriginalSKBitmap = null;
                
                if (value == null)
                {
                    LayerScaleX = 1.0;
                    LayerScaleY = 1.0;
                    LayerAngle = 0.0;
                    LayerTranslateX = 0.0;
                    LayerTranslateY = 0.0;
                    ContentBounds = Rect.Empty;
                }
                else
                {
                    int w = value.PixelWidth;
                    int h = value.PixelHeight;
                    var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    _cachedOriginalSKBitmap = new SkiaSharp.SKBitmap();
                    _cachedOriginalSKBitmap.TryAllocPixels(info);
                    
                    int stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    // CopyPixels là read-only — KHÔNG cần Lock/Unlock
                    value.CopyPixels(pixels, stride, 0);
                    
                    var dstPtr = _cachedOriginalSKBitmap.GetPixels();
                    if (dstPtr != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, dstPtr, pixels.Length);
                    }

                    ContentBounds = CalculateContentBounds(value);
                }
            }
        }

        ~EditorLayer()
        {
            _cachedOriginalSKBitmap?.Dispose();
        }

        public Rect ContentBounds { get; set; } = Rect.Empty;
        
        private Rect _imageContentBounds = Rect.Empty;
        /// <summary>Vùng ảnh gốc khi load vào layer (không bị clear bởi commands/tools).
        /// Dùng cho brush/eraser clipping. Chỉ set khi load ảnh vào layer.</summary>
        public Rect ImageContentBounds
        {
            get => ParentLayer != null ? ParentLayer.ImageContentBounds : _imageContentBounds;
            set
            {
                if (ParentLayer != null)
                {
                    ParentLayer.ImageContentBounds = value;
                }
                else
                {
                    _imageContentBounds = value;
                }
            }
        }

        private Geometry? _contentGeometry;
        public Geometry? ContentGeometry
        {
            get => ParentLayer != null ? ParentLayer.ContentGeometry : _contentGeometry;
            set
            {
                if (ParentLayer != null)
                {
                    ParentLayer.ContentGeometry = value;
                }
                else
                {
                    _contentGeometry = value;
                }
            }
        }

        public static Rect CalculateContentBounds(WriteableBitmap bitmap)
        {
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            bitmap.CopyPixels(pixels, stride, 0);

            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool found = false;

            // Parallel scan cho ảnh lớn (>1M pixels) — giảm từ ~36ms xuống ~4ms
            if (w * h > 1_000_000)
            {
                object lockObj = new object();
                System.Threading.Tasks.Parallel.For(0, h, () => (w, 0, h, 0, false),
                    (y, _, localState) =>
                    {
                        int lMinX = localState.Item1, lMaxX = localState.Item2;
                        int lMinY = localState.Item3, lMaxY = localState.Item4;
                        bool lFound = localState.Item5;
                        int rowOffset = y * stride;

                        for (int x = 0; x < w; x++)
                        {
                            byte alpha = pixels[rowOffset + x * 4 + 3];
                            if (alpha > 5)
                            {
                                if (x < lMinX) lMinX = x;
                                if (x > lMaxX) lMaxX = x;
                                if (y < lMinY) lMinY = y;
                                if (y > lMaxY) lMaxY = y;
                                lFound = true;
                            }
                        }
                        return (lMinX, lMaxX, lMinY, lMaxY, lFound);
                    },
                    localState =>
                    {
                        if (localState.Item5)
                        {
                            lock (lockObj)
                            {
                                if (localState.Item1 < minX) minX = localState.Item1;
                                if (localState.Item2 > maxX) maxX = localState.Item2;
                                if (localState.Item3 < minY) minY = localState.Item3;
                                if (localState.Item4 > maxY) maxY = localState.Item4;
                                found = true;
                            }
                        }
                    });
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        if (alpha > 5)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
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

        public double LayerScaleX { get; set; } = 1.0;
        public double LayerScaleY { get; set; } = 1.0;
        public double LayerAngle { get; set; } = 0.0;
        public double LayerTranslateX { get; set; } = 0.0;
        public double LayerTranslateY { get; set; } = 0.0;

        /// <summary>Khoá layer (không cho vẽ/sửa).</summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => SetField(ref _isLocked, value);
        }

        /// <summary>Layer đang được chọn (active) trong panel.</summary>
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        /// <summary>Blend mode khi composite với các layer bên dưới.</summary>
        public BlendMode BlendMode
        {
            get => _blendMode;
            set => SetField(ref _blendMode, value);
        }

        /// <summary>Xoá toàn bộ pixel (fill transparent).</summary>
        public void Clear()
        {
            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Bitmap.Lock();
            try
            {
                using (var surface = SKSurface.Create(info, Bitmap.BackBuffer, Bitmap.BackBufferStride))
                {
                    if (surface != null)
                    {
                        surface.Canvas.Clear(SKColors.Transparent);
                    }
                }
                Bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
            }
            finally
            {
                Bitmap.Unlock();
            }
            InvalidateThumbnail();
        }

        /// <summary>Fill toàn bộ layer bằng 1 màu.</summary>
        public void Fill(Color color)
        {
            var stride = Width * 4;
            var pixels = new byte[stride * Height];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = color.A;
            }
            Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixels, stride, 0);
            InvalidateThumbnail();
        }

        /// <summary>Copy pixel data từ BitmapSource vào layer (resize nếu cần).</summary>
        public void CopyFrom(BitmapSource source)
        {
            if (source == null) return;

            // Convert sang BGRA32 nếu cần
            var converted = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            // Resize nếu kích thước khác
            if (converted.PixelWidth != Width || converted.PixelHeight != Height)
            {
                var scaled = new TransformedBitmap(converted,
                    new ScaleTransform(
                        (double)Width / converted.PixelWidth,
                        (double)Height / converted.PixelHeight));
                converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            }

            var stride = Width * 4;
            var pixels = new byte[stride * Height];
            converted.CopyPixels(pixels, stride, 0);
            Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixels, stride, 0);
            InvalidateThumbnail();
        }

        /// <summary>
        /// Copy pixel data từ BitmapSource vào layer, giữ nguyên tỷ lệ aspect ratio và căn giữa.
        /// Nếu ảnh lớn hơn canvas, thu nhỏ tỷ lệ để vừa với canvas.
        /// </summary>
        public void CopyFromPreserveAspectRatio(BitmapSource source)
        {
            if (source == null) return;

            // Convert sang BGRA32 nếu cần
            var converted = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            int srcW = converted.PixelWidth;
            int srcH = converted.PixelHeight;

            // Tính vị trí căn giữa dựa trên kích thước cũ của layer (thường là kích thước document)
            double x = (Width - srcW) / 2.0;
            double y = (Height - srcH) / 2.0;
            OffsetX = (int)x;
            OffsetY = (int)y;

            // Resize layer to match original image size
            Width = srcW;
            Height = srcH;
            Bitmap = new WriteableBitmap(srcW, srcH, 96, 96, PixelFormats.Bgra32, null);

            var stride = srcW * 4;
            var pixels = new byte[stride * srcH];
            converted.CopyPixels(pixels, stride, 0);

            Bitmap.WritePixels(new Int32Rect(0, 0, srcW, srcH), pixels, stride, 0);
            
            // Đặt OriginalTransformBitmap là ảnh gốc chất lượng cao
            OriginalTransformBitmap = new WriteableBitmap(converted);
            // Đặt ContentBounds cục bộ (0, 0, srcW, srcH)
            ContentBounds = new Rect(x, y, srcW, srcH);
            // ImageContentBounds persistent - không bị clear bởi commands/tools
            ImageContentBounds = ContentBounds;

            InvalidateThumbnail();
        }

        /// <summary>Tạo bản copy đầy đủ của layer (pixel + properties).</summary>
        public EditorLayer Duplicate()
        {
            var copy = new EditorLayer(Width, Height, _name + " copy");
            copy.OffsetX = ParentLayer != null ? ParentLayer.OffsetX : OffsetX;
            copy.OffsetY = ParentLayer != null ? ParentLayer.OffsetY : OffsetY;
            copy.Opacity = _opacity;
            copy.IsVisible = _isVisible;
            copy.BlendMode = _blendMode;
            copy.IsLocked = false; // copy luôn unlocked
            copy.CodeId = Guid.NewGuid().ToString("N");

            copy.IsTextLayer = IsTextLayer;
            copy.TextContent = TextContent;
            copy.TextX = TextX;
            copy.TextY = TextY;
            copy.TextWidth = TextWidth;
            copy.TextHeight = TextHeight;
            copy.TextFontSize = TextFontSize;
            copy.TextColor = TextColor;
            copy.TextFontFamily = TextFontFamily;
            copy.TextFontStyle = TextFontStyle;
            copy.TextAlignment = TextAlignment;
            copy.IsSelected = IsSelected;
            if (OriginalTransformBitmap != null)
            {
                copy.OriginalTransformBitmap = OriginalTransformBitmap;
            }
            copy.ContentBounds = ContentBounds;
            copy.ImageContentBounds = ImageContentBounds;
            copy.LayerScaleX = LayerScaleX;
            copy.LayerScaleY = LayerScaleY;
            copy.LayerAngle = LayerAngle;
            copy.LayerTranslateX = LayerTranslateX;
            copy.LayerTranslateY = LayerTranslateY;

            // Copy Layer AI configurations
            copy.PngBytes = PngBytes;
            copy.LayerAiPrompt = LayerAiPrompt;
            copy.LayerAiBatchSizeIndex = LayerAiBatchSizeIndex;
            copy.LayerAiAspectRatioIndex = LayerAiAspectRatioIndex;
            copy.LayerAiCustomWidth = LayerAiCustomWidth;
            copy.LayerAiCustomHeight = LayerAiCustomHeight;
            copy.LayerAiSecondarySlotCount = LayerAiSecondarySlotCount;
            
            // Copy main layer aspect-ratio IDs
            foreach (var kvp in AspectRatioImageIds)
            {
                copy.AspectRatioImageIds[kvp.Key] = kvp.Value;
            }

            copy.LayerAiSecondaryImages.Clear();
            foreach (var src in LayerAiSecondaryImages)
            {
                var secCopy = new LayerAiSecondaryImage
                {
                    PngBytes = src.PngBytes,
                    FilePath = src.FilePath,
                    IsSelected = src.IsSelected,
                    Bitmap = src.Bitmap
                };
                foreach (var kvp in src.AspectRatioIds)
                {
                    secCopy.AspectRatioIds[kvp.Key] = kvp.Value;
                }
                copy.LayerAiSecondaryImages.Add(secCopy);
            }
            
            if (ParentLayer != null && ParentLayer.ContentGeometry != null)
            {
                copy.ContentGeometry = ParentLayer.ContentGeometry.Clone();
            }
            else if (ContentGeometry != null)
            {
                copy.ContentGeometry = ContentGeometry.Clone();
            }

            var stride = Width * 4;
            // CopyPixels là read-only KHÔNG cần Lock — tránh deadlock với WPF render thread
            byte[] pixelData = new byte[stride * Height];
            Bitmap.CopyPixels(pixelData, stride, 0);
            copy.Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixelData, stride, 0);
            // Chỉ xoá cache, không gọi InvalidateThumbnail (tránh trigger PropertyChanged đồng bộ)
            copy._cachedThumbnail = null;

            // Nhân bản cả ChildLayers (các biến thể AI/radio items)
            foreach (var child in ChildLayers)
            {
                var childCopy = child.Duplicate();
                childCopy.ParentLayer = copy;
                copy.ChildLayers.Add(childCopy);

                if (ActiveChildLayer == child)
                {
                    copy.ActiveChildLayer = childCopy;
                }
            }

            return copy;
        }

        /// <summary>Snapshot 1 vùng pixel (dùng cho undo).</summary>
        public byte[] SnapshotRegion(Int32Rect rect)
        {
            var clipped = ClipRect(rect);
            if (clipped.Width <= 0 || clipped.Height <= 0)
                return Array.Empty<byte>();

            var stride = clipped.Width * 4;
            var pixels = new byte[stride * clipped.Height];
            Bitmap.CopyPixels(clipped, pixels, stride, 0);
            return pixels;
        }

        /// <summary>Restore pixel data vào 1 vùng (dùng cho undo).</summary>
        public void RestoreRegion(Int32Rect rect, byte[] pixels)
        {
            var clipped = ClipRect(rect);
            if (clipped.Width <= 0 || clipped.Height <= 0 || pixels.Length == 0)
                return;

            var stride = clipped.Width * 4;
            Bitmap.WritePixels(clipped, pixels, stride, 0);
            InvalidateThumbnail();
        }

        /// <summary>Tạo thumbnail cho layer list. GPU-accelerated, dùng cached bounds.</summary>
        private BitmapSource GenerateThumbnail(int maxSize = 0)
        {
            // Dùng cached ContentBounds nếu có, tránh quét toàn bộ pixel O(W×H)
            Rect bounds = ContentBounds;
            int bx, by, bw, bh;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
            {
                bx = (int)Math.Max(0, bounds.X);
                by = (int)Math.Max(0, bounds.Y);
                bw = (int)Math.Min(Width - bx, bounds.Width);
                bh = (int)Math.Min(Height - by, bounds.Height);
            }
            else
            {
                bx = 0; by = 0; bw = Width; bh = Height;
            }
            if (bw <= 0 || bh <= 0) { bx = 0; by = 0; bw = Width; bh = Height; }

            // Thumbnail nhỏ để tạo nhanh
            int thumbW, thumbH;
            if (bw >= bh)
            {
                int targetW = maxSize > 0 ? maxSize : 64;
                double scale = (double)targetW / bw;
                thumbW = targetW;
                thumbH = Math.Max(1, (int)(bh * scale));
            }
            else
            {
                int targetH = maxSize > 0 ? maxSize : 48;
                double scale = (double)targetH / bh;
                thumbH = targetH;
                thumbW = Math.Max(1, (int)(bw * scale));
            }

            // GPU-accelerated render thumbnail
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.LowQuality);
            using (var dc = dv.RenderOpen())
            {
                if (IsTextLayer)
                {
                    double scale = (double)thumbH / Height;
                    var fontStyle = TextFontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;
                    var fontWeight = TextFontStyle == "Bold" ? FontWeights.Bold : FontWeights.Normal;
                    var typeface = new Typeface(new FontFamily(TextFontFamily), fontStyle, fontWeight, FontStretches.Normal);
                    
                    var formattedText = new FormattedText(
                        TextContent ?? "",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        Math.Max(1, TextFontSize * scale),
                        new SolidColorBrush(TextColor),
                        1.0
                    );

                    formattedText.MaxTextWidth = Math.Max(1, TextWidth * scale);
                    formattedText.MaxTextHeight = Math.Max(1, TextHeight * scale);
                    formattedText.TextAlignment = TextAlignment == "Center" ? System.Windows.TextAlignment.Center :
                                                  TextAlignment == "Right" ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left;

                    dc.DrawText(formattedText, new Point(
                        (TextX - bx) * (double)thumbW / bw,
                        (TextY - by) * (double)thumbH / bh
                    ));
                }
                else
                {
                    dc.DrawImage(Bitmap, new Rect(
                        -bx * (double)thumbW / bw,
                        -by * (double)thumbH / bh,
                        Width * (double)thumbW / bw,
                        Height * (double)thumbH / bh));
                }
            }
            var rtb = new RenderTargetBitmap(thumbW, thumbH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>Cached thumbnail for XAML binding. Returns a frozen copy (new object on each invalidation).</summary>
        private BitmapSource? _cachedThumbnail;
        public BitmapSource Thumbnail
        {
            get
            {
                _cachedThumbnail ??= GenerateThumbnail();
                return _cachedThumbnail;
            }
        }

        /// <summary>Backward compat: same as Thumbnail.</summary>
        public BitmapSource GetThumbnail(int maxSize = 48) => GenerateThumbnail(maxSize);

        /// <summary>Đánh dấu thumbnail cần rebuild (gọi sau khi pixel thay đổi).</summary>
        public void InvalidateThumbnail()
        {
            _cachedThumbnail = null;
            _thumbnailCache = null;
            // Defer PropertyChanged sang DispatcherPriority.Background để không block UI
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    OnPropertyChanged(nameof(Bitmap));
                    OnPropertyChanged(nameof(Thumbnail));
                }));
            }
            else
            {
                OnPropertyChanged(nameof(Bitmap));
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        private Int32Rect ClipRect(Int32Rect rect)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int right = Math.Min(Width, rect.X + rect.Width);
            int bottom = Math.Min(Height, rect.Y + rect.Height);
            return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }

        private bool _isTextLayer;
        private string _textContent = "";
        private double _textX;
        private double _textY;
        private double _textWidth = 200;
        private double _textHeight = 100;
        private double _textFontSize = 24;
        private Color _textColor = Colors.White;
        private string _textFontFamily = "Arial";
        private string _textFontStyle = "Bold";
        private string _textAlignment = "Left";
        private bool _isEditingName;
        private bool _isSelected;

        public double TempMoveDx { get; set; }
        public double TempMoveDy { get; set; }
        public Geometry? TempSelectionGeometry { get; set; }
        public SkiaSharp.SKPath? TempSelectionPath { get; set; }

        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
        public bool IsEditingName { get => _isEditingName; set => SetField(ref _isEditingName, value); }
        public bool IsTextLayer { get => _isTextLayer; set => SetField(ref _isTextLayer, value); }
        public string TextContent { get => _textContent; set => SetField(ref _textContent, value ?? ""); }
        public double TextX { get => _textX; set => SetField(ref _textX, value); }
        public double TextY { get => _textY; set => SetField(ref _textY, value); }
        public double TextWidth { get => _textWidth; set => SetField(ref _textWidth, value); }
        public double TextHeight { get => _textHeight; set => SetField(ref _textHeight, value); }
        public double TextFontSize { get => _textFontSize; set => SetField(ref _textFontSize, value); }
        public Color TextColor { get => _textColor; set => SetField(ref _textColor, value); }
        public string TextFontFamily { get => _textFontFamily; set => SetField(ref _textFontFamily, value ?? "Arial"); }
        public string TextFontStyle { get => _textFontStyle; set => SetField(ref _textFontStyle, value ?? "Bold"); }
        public string TextAlignment { get => _textAlignment; set => SetField(ref _textAlignment, value ?? "Left"); }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
        #endregion
        /// <summary>Áp dụng xoay/lật đối với Bitmap của layer.</summary>
        public void ApplyTransform(Transform transform)
        {
            var transformed = new TransformedBitmap(Bitmap, transform);
            var converted = new FormatConvertedBitmap(transformed, PixelFormats.Bgra32, null, 0);
            
            Width = converted.PixelWidth;
            Height = converted.PixelHeight;
            Bitmap = new WriteableBitmap(converted);
            InvalidateThumbnail();
        }

            // Layer AI settings specific to this layer
        public byte[]? PngBytes { get; set; }
        public string LayerAiPrompt { get; set; } = string.Empty;
        public int LayerAiBatchSizeIndex { get; set; } = 2; // Default size index (usually 3)
        public int LayerAiAspectRatioIndex { get; set; } = 3; // Default ratio (1:1)
        public string LayerAiCustomWidth { get; set; } = string.Empty;
        public string LayerAiCustomHeight { get; set; } = string.Empty;
        public int LayerAiSecondarySlotCount { get; set; } = 4;

        /// <summary>Bảng lưu trữ ID tương ứng với từng tỉ lệ ảnh (Key = AspectRatioIndex [0..6]).</summary>
        public System.Collections.Generic.Dictionary<int, string> AspectRatioImageIds { get; } = new System.Collections.Generic.Dictionary<int, string>();

        public string? GetImageId(int aspectIndex)
        {
            if (AspectRatioImageIds.TryGetValue(aspectIndex, out var id) && !string.IsNullOrWhiteSpace(id))
                return id;
            return null;
        }

        public void SetImageId(int aspectIndex, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                AspectRatioImageIds.Remove(aspectIndex);
            }
            else
            {
                AspectRatioImageIds[aspectIndex] = id.Trim();
            }
            OnPropertyChanged(nameof(CurrentImageId));
            OnPropertyChanged(nameof(HasImageId));
            OnPropertyChanged(nameof(ShortImageId));
        }

        /// <summary>ID của ảnh ở tỉ lệ hiện tại.</summary>
        public string? CurrentImageId => GetImageId(LayerAiAspectRatioIndex);
        public bool HasImageId => !string.IsNullOrEmpty(CurrentImageId);

        /// <summary>Hiển thị ID vắn tắt trên UI (ví dụ `#ID: 8a7f9b...`).</summary>
        public string ShortImageId
        {
            get
            {
                var id = CurrentImageId;
                if (string.IsNullOrEmpty(id)) return string.Empty;
                return id.Length > 10 ? $"#ID: {id.Substring(0, 8)}…" : $"#ID: {id}";
            }
        }

        public class LayerAiSecondaryImage
        {
            public string? ImageId { get; set; }
            public byte[]? PngBytes { get; set; }
            public string? FilePath { get; set; }
            public bool IsSelected { get; set; } = true;
            public BitmapSource? Bitmap { get; set; }
            public System.Collections.Generic.Dictionary<int, string> AspectRatioIds { get; } = new System.Collections.Generic.Dictionary<int, string>();
            public System.Collections.Generic.List<LayerAiSecondaryImage> SavedChildImages { get; } = new System.Collections.Generic.List<LayerAiSecondaryImage>();

            public string? GetImageId(int aspectIndex = 0)
            {
                if (!string.IsNullOrWhiteSpace(ImageId)) return ImageId;
                if (AspectRatioIds.TryGetValue(aspectIndex, out var id) && !string.IsNullOrWhiteSpace(id))
                    return id;
                return null;
            }

            public void SetImageId(int aspectIndex, string? id)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    ImageId = null;
                    AspectRatioIds.Remove(aspectIndex);
                }
                else
                {
                    ImageId = id.Trim();
                    AspectRatioIds[aspectIndex] = id.Trim();
                }
            }
        }

        public List<LayerAiSecondaryImage> LayerAiSecondaryImages { get; } = new List<LayerAiSecondaryImage>
        {
            new LayerAiSecondaryImage(),
            new LayerAiSecondaryImage(),
            new LayerAiSecondaryImage(),
            new LayerAiSecondaryImage()
        };
    }
}
