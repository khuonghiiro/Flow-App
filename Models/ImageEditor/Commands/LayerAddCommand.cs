using System;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command: thêm 1 layer mới vào document.</summary>
    public sealed class LayerAddCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly EditorLayer _layer;
        private readonly int _insertIndex;
        private readonly EditorLayer? _previousActiveLayer;

        public LayerAddCommand(EditorDocument doc, EditorLayer layer, int insertIndex)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _insertIndex = insertIndex;
            _previousActiveLayer = doc.ActiveLayer;
        }

        public string Description => $"Add layer \"{_layer.Name}\"";

        public void Execute()
        {
            int idx = Math.Clamp(_insertIndex, 0, _doc.Layers.Count);
            _doc.Layers.Insert(idx, _layer);
            _doc.ActiveLayer = _layer;
        }

        public void Undo()
        {
            _doc.Layers.Remove(_layer);
            _doc.ActiveLayer = _previousActiveLayer ?? (_doc.Layers.Count > 0 ? _doc.Layers[^1] : null);
        }
    }
}
