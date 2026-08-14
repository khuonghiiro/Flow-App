using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Undo command cho Color Adjust (Brightness/Contrast/Saturation/Hue) áp dụng lên layer.
    /// Lưu snapshot pixel cũ và mới để undo/redo.
    /// </summary>
    public sealed class ColorAdjustCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly byte[] _oldPixels;
        private readonly byte[] _newPixels;

        public ColorAdjustCommand(EditorLayer layer, byte[] oldPixels, byte[] newPixels)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldPixels = oldPixels ?? throw new ArgumentNullException(nameof(oldPixels));
            _newPixels = newPixels ?? throw new ArgumentNullException(nameof(newPixels));
            Description = "Color Adjust";
        }

        public string Description { get; }

        public void Execute()
        {
            int stride = _layer.Width * 4;
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _newPixels, stride, 0);
            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            int stride = _layer.Width * 4;
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _oldPixels, stride, 0);
            _layer.InvalidateThumbnail();
        }
    }
}
