using System;
using System.Windows;
using System.Windows.Media;
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

        private readonly Geometry? _oldContentGeometry;

        private readonly int _oldOffsetX, _oldOffsetY;
        private int _newOffsetX, _newOffsetY;

        private readonly Rect _oldContentBounds;
        private Rect _newContentBounds;

        public Rect OldContentBounds { get => _oldContentBounds; set => _newContentBounds = value; } // backward-compat or helper
        public Rect CustomOldContentBounds { get; set; }
        public Rect CustomNewContentBounds { get; set; }
        public bool HasCustomBounds { get; set; } = false;

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
            _oldContentBounds = _layer.ContentBounds;
            _newContentBounds = _layer.ContentBounds;
            _oldContentGeometry = _layer.ContentGeometry;

            _oldOffsetX = _layer.OffsetX;
            _oldOffsetY = _layer.OffsetY;
            _newOffsetX = _layer.OffsetX;
            _newOffsetY = _layer.OffsetY;
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
            _oldContentGeometry = layer.ContentGeometry;
        }

        public void CaptureNewTransformState()
        {
            _newScaleX = _layer.LayerScaleX;
            _newScaleY = _layer.LayerScaleY;
            _newAngle = _layer.LayerAngle;
            _newTranslateX = _layer.LayerTranslateX;
            _newTranslateY = _layer.LayerTranslateY;
            _newOriginalTransformBitmap = _layer.OriginalTransformBitmap;
            _newContentBounds = _layer.ContentBounds;

            _newOffsetX = _layer.OffsetX;
            _newOffsetY = _layer.OffsetY;
        }

        public string Description => "Paint/Erase/Transform";

        public void Execute()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _newPixels, _stride, 0);
            _layer.PngBytes = null;
            
            if (KeepOriginalTransformBitmap)
            {
                _layer.LayerScaleX = _newScaleX;
                _layer.LayerScaleY = _newScaleY;
                _layer.LayerAngle = _newAngle;
                _layer.LayerTranslateX = _newTranslateX;
                _layer.LayerTranslateY = _newTranslateY;
                _layer.OriginalTransformBitmap = _newOriginalTransformBitmap;
                _layer.ContentBounds = HasCustomBounds ? CustomNewContentBounds : _newContentBounds;
            }
            else
            {
                _layer.OriginalTransformBitmap = null;
                _layer.ContentGeometry = null;
            }

            _layer.OffsetX = _newOffsetX;
            _layer.OffsetY = _newOffsetY;

            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            _layer.Bitmap.WritePixels(new Int32Rect(0, 0, _layer.Width, _layer.Height), _oldPixels, _stride, 0);
            _layer.PngBytes = null;
            
            if (KeepOriginalTransformBitmap || _oldOriginalTransformBitmap != null)
            {
                _layer.LayerScaleX = _oldScaleX;
                _layer.LayerScaleY = _oldScaleY;
                _layer.LayerAngle = _oldAngle;
                _layer.LayerTranslateX = _oldTranslateX;
                _layer.LayerTranslateY = _oldTranslateY;
                _layer.OriginalTransformBitmap = _oldOriginalTransformBitmap;
                _layer.ContentBounds = HasCustomBounds ? CustomOldContentBounds : _oldContentBounds;
            }
            else
            {
                _layer.OriginalTransformBitmap = null;
            }

            _layer.ContentGeometry = _oldContentGeometry;

            _layer.OffsetX = _oldOffsetX;
            _layer.OffsetY = _oldOffsetY;

            _layer.InvalidateThumbnail();
        }
    }
}
