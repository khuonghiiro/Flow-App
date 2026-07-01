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
            var bgLayer = new EditorLayer(doc.Width, doc.Height, "Background");
            bgLayer.CopyFrom(source);
            bgLayer.IsLocked = false;

            doc.Layers.Add(bgLayer);
            doc.ActiveLayer = bgLayer;
            return doc;
        }

        /// <summary>
        /// Tạo document trống (canvas trắng).
        /// </summary>
        public static EditorDocument CreateBlank(int width, int height)
        {
            var doc = new EditorDocument(width, height);
            var bgLayer = new EditorLayer(width, height, "Background");
            bgLayer.Fill(Colors.White);

            doc.Layers.Add(bgLayer);
            doc.ActiveLayer = bgLayer;
            return doc;
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
                name = $"Layer {Layers.Count + 1}";

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

        /// <summary>
        /// Composite tất cả visible layers → 1 WriteableBitmap.
        /// Dùng alpha blending đơn giản (Normal blend mode).
        /// </summary>
        public WriteableBitmap Composite()
        {
            var result = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
            var stride = Width * 4;
            var resultPixels = new byte[stride * Height];

            // Fill transparent
            Array.Clear(resultPixels, 0, resultPixels.Length);

            foreach (var layer in Layers)
            {
                if (!layer.IsVisible || layer.Opacity <= 0) continue;

                var layerPixels = new byte[stride * Height];
                layer.Bitmap.CopyPixels(layerPixels, stride, 0);

                double layerOpacity = layer.Opacity;
                BlendLayerPixels(resultPixels, layerPixels, layerOpacity, layer.BlendMode);
            }

            result.WritePixels(new Int32Rect(0, 0, Width, Height), resultPixels, stride, 0);
            return result;
        }

        /// <summary>Flatten tất cả layers → 1 frozen BitmapSource.</summary>
        public BitmapSource Flatten()
        {
            var result = Composite();
            result.Freeze();
            return result;
        }

        private static void BlendLayerPixels(byte[] dst, byte[] src, double opacity, BlendMode mode)
        {
            // Hot path — pixel loop (optimized cho Normal mode)
            int len = dst.Length;
            if (len != src.Length) return;

            for (int i = 0; i < len; i += 4)
            {
                byte sB = src[i], sG = src[i + 1], sR = src[i + 2], sA = src[i + 3];
                if (sA == 0) continue;

                // Apply layer opacity
                double srcAlpha = (sA / 255.0) * opacity;
                if (srcAlpha <= 0) continue;

                byte dB = dst[i], dG = dst[i + 1], dR = dst[i + 2], dA = dst[i + 3];
                double dstAlpha = dA / 255.0;

                // Blend theo mode (tính blended RGB trước, rồi alpha composite)
                double bR, bG, bB;
                switch (mode)
                {
                    case BlendMode.Multiply:
                        bR = (sR / 255.0) * (dR / 255.0) * 255;
                        bG = (sG / 255.0) * (dG / 255.0) * 255;
                        bB = (sB / 255.0) * (dB / 255.0) * 255;
                        break;
                    case BlendMode.Screen:
                        bR = (1 - (1 - sR / 255.0) * (1 - dR / 255.0)) * 255;
                        bG = (1 - (1 - sG / 255.0) * (1 - dG / 255.0)) * 255;
                        bB = (1 - (1 - sB / 255.0) * (1 - dB / 255.0)) * 255;
                        break;
                    case BlendMode.Overlay:
                        bR = OverlayChannel(dR, sR);
                        bG = OverlayChannel(dG, sG);
                        bB = OverlayChannel(dB, sB);
                        break;
                    case BlendMode.Darken:
                        bR = Math.Min(sR, dR);
                        bG = Math.Min(sG, dG);
                        bB = Math.Min(sB, dB);
                        break;
                    case BlendMode.Lighten:
                        bR = Math.Max(sR, dR);
                        bG = Math.Max(sG, dG);
                        bB = Math.Max(sB, dB);
                        break;
                    default: // Normal
                        bR = sR; bG = sG; bB = sB;
                        break;
                }

                // Porter-Duff "over" composite
                double outAlpha = srcAlpha + dstAlpha * (1 - srcAlpha);
                if (outAlpha > 0)
                {
                    dst[i + 2] = ClampByte((bR * srcAlpha + dR * dstAlpha * (1 - srcAlpha)) / outAlpha);
                    dst[i + 1] = ClampByte((bG * srcAlpha + dG * dstAlpha * (1 - srcAlpha)) / outAlpha);
                    dst[i] = ClampByte((bB * srcAlpha + dB * dstAlpha * (1 - srcAlpha)) / outAlpha);
                    dst[i + 3] = ClampByte(outAlpha * 255);
                }
            }
        }

        private static double OverlayChannel(byte dstByte, byte srcByte)
        {
            double d = dstByte / 255.0, s = srcByte / 255.0;
            return (d < 0.5 ? 2 * d * s : 1 - 2 * (1 - d) * (1 - s)) * 255;
        }

        private static byte ClampByte(double val)
            => (byte)Math.Clamp((int)(val + 0.5), 0, 255);

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
