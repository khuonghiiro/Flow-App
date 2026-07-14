using System;
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
        private WriteableBitmap? _thumbnailCache;

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
        public string Id { get; internal set; }

        /// <summary>Kích thước layer (luôn bằng document).</summary>
        public int Width { get; internal set; }
        public int Height { get; internal set; }

        /// <summary>Bitmap pixel data (BGRA32, cùng kích thước document).</summary>
        public WriteableBitmap Bitmap { get; internal set; }

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

        private WriteableBitmap? _originalTransformBitmap;
        public WriteableBitmap? OriginalTransformBitmap
        {
            get => _originalTransformBitmap;
            set
            {
                _originalTransformBitmap = value;
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
                    ContentBounds = CalculateContentBounds(value);
                }
            }
        }

        public Rect ContentBounds { get; set; } = Rect.Empty;
        public Geometry? ContentGeometry { get; set; }

        public static Rect CalculateContentBounds(WriteableBitmap bitmap)
        {
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
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
            var stride = Width * 4;
            var pixels = new byte[stride * Height];
            Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixels, stride, 0);
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

            double srcW = converted.PixelWidth;
            double srcH = converted.PixelHeight;

            // Tính vị trí căn giữa trên Canvas
            double x = (Width - srcW) / 2.0;
            double y = (Height - srcH) / 2.0;

            // Render ảnh gốc vào layer's Bitmap (kích thước Document, sẽ bị clip phần thừa ngoài canvas)
            var drawingVisual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Vẽ ảnh nguồn ở đúng kích thước gốc và căn giữa
                drawingContext.DrawImage(converted, new Rect(x, y, srcW, srcH));
            }

            // Render ra RenderTargetBitmap
            var rtb = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            // Chuyển sang format Bgra32 để ghi vào layer's Bitmap
            var finalBmp = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);

            var stride = Width * 4;
            var pixels = new byte[stride * Height];
            finalBmp.CopyPixels(pixels, stride, 0);

            Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixels, stride, 0);
            
            // Đặt OriginalTransformBitmap là ảnh gốc chất lượng cao và kích thước nguyên bản (không bị scale hay clip!)
            OriginalTransformBitmap = new WriteableBitmap(converted);
            // Đặt ContentBounds bằng kích thước gốc căn giữa
            ContentBounds = new Rect(x, y, srcW, srcH);

            InvalidateThumbnail();
        }

        /// <summary>Tạo bản copy đầy đủ của layer (pixel + properties).</summary>
        public EditorLayer Duplicate()
        {
            var copy = new EditorLayer(Width, Height, _name + " copy");
            copy.Opacity = _opacity;
            copy.IsVisible = _isVisible;
            copy.BlendMode = _blendMode;
            copy.IsLocked = false; // copy luôn unlocked

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
            copy.IsSelected = IsSelected;
            copy.ContentBounds = ContentBounds;
            if (OriginalTransformBitmap != null)
            {
                // Gán trực tiếp field để bỏ qua setter (chia sẻ chung bitmap để không tốn thêm 16MB)
                copy._originalTransformBitmap = OriginalTransformBitmap;
            }
            copy.LayerScaleX = LayerScaleX;
            copy.LayerScaleY = LayerScaleY;
            copy.LayerAngle = LayerAngle;
            copy.LayerTranslateX = LayerTranslateX;
            copy.LayerTranslateY = LayerTranslateY;
            if (ContentGeometry != null)
            {
                copy.ContentGeometry = ContentGeometry.Clone();
            }

            var stride = Width * 4;
            Bitmap.Lock();
            copy.Bitmap.Lock();
            try
            {
                unsafe
                {
                    System.Buffer.MemoryCopy(
                        (void*)Bitmap.BackBuffer,
                        (void*)copy.Bitmap.BackBuffer,
                        stride * Height,
                        stride * Height);
                }
                copy.Bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
            }
            finally
            {
                copy.Bitmap.Unlock();
                Bitmap.Unlock();
            }
            // Chỉ xoá cache, không gọi InvalidateThumbnail (tránh trigger PropertyChanged đồng bộ)
            copy._cachedThumbnail = null;
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
                dc.DrawImage(Bitmap, new Rect(
                    -bx * (double)thumbW / bw,
                    -by * (double)thumbH / bh,
                    Width * (double)thumbW / bw,
                    Height * (double)thumbH / bh));
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
        private bool _isEditingName;
        private bool _isSelected;

        public double TempMoveDx { get; set; }
        public double TempMoveDy { get; set; }
        public Geometry? TempSelectionGeometry { get; set; }

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
    }
}
