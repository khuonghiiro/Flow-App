// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Command hỗ trợ hoàn tác/chạy lại việc xoay/lật toàn bộ document (và tất cả các layer của nó).
    /// </summary>
    public sealed class TransformDocumentCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly int _oldWidth;
        private readonly int _oldHeight;
        private readonly int _newWidth;
        private readonly int _newHeight;
        private readonly List<(EditorLayer layer, byte[] pixels, int w, int h)> _oldState;
        private readonly List<(EditorLayer layer, byte[] pixels, int w, int h)> _newState;

        public TransformDocumentCommand(
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

        public string Description => "Transform Canvas";

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
