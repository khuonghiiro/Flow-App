// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command cho hiệu ứng MagickEffects áp dụng lên layer.</summary>
    public sealed class EffectApplyCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly byte[] _oldPixels;
        private readonly WriteableBitmap _newBitmap;
        private byte[]? _newPixels;

        public EffectApplyCommand(EditorLayer layer, WriteableBitmap oldSnapshot, WriteableBitmap newBitmap, string effectName)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _newBitmap = newBitmap ?? throw new ArgumentNullException(nameof(newBitmap));
            Description = effectName;

            // Snapshot old pixels
            int stride = layer.Width * 4;
            _oldPixels = new byte[stride * layer.Height];
            oldSnapshot.CopyPixels(_oldPixels, stride, 0);
        }

        public string Description { get; }

        public void Execute()
        {
            // Snapshot new pixels lazily
            if (_newPixels == null)
            {
                int stride = _layer.Width * 4;
                _newPixels = new byte[stride * _layer.Height];
                _newBitmap.CopyPixels(_newPixels, stride, 0);
            }

            int s = _layer.Width * 4;
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _newPixels, s, 0);
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
