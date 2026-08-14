using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Command hỗ trợ hoàn tác/chạy lại việc Cắt (Crop) toàn bộ document.
    /// </summary>
    public sealed class CropDocumentCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly int _oldWidth;
        private readonly int _oldHeight;
        private readonly int _newWidth;
        private readonly int _newHeight;
        private readonly List<(EditorLayer layer, byte[] pixels, int w, int h)> _oldState;
        private readonly List<(EditorLayer layer, byte[] pixels, int w, int h)> _newState;

        public CropDocumentCommand(
            EditorDocument doc, 
            int oldW, int oldH, 
            int newW, int newH, 
            List<(EditorLayer layer, byte[] pixels, int w, int h)> oldState,
            List<(EditorLayer layer, byte[] pixels, int w, int h)> newState)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _oldWidth = oldW;
            _oldHeight = oldH;
            _newWidth = newW;
            _newHeight = newH;
            _oldState = oldState ?? throw new ArgumentNullException(nameof(oldState));
            _newState = newState ?? throw new ArgumentNullException(nameof(newState));
        }

        public string Description => "Crop Canvas";

        public static CropDocumentCommand Create(EditorDocument doc, int cx, int cy, int cw, int ch)
        {
            var oldState = new List<(EditorLayer layer, byte[] pixels, int w, int h)>();
            var newState = new List<(EditorLayer layer, byte[] pixels, int w, int h)>();

            foreach (var layer in doc.Layers)
            {
                int oldW = layer.Width;
                int oldH = layer.Height;
                int oldStride = oldW * 4;
                byte[] oldPixels = new byte[oldStride * oldH];
                layer.Bitmap.CopyPixels(oldPixels, oldStride, 0);
                oldState.Add((layer, oldPixels, oldW, oldH));

                int newStride = cw * 4;
                byte[] newPixels = new byte[newStride * ch];

                for (int y = 0; y < ch; y++)
                {
                    int srcY = cy + y;
                    if (srcY >= 0 && srcY < oldH)
                    {
                        int srcIdx = (srcY * oldW + cx) * 4;
                        int dstIdx = y * cw * 4;
                        for (int x = 0; x < cw; x++)
                        {
                            int srcX = cx + x;
                            int dstPixelIdx = dstIdx + x * 4;
                            if (srcX >= 0 && srcX < oldW)
                            {
                                int srcPixelIdx = srcIdx + x * 4;
                                newPixels[dstPixelIdx] = oldPixels[srcPixelIdx];
                                newPixels[dstPixelIdx + 1] = oldPixels[srcPixelIdx + 1];
                                newPixels[dstPixelIdx + 2] = oldPixels[srcPixelIdx + 2];
                                newPixels[dstPixelIdx + 3] = oldPixels[srcPixelIdx + 3];
                            }
                            else
                            {
                                newPixels[dstPixelIdx + 3] = 0;
                            }
                        }
                    }
                }
                newState.Add((layer, newPixels, cw, ch));
            }

            return new CropDocumentCommand(doc, doc.Width, doc.Height, cw, ch, oldState, newState);
        }

        public void Execute()
        {
            _doc.Width = _newWidth;
            _doc.Height = _newHeight;

            foreach (var item in _newState)
            {
                item.layer.Width = item.w;
                item.layer.Height = item.h;
                
                var bmp = new WriteableBitmap(item.w, item.h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                bmp.WritePixels(new System.Windows.Int32Rect(0, 0, item.w, item.h), item.pixels, item.w * 4, 0);
                item.layer.Bitmap = bmp;
                item.layer.InvalidateThumbnail();
            }

            _doc.RaisePropertyChanged(nameof(EditorDocument.Width));
            _doc.RaisePropertyChanged(nameof(EditorDocument.Height));
        }

        public void Undo()
        {
            _doc.Width = _oldWidth;
            _doc.Height = _oldHeight;

            foreach (var item in _oldState)
            {
                item.layer.Width = item.w;
                item.layer.Height = item.h;

                var bmp = new WriteableBitmap(item.w, item.h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                bmp.WritePixels(new System.Windows.Int32Rect(0, 0, item.w, item.h), item.pixels, item.w * 4, 0);
                item.layer.Bitmap = bmp;
                item.layer.InvalidateThumbnail();
            }

            _doc.RaisePropertyChanged(nameof(EditorDocument.Width));
            _doc.RaisePropertyChanged(nameof(EditorDocument.Height));
        }
    }
}
