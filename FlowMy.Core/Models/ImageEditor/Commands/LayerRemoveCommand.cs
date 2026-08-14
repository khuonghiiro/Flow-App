// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command: xoá 1 layer khỏi document.</summary>
    public sealed class LayerRemoveCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly EditorLayer _layer;
        private readonly int _originalIndex;
        private readonly EditorLayer? _previousActiveLayer;

        public LayerRemoveCommand(EditorDocument doc, EditorLayer layer)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _originalIndex = doc.Layers.IndexOf(layer);
            _previousActiveLayer = doc.ActiveLayer;
        }

        public string Description => $"Remove layer \"{_layer.Name}\"";

        public void Execute()
        {
            var idx = _doc.Layers.IndexOf(_layer);
            if (idx < 0) return;

            _doc.Layers.RemoveAt(idx);

            // Chọn layer gần nhất
            if (_doc.ActiveLayer == _layer && _doc.Layers.Count > 0)
                _doc.ActiveLayer = _doc.Layers[Math.Min(idx, _doc.Layers.Count - 1)];
        }

        public void Undo()
        {
            int idx = Math.Clamp(_originalIndex, 0, _doc.Layers.Count);
            _doc.Layers.Insert(idx, _layer);
            _doc.ActiveLayer = _previousActiveLayer ?? _layer;
        }
    }
}
