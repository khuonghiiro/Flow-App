using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public string Id { get; }

        /// <summary>Kích thước layer (luôn bằng document).</summary>
        public int Width { get; }
        public int Height { get; }

        /// <summary>Bitmap pixel data (BGRA32, cùng kích thước document).</summary>
        public WriteableBitmap Bitmap { get; }

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

        /// <summary>Khoá layer (không cho vẽ/sửa).</summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => SetField(ref _isLocked, value);
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

        /// <summary>Tạo thumbnail nhỏ cho hiển thị trong layer list.</summary>
        public BitmapSource GetThumbnail(int maxSize = 48)
        {
            if (_thumbnailCache != null)
                return _thumbnailCache;

            double scale = Math.Min((double)maxSize / Width, (double)maxSize / Height);
            int thumbW = Math.Max(1, (int)(Width * scale));
            int thumbH = Math.Max(1, (int)(Height * scale));

            var thumb = new WriteableBitmap(thumbW, thumbH, 96, 96, PixelFormats.Bgra32, null);
            // Simple nearest-neighbor downsample
            var srcStride = Width * 4;
            var srcPixels = new byte[srcStride * Height];
            Bitmap.CopyPixels(srcPixels, srcStride, 0);

            var dstStride = thumbW * 4;
            var dstPixels = new byte[dstStride * thumbH];

            for (int y = 0; y < thumbH; y++)
            {
                int srcY = (int)(y / scale);
                if (srcY >= Height) srcY = Height - 1;
                for (int x = 0; x < thumbW; x++)
                {
                    int srcX = (int)(x / scale);
                    if (srcX >= Width) srcX = Width - 1;

                    int srcIdx = srcY * srcStride + srcX * 4;
                    int dstIdx = y * dstStride + x * 4;
                    dstPixels[dstIdx] = srcPixels[srcIdx];
                    dstPixels[dstIdx + 1] = srcPixels[srcIdx + 1];
                    dstPixels[dstIdx + 2] = srcPixels[srcIdx + 2];
                    dstPixels[dstIdx + 3] = srcPixels[srcIdx + 3];
                }
            }

            thumb.WritePixels(new Int32Rect(0, 0, thumbW, thumbH), dstPixels, dstStride, 0);
            _thumbnailCache = thumb;
            thumb.Freeze();
            return thumb;
        }

        /// <summary>Đánh dấu thumbnail cần rebuild (gọi sau khi pixel thay đổi).</summary>
        public void InvalidateThumbnail()
        {
            _thumbnailCache = null;
            OnPropertyChanged(nameof(Bitmap));
        }

        private Int32Rect ClipRect(Int32Rect rect)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int right = Math.Min(Width, rect.X + rect.Width);
            int bottom = Math.Min(Height, rect.Y + rect.Height);
            return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
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
    }
}
