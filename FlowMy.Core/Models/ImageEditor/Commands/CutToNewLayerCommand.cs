using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Undo/redo command for cutting a selection to a new layer.
    /// </summary>
    public sealed class CutToNewLayerCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly EditorLayer _originalLayer;
        private readonly byte[] _oldPixels;
        private readonly byte[] _newPixels;
        private readonly EditorLayer _newLayer;
        private readonly int _insertIndex;
        private readonly EditorLayer? _previousActiveLayer;

        public CutToNewLayerCommand(
            EditorDocument doc, 
            EditorLayer originalLayer, 
            byte[] oldPixels, 
            byte[] newPixels, 
            EditorLayer newLayer, 
            int insertIndex)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _originalLayer = originalLayer ?? throw new ArgumentNullException(nameof(originalLayer));
            _oldPixels = oldPixels ?? throw new ArgumentNullException(nameof(oldPixels));
            _newPixels = newPixels ?? throw new ArgumentNullException(nameof(newPixels));
            _newLayer = newLayer ?? throw new ArgumentNullException(nameof(newLayer));
            _insertIndex = insertIndex;
            _previousActiveLayer = originalLayer;
        }

        public string Description => $"Cut Selection to \"{_newLayer.Name}\"";

        public void Execute()
        {
            int stride = _originalLayer.Width * 4;
            _originalLayer.Bitmap.WritePixels(new Int32Rect(0, 0, _originalLayer.Width, _originalLayer.Height), _newPixels, stride, 0);
            _originalLayer.InvalidateThumbnail();

            int idx = Math.Clamp(_insertIndex, 0, _doc.Layers.Count);
            if (!_doc.Layers.Contains(_newLayer))
            {
                _doc.Layers.Insert(idx, _newLayer);
            }
            _doc.ActiveLayer = _newLayer;
        }

        public void Undo()
        {
            _doc.Layers.Remove(_newLayer);

            int stride = _originalLayer.Width * 4;
            _originalLayer.Bitmap.WritePixels(new Int32Rect(0, 0, _originalLayer.Width, _originalLayer.Height), _oldPixels, stride, 0);
            _originalLayer.InvalidateThumbnail();

            _doc.ActiveLayer = _previousActiveLayer ?? (_doc.Layers.Count > 0 ? _doc.Layers[^1] : null);
        }
    }
}
