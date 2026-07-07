using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command cho việc sửa đổi pixel (vẽ/xoá/tô màu/biến đổi) trên layer.</summary>
    public sealed class PixelEditCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly byte[] _oldPixels;
        private readonly byte[] _newPixels;
        private readonly int _stride;

        public bool KeepOriginalTransformBitmap { get; set; } = false;

        // Transform state tracking
        private readonly double _oldScaleX, _oldScaleY, _oldAngle, _oldTranslateX, _oldTranslateY;
        private double _newScaleX = 1.0, _newScaleY = 1.0, _newAngle = 0.0, _newTranslateX = 0.0, _newTranslateY = 0.0;
        private readonly WriteableBitmap? _oldOriginalTransformBitmap;
        private WriteableBitmap? _newOriginalTransformBitmap;

        public PixelEditCommand(EditorLayer layer, byte[] oldPixels, byte[] newPixels)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldPixels = oldPixels ?? throw new ArgumentNullException(nameof(oldPixels));
            _newPixels = newPixels ?? throw new ArgumentNullException(nameof(newPixels));
            _stride = _layer.Width * 4;

            // Capture old transform state
            _oldScaleX = _layer.LayerScaleX;
            _oldScaleY = _layer.LayerScaleY;
            _oldAngle = _layer.LayerAngle;
            _oldTranslateX = _layer.LayerTranslateX;
            _oldTranslateY = _layer.LayerTranslateY;
            _oldOriginalTransformBitmap = _layer.OriginalTransformBitmap;
        }

        public PixelEditCommand(EditorLayer layer, byte[] oldPixels, byte[] newPixels,
                                double oldScaleX, double oldScaleY, double oldAngle,
                                double oldTranslateX, double oldTranslateY, WriteableBitmap? oldOrig)
            : this(layer, oldPixels, newPixels)
        {
            _oldScaleX = oldScaleX;
            _oldScaleY = oldScaleY;
            _oldAngle = oldAngle;
            _oldTranslateX = oldTranslateX;
            _oldTranslateY = oldTranslateY;
            _oldOriginalTransformBitmap = oldOrig;
        }

        public void CaptureNewTransformState()
        {
            _newScaleX = _layer.LayerScaleX;
            _newScaleY = _layer.LayerScaleY;
            _newAngle = _layer.LayerAngle;
            _newTranslateX = _layer.LayerTranslateX;
            _newTranslateY = _layer.LayerTranslateY;
            _newOriginalTransformBitmap = _layer.OriginalTransformBitmap;
        }

        public string Description => "Paint/Erase/Transform";

        public void Execute()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _newPixels, _stride, 0);
            
            if (KeepOriginalTransformBitmap)
            {
                _layer.LayerScaleX = _newScaleX;
                _layer.LayerScaleY = _newScaleY;
                _layer.LayerAngle = _newAngle;
                _layer.LayerTranslateX = _newTranslateX;
                _layer.LayerTranslateY = _newTranslateY;
                _layer.OriginalTransformBitmap = _newOriginalTransformBitmap;
            }
            else
            {
                _layer.OriginalTransformBitmap = null;
            }
            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _oldPixels, _stride, 0);
            
            if (KeepOriginalTransformBitmap)
            {
                _layer.LayerScaleX = _oldScaleX;
                _layer.LayerScaleY = _oldScaleY;
                _layer.LayerAngle = _oldAngle;
                _layer.LayerTranslateX = _oldTranslateX;
                _layer.LayerTranslateY = _oldTranslateY;
                _layer.OriginalTransformBitmap = _oldOriginalTransformBitmap;
            }
            else
            {
                _layer.OriginalTransformBitmap = null;
            }
            _layer.InvalidateThumbnail();
        }
    }
}
