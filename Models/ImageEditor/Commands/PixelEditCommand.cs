using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command cho việc sửa đổi pixel (vẽ/xoá/tô màu) trên layer.</summary>
    public sealed class PixelEditCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly byte[] _oldPixels;
        private readonly byte[] _newPixels;
        private readonly int _stride;

        public PixelEditCommand(EditorLayer layer, byte[] oldPixels, byte[] newPixels)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldPixels = oldPixels ?? throw new ArgumentNullException(nameof(oldPixels));
            _newPixels = newPixels ?? throw new ArgumentNullException(nameof(newPixels));
            _stride = _layer.Width * 4;
        }

        public string Description => "Paint/Erase";

        public void Execute()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _newPixels, _stride, 0);
            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _oldPixels, _stride, 0);
            _layer.InvalidateThumbnail();
        }
    }
}
